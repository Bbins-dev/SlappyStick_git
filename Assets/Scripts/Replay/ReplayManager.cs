// Assets/Scripts/Replay/ReplayManager.cs
using System.Collections.Generic;
using System.IO;
using UnityEngine;

[AddComponentMenu("StickIt/Replay Manager")]
public class ReplayManager : MonoBehaviour
{
    public static ReplayManager Instance { get; private set; }
    public static string CacheFilePath =>
        Path.Combine(Application.temporaryCachePath, "stickit_last.replay"); // cached single replay

    // Binary header
    const uint MAGIC = 0x53525032; // "SRP2"
    const int VERSION = 2;

    [Header("Record Settings")]
    [Tooltip("Sampling interval in seconds (unscaled)")]
    public float recordStep = 1f / 60f;

    [Header("Capture Filter")]
    [Tooltip("GameObject tags to auto-capture (add more later e.g. Enemy, Trap...)")]
    [SerializeField] private string[] captureTags = new[] { "Stick", "Obstacle" };

    [Tooltip("Include inactive scene objects when collecting targets")]
    [SerializeField] private bool captureInactiveObjects = false;

    // ---- runtime state (new unified buffers) ----
    private readonly List<Transform> _tracks = new();
    private readonly List<Rigidbody2D> _trackRBs = new();
    private readonly List<string> _trackPaths = new();

    private readonly List<float> _times = new(2048);
    private readonly List<Vector3> _pos = new(4096);
    private readonly List<float> _rotZ = new(4096);

    private int _trackCount;
    private bool _isRecording;
    private float _elapsed, _nextSample;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // ===================== API =====================

    /// <summary>
    /// Begin recording with auto-collected targets:
    /// - All objects whose tag is in `captureTags` (e.g. Stick, Obstacle)
    /// - Any object with ReplayOptIn (optional, includeChildren supported)
    /// - Fallback: first StickMove in scene (even if no tag set)
    /// </summary>
    public void BeginRecording()
    {
        if (_isRecording) return;
        _isRecording = true;

        CollectCaptureTargets();          // auto-collect (tags + opt-in + fallback)

        _times.Clear(); _pos.Clear(); _rotZ.Clear();
        _elapsed = 0f; _nextSample = 0f;
#if UNITY_EDITOR
        Debug.Log($"[Replay] BeginRecording: tracks={_trackCount}");
#endif
    }

    /// <summary>
    /// Overload: begin recording and also include extra targets explicitly.
    /// </summary>
    public void BeginRecording(params Transform[] extraTargets)
    {
        if (_isRecording) return;
        _isRecording = true;

        CollectCaptureTargets();

        if (extraTargets != null)
        {
            foreach (var t in extraTargets)
                AddTrack(t, includeChildren: false); // ensure uniqueness
            _trackCount = _tracks.Count;
        }

        _times.Clear(); _pos.Clear(); _rotZ.Clear();
        _elapsed = 0f; _nextSample = 0f;
#if UNITY_EDITOR
        Debug.Log($"[Replay] BeginRecording(+extra): tracks={_trackCount}");
#endif
    }

    /// <summary>
    /// End recording. keepFile=true on success (clear), false on fail (respawn).
    /// </summary>
    public void EndRecording(bool keepFile)
    {
        if (!_isRecording) return;
        _isRecording = false;

        if (!keepFile)
        {
            TryDeleteCache();
#if UNITY_EDITOR
            Debug.Log("[Replay] EndRecording: discarded (fail).");
#endif
            return;
        }

        // pack and save
        var data = new ReplayData
        {
            step = recordStep,
            trackCount = _trackCount,
            paths = _trackPaths.ToArray(),
            times = _times.ToArray(),
            pos = _pos.ToArray(),
            rotZ = _rotZ.ToArray()
        };
        SaveToCache(data);
#if UNITY_EDITOR
        Debug.Log($"[Replay] EndRecording: saved tracks={data.trackCount}, frames={data.times.Length}");
#endif
    }

    public bool HasCache => File.Exists(CacheFilePath);

    public void TryDeleteCache()
    {
        try { if (File.Exists(CacheFilePath)) File.Delete(CacheFilePath); }
        catch { /* ignore */ }
    }

    public ReplayData LoadFromCache()
    {
        if (!File.Exists(CacheFilePath)) return null;
        return LoadCacheInternal();
    }

    // ===================== Runtime sampling =====================

    void Update()
    {
        if (!_isRecording) return;

        _elapsed += Time.unscaledDeltaTime;
        while (_elapsed >= _nextSample)
        {
            Sample(_nextSample);
            _nextSample += recordStep;
        }
    }

    private void Sample(float t)
    {
        _times.Add(t);

        // for each tracked transform, store position (Vector3) and z-rot
        for (int i = 0; i < _trackCount; i++)
        {
            var tr = _tracks[i];
            var rb = _trackRBs[i];

            if (!tr) { _pos.Add(Vector3.zero); _rotZ.Add(0f); continue; }

            if (rb)
            {
                // Rigidbody2D uses XY; keep Z from Transform for compatibility with ReplayPlayer
                var tp = tr.position;
                _pos.Add(new Vector3(rb.position.x, rb.position.y, tp.z));
                _rotZ.Add(rb.rotation);
            }
            else
            {
                var tp = tr.position;
                _pos.Add(tp);
                _rotZ.Add(tr.eulerAngles.z);
            }
        }
    }

    // ===================== Target collection =====================

    private void CollectCaptureTargets()
    {
        _tracks.Clear(); _trackRBs.Clear(); _trackPaths.Clear();

        // 1) ReplayOptIn first (optional component)
        ReplayOptIn[] optIns = FindObjectsOfType<ReplayOptIn>(captureInactiveObjects);
        foreach (var opt in optIns)
            AddTrack(opt.transform, opt.includeChildren);

        // 2) Tag-matched
#if UNITY_2023_1_OR_NEWER
        var allGos = FindObjectsByType<GameObject>(
            captureInactiveObjects ? FindObjectsInactive.Include : FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
#else
        var allGos = FindObjectsOfType<GameObject>(captureInactiveObjects);
#endif
        foreach (var go in allGos)
        {
            if (!go) continue;
            if (!go.activeInHierarchy && !captureInactiveObjects) continue;
            if (MatchesCaptureTag(go))
                AddTrack(go.transform, includeChildren: false);
        }

        // 3) Fallback: ensure at least the Stick exists (via StickMove)
        var sticks = FindObjectsOfType<StickMove>(captureInactiveObjects);
        if (sticks != null && sticks.Length > 0)
            AddTrack(sticks[0].transform, includeChildren: false);

        _trackCount = _tracks.Count;
    }

    private void AddTrack(Transform t, bool includeChildren)
    {
        if (!t) return;

        if (!_tracks.Contains(t))
        {
            _tracks.Add(t);
            _trackRBs.Add(t.GetComponent<Rigidbody2D>());
            _trackPaths.Add(BuildFullPath(t));
        }

        if (includeChildren)
        {
            for (int i = 0; i < t.childCount; i++)
                AddTrack(t.GetChild(i), true);
        }
    }

    private bool MatchesCaptureTag(GameObject go)
    {
        if (captureTags == null) return false;
        for (int i = 0; i < captureTags.Length; i++)
        {
            var tag = captureTags[i];
            if (!string.IsNullOrEmpty(tag) && go.CompareTag(tag))
                return true;
        }
        return false;
    }

    private static string BuildFullPath(Transform t)
    {
        var stack = new System.Collections.Generic.Stack<string>();
        while (t != null) { stack.Push(t.name); t = t.parent; }
        return string.Join("/", stack);
    }

    // ===================== IO =====================

    private void SaveToCache(ReplayData d)
    {
        try
        {
            using var fs = new FileStream(CacheFilePath, FileMode.Create, FileAccess.Write);
            using var bw = new BinaryWriter(fs);

            bw.Write(MAGIC);
            bw.Write(VERSION);

            bw.Write(d.step);
            bw.Write(d.trackCount);

            // paths
            bw.Write(d.paths.Length);
            for (int i = 0; i < d.paths.Length; i++)
                bw.Write(d.paths[i] ?? "");

            // times
            bw.Write(d.times.Length);
            for (int i = 0; i < d.times.Length; i++)
                bw.Write(d.times[i]);

            // pos (Vector3)
            bw.Write(d.pos.Length);
            for (int i = 0; i < d.pos.Length; i++)
            {
                bw.Write(d.pos[i].x);
                bw.Write(d.pos[i].y);
                bw.Write(d.pos[i].z);
            }

            // rotZ
            bw.Write(d.rotZ.Length);
            for (int i = 0; i < d.rotZ.Length; i++)
                bw.Write(d.rotZ[i]);

            Debug.Log($"[Replay] Cache saved: {CacheFilePath}");
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[Replay] Save failed: {e.Message}");
        }
    }

    private ReplayData LoadCacheInternal()
    {
        try
        {
            using var fs = new FileStream(CacheFilePath, FileMode.Open, FileAccess.Read);
            using var br = new BinaryReader(fs);

            if (br.ReadUInt32() != MAGIC) throw new IOException("Invalid header");
            if (br.ReadInt32() != VERSION) throw new IOException("Version mismatch");

            var data = new ReplayData
            {
                step = br.ReadSingle(),
                trackCount = br.ReadInt32()
            };

            int nPaths = br.ReadInt32();
            data.paths = new string[nPaths];
            for (int i = 0; i < nPaths; i++)
                data.paths[i] = br.ReadString();

            int nTimes = br.ReadInt32();
            data.times = new float[nTimes];
            for (int i = 0; i < nTimes; i++)
                data.times[i] = br.ReadSingle();

            int nPos = br.ReadInt32();
            data.pos = new Vector3[nPos];
            for (int i = 0; i < nPos; i++)
            {
                float x = br.ReadSingle();
                float y = br.ReadSingle();
                float z = br.ReadSingle();
                data.pos[i] = new Vector3(x, y, z);
            }

            int nRot = br.ReadInt32();
            data.rotZ = new float[nRot];
            for (int i = 0; i < nRot; i++)
                data.rotZ[i] = br.ReadSingle();

            return data;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[Replay] Read cache failed: {e.Message}");
            return null;
        }
    }
}
