// Assets/Scripts/Replay/ReplayManager.cs
using System.Collections.Generic;
using System.IO;
using UnityEngine;

[AddComponentMenu("StickIt/Replay Manager")]
public class ReplayManager : MonoBehaviour
{
    public static ReplayManager Instance 
    { 
        get 
        {
            if (_instance == null)
            {
                // 자동 생성: 메이킹 씬 등에서 ReplayManager가 없을 때 대비
                var go = new GameObject("ReplayManager (Auto-Created)");
                _instance = go.AddComponent<ReplayManager>();
                DontDestroyOnLoad(go);
                Debug.Log("[ReplayManager] 인스턴스 자동 생성됨");
            }
            return _instance;
        }
        private set { _instance = value; }
    }
    private static ReplayManager _instance;

    /// <summary>
    /// OnDestroy 등에서 안전하게 사용할 수 있는 메서드 (새 인스턴스 생성하지 않음)
    /// </summary>
    public static ReplayManager GetExistingInstance()
    {
        return _instance;
    }
    public static string CacheFilePath =>
        Path.Combine(Application.temporaryCachePath, "stickit_last.replay"); // cached single replay

    // Binary header
    const uint MAGIC = 0x53525032; // "SRP2"
    const int VERSION = 3; // v3: 고유 식별자 시스템 지원

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
    private readonly List<string> _trackIdentifiers = new(); // 고유 식별자 저장

    private readonly List<float> _times = new(2048);
    private readonly List<Vector3> _pos = new(4096);
    private readonly List<float> _rotZ = new(4096);

    private int _trackCount;
    private bool _isRecording;
    private float _elapsed, _nextSample;

    void Awake()
    {
        if (_instance != null && _instance != this) 
        { 
            Destroy(gameObject); 
            return; 
        }
        _instance = this;
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
            identifiers = _trackIdentifiers.ToArray(),
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
        _tracks.Clear(); _trackRBs.Clear(); _trackIdentifiers.Clear();

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
            
            // 고유 식별자 생성/가져오기
            var uniqueId = GetOrCreateUniqueId(t);
            _trackIdentifiers.Add(uniqueId.GetFullIdentifier());
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

    /// <summary>
    /// Transform에 ReplayUniqueId 컬포넌트를 찾거나 생성
    /// </summary>
    private ReplayUniqueId GetOrCreateUniqueId(Transform t)
    {
        var uniqueId = t.GetComponent<ReplayUniqueId>();
        if (uniqueId == null)
        {
            uniqueId = t.gameObject.AddComponent<ReplayUniqueId>();
        }
        return uniqueId;
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

            // identifiers (v3 이상)
            bw.Write(d.identifiers?.Length ?? 0);
            if (d.identifiers != null)
            {
                for (int i = 0; i < d.identifiers.Length; i++)
                    bw.Write(d.identifiers[i] ?? "");
            }

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
            int version = br.ReadInt32();
            if (version < 2 || version > VERSION) throw new IOException($"Unsupported version: {version}");

            var data = new ReplayData
            {
                step = br.ReadSingle(),
                trackCount = br.ReadInt32()
            };

            int nIds = br.ReadInt32();
            if (version >= 3)
            {
                // v3 이상: identifiers 사용
                data.identifiers = new string[nIds];
                for (int i = 0; i < nIds; i++)
                    data.identifiers[i] = br.ReadString();
            }
            else
            {
                // v2 이하: 하위 호환성을 위해 paths를 identifiers로 변환
#pragma warning disable CS0618 // Obsolete warning suppression
                data.paths = new string[nIds];
                data.identifiers = new string[nIds];
                for (int i = 0; i < nIds; i++)
                {
                    string path = br.ReadString();
                    data.paths[i] = path;
                    data.identifiers[i] = path; // 경로를 식별자로 사용 (하위 호환)
                }
#pragma warning restore CS0618
            }

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
