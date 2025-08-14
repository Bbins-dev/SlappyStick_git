// Assets/Scripts/Replay/ReplayPlayer.cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[AddComponentMenu("StickIt/Replay Player")]
public class ReplayPlayer : MonoBehaviour
{
    [Tooltip("When entering replay, hide normal HUD (UIRoot.HUD)")]
    public bool hideHUD = true;

    private readonly List<Transform> boundTargets = new();
    private readonly List<Rigidbody2D> disabledBodies = new();

    private ReplayData data;     // trackCount, step, paths[], pos[], rotZ[]
    private bool isPlaying;

    public bool IsPlaying => isPlaying;
    public bool HasCachedReplay => System.IO.File.Exists(ReplayManager.CacheFilePath);

    public void PlayCached()
    {
        if (isPlaying) return;

        data = ReplayManager.Instance?.LoadFromCache();
        if (data == null)
        {
            Debug.LogWarning("[Replay] No cached replay.");
            return;
        }

        if (!ValidateData(data))
        {
            Debug.LogWarning("[Replay] Invalid replay data.");
            return;
        }

        BindTargetsToPaths();
        StartCoroutine(CoPlay());
    }

    private bool ValidateData(ReplayData d)
    {
        if (d.trackCount <= 0) return false;
        if (d.pos == null || d.rotZ == null) return false;

        int framesByPos = d.pos.Length / d.trackCount;
        int framesByRot = d.rotZ.Length / d.trackCount;

        if (framesByPos <= 0 || framesByRot <= 0 || framesByPos != framesByRot) return false;

        if (d.paths == null || d.paths.Length < d.trackCount)
        {
            Debug.LogWarning($"[Replay] paths count {d.paths?.Length ?? 0} < trackCount {d.trackCount}. Missing names will use ghosts.");
            // 허용하되, 부족한 트랙은 고스트 GO로 채움
        }
        return true;
    }

    private void BindTargetsToPaths()
    {
        boundTargets.Clear();

        for (int i = 0; i < data.trackCount; i++)
        {
            string path = (data.paths != null && i < data.paths.Length) ? data.paths[i] : string.Empty;

            var t = !string.IsNullOrEmpty(path) ? FindTransformInAllScenesByPath(path) : null;
            if (t == null)
            {
                var ghostRoot = GetOrCreateGhostRoot();
                var go = new GameObject($"ReplayGhost_{i}");
                go.transform.SetParent(ghostRoot, worldPositionStays: false);
                t = go.transform;
            }
            boundTargets.Add(t);
        }
    }

    private static Transform GetOrCreateGhostRoot()
    {
        var existing = GameObject.Find("ReplayGhosts");
        if (existing) return existing.transform;
        var root = new GameObject("ReplayGhosts");
        Object.DontDestroyOnLoad(root);
        return root.transform;
    }

    private Transform FindTransformInAllScenesByPath(string fullPath)
    {
        if (string.IsNullOrEmpty(fullPath)) return null;

        // 메인 카메라 특별 케이스
        if (fullPath.EndsWith("Main Camera") && Camera.main)
            return Camera.main.transform;

        string[] parts = fullPath.Split('/');
        if (parts.Length == 0) return null;

        int sceneCount = SceneManager.sceneCount;
        for (int si = 0; si < sceneCount; si++)
        {
            var scene = SceneManager.GetSceneAt(si);
            if (!scene.IsValid() || !scene.isLoaded) continue;

            var roots = scene.GetRootGameObjects();
            for (int r = 0; r < roots.Length; r++)
            {
                var root = roots[r].transform;
                if (root.name != parts[0]) continue;

                var cur = root;
                bool ok = true;
                for (int i = 1; i < parts.Length; i++)
                {
                    cur = cur.Find(parts[i]);
                    if (cur == null) { ok = false; break; }
                }
                if (ok && cur != null) return cur;
            }
        }
        return null;
    }

    private IEnumerator CoPlay()
    {
        isPlaying = true;

        // 물리 잠깐 비활성
        disabledBodies.Clear();
        foreach (var t in boundTargets)
        {
            if (!t) continue;
            var rb = t.GetComponent<Rigidbody2D>();
            if (rb && rb.simulated)
            {
                rb.simulated = false;
                disabledBodies.Add(rb);
            }
        }

        // HUD 숨김
        GameObject hudToRestore = null;
        if (hideHUD && UIRoot.Instance && UIRoot.Instance.hudRoot)
        {
            hudToRestore = UIRoot.Instance.hudRoot;
            hudToRestore.SetActive(false);
        }

        // 재생 루프 (frameCount 계산)
        float step = Mathf.Max(0.0001f, data.step);
        int tracks = data.trackCount;
        int frameCount = data.pos.Length / tracks;

        for (int f = 0; f < frameCount; f++)
        {
            int baseIdx = f * tracks;
            for (int t = 0; t < tracks; t++)
            {
                var tr = boundTargets[t];
                if (!tr) continue;

                int idx = baseIdx + t;
                tr.SetPositionAndRotation(
                    data.pos[idx],
                    Quaternion.Euler(0, 0, data.rotZ[idx])
                );
            }
            yield return new WaitForSecondsRealtime(step);
        }

        // 복구
        for (int i = 0; i < disabledBodies.Count; i++)
        {
            var rb = disabledBodies[i];
            if (rb) rb.simulated = true;
        }
        disabledBodies.Clear();

        if (hudToRestore) hudToRestore.SetActive(true);

        isPlaying = false;
    }
}
