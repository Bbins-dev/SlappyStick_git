// Assets/Scripts/Debug/ReplayTimingDebugger.cs
#if UNITY_EDITOR
using UnityEngine;

/// <summary>
/// 리플레이 타이밍 문제 디버깅용 컴포넌트
/// </summary>
[AddComponentMenu("StickIt/Debug/Replay Timing Debugger")]
public class ReplayTimingDebugger : MonoBehaviour
{
    [Header("이벤트 로깅")]
    [Tooltip("TipTrigger 이벤트 로깅 여부")]
    public bool logTipTriggerEvents = true;
    
    [Tooltip("ClearPopup 이벤트 로깅 여부")]
    public bool logClearPopupEvents = true;
    
    [Tooltip("ReplayManager 상태 로깅 여부")]
    public bool logReplayManagerState = true;

    [Header("실시간 상태")]
    [SerializeField, Tooltip("현재 리플레이 재생 중인지")]
    private bool isCurrentlyReplaying;
    
    [SerializeField, Tooltip("현재 녹화 중인지")]
    private bool isCurrentlyRecording;
    
    [SerializeField, Tooltip("리플레이 캐시 존재 여부")]
    private bool hasCachedReplay;

    private void OnEnable()
    {
        // 이벤트 구독
        if (logTipTriggerEvents)
        {
            GameManager.StageClearedStatic += OnStageClearedStatic;
        }
        
        Debug.Log("[ReplayTimingDebugger] 활성화됨 - 이벤트 모니터링 시작");
    }

    private void OnDisable()
    {
        // 이벤트 구독 해제
        if (logTipTriggerEvents)
        {
            GameManager.StageClearedStatic -= OnStageClearedStatic;
        }
        
        Debug.Log("[ReplayTimingDebugger] 비활성화됨 - 이벤트 모니터링 종료");
    }

    private void Update()
    {
        // 실시간 상태 업데이트
        var replayManager = ReplayManager.Instance;
        if (replayManager != null)
        {
            isCurrentlyReplaying = replayManager.IsReplaying;
            isCurrentlyRecording = replayManager.IsRecording;
            hasCachedReplay = replayManager.HasCache;
        }
        else
        {
            isCurrentlyReplaying = false;
            isCurrentlyRecording = false;
            hasCachedReplay = false;
        }
    }

    private void OnStageClearedStatic()
    {
        if (logTipTriggerEvents)
        {
            var replayManager = ReplayManager.Instance;
            bool isReplay = replayManager?.IsReplaying ?? false;
            
            Debug.Log($"[ReplayTimingDebugger] ★ StageClearedStatic 이벤트 발생! " +
                     $"리플레이 모드: {isReplay}, " +
                     $"녹화 중: {replayManager?.IsRecording ?? false}, " +
                     $"시간: {Time.time:F2}초");
        }
    }

    [ContextMenu("Print Current State")]
    public void PrintCurrentState()
    {
        var replayManager = ReplayManager.Instance;
        var replayPlayer = FindObjectOfType<ReplayPlayer>(true);
        var tipTriggers = FindObjectsOfType<TipTrigger>();
        var clearPopup = FindObjectOfType<ClearPopupController>(true);
        
        Debug.Log("=== Replay Timing Debugger - 현재 상태 ===");
        Debug.Log($"ReplayManager: {(replayManager != null ? "존재" : "없음")}");
        if (replayManager != null)
        {
            Debug.Log($"  - 녹화 중: {replayManager.IsRecording}");
            Debug.Log($"  - 리플레이 중: {replayManager.IsReplaying}");
            Debug.Log($"  - 캐시 존재: {replayManager.HasCache}");
        }
        
        Debug.Log($"ReplayPlayer: {(replayPlayer != null ? "존재" : "없음")}");
        if (replayPlayer != null)
        {
            Debug.Log($"  - 재생 중: {replayPlayer.IsPlaying}");
        }
        
        Debug.Log($"TipTrigger 개수: {tipTriggers.Length}");
        foreach (var tip in tipTriggers)
        {
            Debug.Log($"  - {tip.name}: 트리거됨={tip.HasTriggered}");
        }
        
        Debug.Log($"ClearPopupController: {(clearPopup != null ? "존재" : "없음")}");
        Debug.Log("=======================================");
    }

    [ContextMenu("Force Test Wobble Effect")]
    public void ForceTestWobbleEffect()
    {
        var tipTrigger = FindObjectOfType<TipTrigger>();
        if (tipTrigger != null)
        {
            Debug.Log("[ReplayTimingDebugger] Wobble 이펙트 강제 테스트 시작");
            // 리플렉션을 사용하여 private 메서드 호출
            var method = typeof(TipTrigger).GetMethod("DoStuckSequence", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (method != null)
            {
                StartCoroutine((System.Collections.IEnumerator)method.Invoke(tipTrigger, new object[] { null }));
            }
        }
        else
        {
            Debug.LogWarning("[ReplayTimingDebugger] TipTrigger를 찾을 수 없음");
        }
    }
}
#endif