// Assets/Scripts/Replay/ReplayPlayer.cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using System;

[AddComponentMenu("StickIt/Replay Player")]
public class ReplayPlayer : MonoBehaviour
{
    [Tooltip("When entering replay, hide normal HUD (UIRoot.HUD)")]
    public bool hideHUD = true;

    private readonly List<Transform> boundTargets = new();
    private readonly List<Rigidbody2D> disabledBodies = new();

    private ReplayData data;             // trackCount, step, paths[], pos[], rotZ[]
    private bool isPlaying;

    // 카메라 핸들/복구 정보
    private CameraFollow camFollow;
    private Transform restoreFollow;     // 원래 Stick
    private Transform restoreInitial;    // 원래 첫 Target (없으면 Stick)

    private Action finishedCallback;     // 재생 끝나면 호출

    public bool IsPlaying => isPlaying;
    public bool HasCachedReplay => System.IO.File.Exists(ReplayManager.CacheFilePath);

    /// <summary>
    /// Play cached replay. When finished, invokes onFinished (optional).
    /// </summary>
    public void PlayCached(Action onFinished = null)
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

        finishedCallback = onFinished;

        BindTargetsToPaths();
        PrepareCameraFollowTakeover();      // ← 카메라 팔로우 인계

        // 카메라가 리플레이 동안엔 초기/포지셔닝 연출 없이 바로 타겟만 따라가게
        var camFollow = Camera.main ? Camera.main.GetComponent<CameraFollow>() : null;
        camFollow?.SetReplayOverride(true, FindStickTarget());  // 아래 B에서 헬퍼 추가

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
        DontDestroyOnLoad(root);
        return root.transform;
    }

    private Transform FindTransformInAllScenesByPath(string fullPath)
    {
        if (string.IsNullOrEmpty(fullPath)) return null;

        // 메인 카메라 특례
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

    private void PrepareCameraFollowTakeover()
    {
        // 카메라 핸들
        camFollow = Camera.main ? Camera.main.GetComponent<CameraFollow>() : null;
        if (camFollow == null) return;

        // 복구용 원래 타깃들
        restoreFollow = FindCurrentStick();
        restoreInitial = FindFirstTarget() ?? restoreFollow;

        // 리플레이 대상(대개 0번 트랙)을 초기/팔로우 모두로 설정 → 실제 플레이와 동일한 팔로우 로직/속도 사용
        var replayTarget = boundTargets.Count > 0 ? boundTargets[0] : null;
        if (replayTarget != null)
        {
            camFollow.ConfigureTargets(replayTarget, replayTarget, resetTimers: true);
        }
    }

    private Transform FindCurrentStick()
    {
        var sm = GameObject.FindObjectOfType<StickMove>();
        return sm ? sm.transform : null;
    }

    private Transform FindFirstTarget()
    {
        // 가장 간단한 방법: Tag "Target" 중 첫 번째
        var go = GameObject.FindWithTag("Target");
        return go ? go.transform : null;
    }

    private IEnumerator CoPlay()
    {
        isPlaying = true;

        // 물리 비활성
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

        // 재생
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

        // 복구: 물리
        for (int i = 0; i < disabledBodies.Count; i++)
        {
            var rb = disabledBodies[i];
            if (rb) rb.simulated = true;
        }
        disabledBodies.Clear();

        // 복구: HUD
        if (hudToRestore) hudToRestore.SetActive(true);

        // 복구: 카메라 팔로우 (원래 Stick/Target로)
        if (camFollow != null)
        {
            var stick = restoreFollow ?? FindCurrentStick();
            var init = restoreInitial ?? FindFirstTarget() ?? stick;
            if (stick != null)
                camFollow.ConfigureTargets(init, stick, resetTimers: true);
        }

        Camera.main?.GetComponent<CameraFollow>()?.SetReplayOverride(false);

        isPlaying = false;

        // 콜백 알림 (팝업 다시 열기 등)
        finishedCallback?.Invoke();
        finishedCallback = null;
    }
    
    private Transform FindStickTarget()
    {
        // 녹화 트랙 중 StickMove가 붙은 트랜스폼을 우선 사용
        foreach (var t in boundTargets)
            if (t && t.GetComponent<StickMove>() != null)
                return t;

        // 못 찾으면 첫 트랙으로 폴백
        return boundTargets.Count > 0 ? boundTargets[0] : null;
    }
}
