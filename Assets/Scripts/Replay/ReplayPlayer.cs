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
    private bool isInterpolating;
    private float replayStartTime;
    

    // 카메라 핸들/복구 정보
    private StickItCamera camFollow;
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
        var camFollow = Camera.main ? Camera.main.GetComponent<StickItCamera>() : null;
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

        if (d.identifiers == null || d.identifiers.Length < d.trackCount)
        {
            Debug.LogWarning($"[Replay] identifiers count {d.identifiers?.Length ?? 0} < trackCount {d.trackCount}. Missing identifiers will use ghosts.");
        }
        return true;
    }

    private void BindTargetsToPaths()
    {
        boundTargets.Clear();

        for (int i = 0; i < data.trackCount; i++)
        {
            string identifier = (data.identifiers != null && i < data.identifiers.Length) ? data.identifiers[i] : string.Empty;

            var t = !string.IsNullOrEmpty(identifier) ? FindTransformByIdentifier(identifier) : null;
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

    /// <summary>
    /// 고유 식별자로 Transform 찾기 (경로#ID 형태)
    /// </summary>
    private Transform FindTransformByIdentifier(string identifier)
    {
        if (string.IsNullOrEmpty(identifier)) return null;

        // 메인 카메라 특례 (ID 없이 경로만 있을 수 있음)
        if (identifier.EndsWith("Main Camera") && Camera.main)
            return Camera.main.transform;

        // 경로#ID 형태로 분리
        string[] parts = identifier.Split('#');
        string path = parts[0];
        string uniqueId = parts.Length > 1 ? parts[1] : "";

        // 고유 ID가 있으면 우선 찾기
        if (!string.IsNullOrEmpty(uniqueId))
        {
            var allUniqueIds = FindObjectsOfType<ReplayUniqueId>();
            foreach (var uid in allUniqueIds)
            {
                if (uid.UniqueId == uniqueId)
                {
                    return uid.transform;
                }
            }
        }

        // ID로 못 찾으면 경로로 폴백 (하위 호환)
        return FindTransformByPath(path);
    }

    /// <summary>
    /// 경로로 Transform 찾기 (하위 호환용)
    /// </summary>
    private Transform FindTransformByPath(string fullPath)
    {
        if (string.IsNullOrEmpty(fullPath)) return null;

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
        camFollow = Camera.main ? Camera.main.GetComponent<StickItCamera>() : null;
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
        isInterpolating = true;
        replayStartTime = Time.realtimeSinceStartup;
        
        Debug.Log($"[ReplayPlayer] ★★★ 리플레이 재생 시작 ★★★ Time: {Time.time:F3}");
        Debug.Log($"[ReplayPlayer] 바인딩된 타겟 수: {boundTargets.Count}");
        for (int i = 0; i < boundTargets.Count; i++)
        {
            var target = boundTargets[i];
            if (target != null)
            {
                var rb = target.GetComponent<Rigidbody2D>();
                Debug.Log($"[ReplayPlayer] 타겟[{i}]: {target.name}, Rigidbody2D: {rb != null}, Position: {target.position}");
            }
        }
        
        // 실제 재생이 끝날 때까지 대기 (Update에서 EndReplay 호출할 때까지)
        while (isInterpolating)
        {
            yield return null;
        }
        
        Debug.Log($"[ReplayPlayer] ★★★ 리플레이 재생 완료 ★★★ Time: {Time.time:F3}");
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

    private void EndReplay()
    {
        isPlaying = false;
        isInterpolating = false;
        finishedCallback?.Invoke();
        finishedCallback = null;
    }

    void Update()
    {
        if (!isInterpolating || data == null) return;
        float elapsed = Time.realtimeSinceStartup - replayStartTime;
        
        float step = Mathf.Max(0.0001f, data.step);
        int tracks = data.trackCount;
        int frameCount = data.pos.Length / tracks;
        float t = elapsed / step;
        int f0 = Mathf.FloorToInt(t);
        int f1 = Mathf.Min(f0 + 1, frameCount - 1);
        float lerp = Mathf.Clamp01(t - f0);

        if (f0 >= frameCount - 1) {
            // 마지막 프레임 고정
            for (int trk = 0; trk < tracks; trk++) {
                var tr = boundTargets[trk];
                if (!tr) continue;
                int idx = (frameCount - 1) * tracks + trk;
                
                // Rigidbody가 있으면 물리 기반으로, 없으면 Transform 직접 설정
                var rb = tr.GetComponent<Rigidbody2D>();
                if (rb != null && !rb.isKinematic)
                {
                    rb.position = data.pos[idx];
                    rb.rotation = data.rotZ[idx];
                }
                else
                {
                    tr.SetPositionAndRotation(data.pos[idx], Quaternion.Euler(0, 0, data.rotZ[idx]));
                }
            }
            EndReplay();
            return;
        }

        for (int trk = 0; trk < tracks; trk++) {
            var tr = boundTargets[trk];
            if (!tr) continue;
            int idx0 = f0 * tracks + trk;
            int idx1 = f1 * tracks + trk;
            Vector3 pos = Vector3.Lerp(data.pos[idx0], data.pos[idx1], lerp);
            float rotZ = Mathf.LerpAngle(data.rotZ[idx0], data.rotZ[idx1], lerp);
            
            // Rigidbody가 있으면 물리 기반으로, 없으면 Transform 직접 설정
            var rb = tr.GetComponent<Rigidbody2D>();
            if (rb != null && !rb.isKinematic)
            {
                // 물리 기반 움직임으로 충돌 감지 가능
                rb.position = pos;
                rb.rotation = rotZ;
            }
            else
            {
                // Transform 직접 설정 (물리 충돌 없음)
                tr.SetPositionAndRotation(pos, Quaternion.Euler(0, 0, rotZ));
            }
        }
    }
    
}
