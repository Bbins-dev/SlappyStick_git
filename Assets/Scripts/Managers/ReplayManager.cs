// Assets/Scripts/Replay/ReplayManager.cs
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

[AddComponentMenu("StickIt/Replay Manager")]
public class ReplayManager : MonoBehaviour
{
    public static ReplayManager Instance { get; private set; }
    public static string CacheFilePath =>
        Path.Combine(Application.temporaryCachePath, "stickit_last.replay"); // 캐시 파일

    const uint MAGIC   = 0x53525032; // "SRP2"
    const int  VERSION = 2;

    [Header("Record Settings")]
    [Tooltip("녹화 샘플 간격(초) - unscaled")]
    public float recordStep = 1f / 60f;

    bool recording;
    Coroutine recCo;
    float t0;

    // 트랙/경로/타임라인 버퍼
    readonly List<Transform> tracks = new();
    string[] trackPaths;

    readonly List<float>  times = new(2048);
    readonly List<Vector3> pos  = new(4096);
    readonly List<float>  rotZ  = new(4096);

    void Awake() {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // ---------------- API ----------------

    /// <summary>발사 직전(마우스 업)에 호출. 기본은 스틱 1트랙만 녹화하면 됩니다.</summary>
    public void BeginRecording(params Transform[] targets) {
        EndRecording(false); // 이전 세션 정리

        tracks.Clear();
        if (targets != null) tracks.AddRange(targets);
        if (tracks.Count == 0) { Debug.LogWarning("[Replay] No targets to record."); return; }

        // 경로 계산(바인딩용)
        trackPaths = new string[tracks.Count];
        for (int i = 0; i < tracks.Count; i++)
            trackPaths[i] = BuildFullPath(tracks[i]);

        times.Clear(); pos.Clear(); rotZ.Clear();

        t0 = Time.unscaledTime;
        recording = true;
        recCo = StartCoroutine(CoRecord());
    }

    /// <summary>성공(클리어)=keepFile:true / 실패(리스폰)=false 로 호출</summary>
    public void EndRecording(bool keepFile) {
        if (!recording) return;
        recording = false;
        if (recCo != null) { StopCoroutine(recCo); recCo = null; }

        if (!keepFile) { TryDeleteCache(); return; }

        // 데이터 조립 후 저장
        var data = new ReplayData {
            step       = recordStep,
            trackCount = tracks.Count,
            paths      = trackPaths,
            times      = times.ToArray(),
            pos        = pos.ToArray(),
            rotZ       = rotZ.ToArray(),
        };
        SaveToCache(data);
    }

    public void TryDeleteCache() {
        try { if (File.Exists(CacheFilePath)) File.Delete(CacheFilePath); }
        catch { /* ignore */ }
    }

    public ReplayData LoadFromCache() {
        if (!File.Exists(CacheFilePath)) return null;
        return LoadCacheInternal();
    }

    public bool HasCache => File.Exists(CacheFilePath);

    // -------------- 내부 구현 --------------

    IEnumerator CoRecord() {
        var wait = new WaitForSecondsRealtime(Mathf.Max(0.0001f, recordStep));
        while (recording) {
            // 한 프레임 기록
            times.Add(Time.unscaledTime - t0);
            for (int i = 0; i < tracks.Count; i++) {
                var tr = tracks[i];
                if (!tr) { pos.Add(Vector3.zero); rotZ.Add(0f); continue; }
                pos.Add(tr.position);
                float z = tr.eulerAngles.z;
                rotZ.Add(z);
            }
            yield return wait;
        }
    }

    void SaveToCache(ReplayData d) {
        try {
            using var fs = new FileStream(CacheFilePath, FileMode.Create, FileAccess.Write);
            using var bw = new BinaryWriter(fs);

            bw.Write(MAGIC);
            bw.Write(VERSION);

            bw.Write(d.step);
            bw.Write(d.trackCount);

            // paths
            bw.Write(d.paths.Length);
            for (int i = 0; i < d.paths.Length; i++) bw.Write(d.paths[i] ?? "");

            // times
            bw.Write(d.times.Length);
            for (int i = 0; i < d.times.Length; i++) bw.Write(d.times[i]);

            // pos
            bw.Write(d.pos.Length);
            for (int i = 0; i < d.pos.Length; i++) {
                bw.Write(d.pos[i].x); bw.Write(d.pos[i].y); bw.Write(d.pos[i].z);
            }

            // rotZ
            bw.Write(d.rotZ.Length);
            for (int i = 0; i < d.rotZ.Length; i++) bw.Write(d.rotZ[i]);

            Debug.Log($"[Replay] Cache saved: {CacheFilePath}");
        } catch (System.Exception e) {
            Debug.LogWarning($"[Replay] Save failed: {e.Message}");
        }
    }

    ReplayData LoadCacheInternal() {
        try {
            using var fs = new FileStream(CacheFilePath, FileMode.Open, FileAccess.Read);
            using var br = new BinaryReader(fs);

            if (br.ReadUInt32() != MAGIC)        throw new IOException("Invalid header");
            if (br.ReadInt32()   != VERSION)      throw new IOException("Version mismatch");

            var data = new ReplayData();
            data.step       = br.ReadSingle();
            data.trackCount = br.ReadInt32();

            int nPaths = br.ReadInt32();
            data.paths = new string[nPaths];
            for (int i = 0; i < nPaths; i++) data.paths[i] = br.ReadString();

            int nTimes = br.ReadInt32();
            data.times = new float[nTimes];
            for (int i = 0; i < nTimes; i++) data.times[i] = br.ReadSingle();

            int nPos = br.ReadInt32();
            data.pos = new Vector3[nPos];
            for (int i = 0; i < nPos; i++) {
                float x = br.ReadSingle(), y = br.ReadSingle(), z = br.ReadSingle();
                data.pos[i] = new Vector3(x, y, z);
            }

            int nRot = br.ReadInt32();
            data.rotZ = new float[nRot];
            for (int i = 0; i < nRot; i++) data.rotZ[i] = br.ReadSingle();

            return data;
        } catch (System.Exception e) {
            Debug.LogWarning($"[Replay] Read cache failed: {e.Message}");
            return null;
        }
    }

    // 트랜스폼 풀 경로 (씬 루트부터)
    static string BuildFullPath(Transform t) {
        var stack = new Stack<string>();
        var cur = t;
        while (cur != null) { stack.Push(cur.name); cur = cur.parent; }
        return string.Join("/", stack);
    }
}
