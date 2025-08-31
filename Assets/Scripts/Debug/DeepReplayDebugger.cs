// Assets/Scripts/Debug/DeepReplayDebugger.cs
#if UNITY_EDITOR
using UnityEngine;
using System.Collections.Generic;
using System.Collections;

/// <summary>
/// 리플레이 시스템의 모든 동작을 깊이 추적하는 디버거
/// </summary>
[AddComponentMenu("StickIt/Debug/Deep Replay Debugger")]
public class DeepReplayDebugger : MonoBehaviour
{
    [Header("디버깅 설정")]
    public bool enableDetailedLogging = true;
    public bool trackTipTriggerExecution = true;
    public bool trackWobbleExecution = true;
    public bool trackReplayPlayerState = true;
    public bool trackCollisionEvents = true;

    private List<string> eventLog = new List<string>();
    private float startTime;
    private Dictionary<string, bool> wobbleStates = new Dictionary<string, bool>();

    private void Awake()
    {
        startTime = Time.time;
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        GameManager.StageClearedStatic += OnStageClearedEvent;
        StartCoroutine(MonitorReplayState());
    }

    private void OnDisable()
    {
        GameManager.StageClearedStatic -= OnStageClearedEvent;
    }

    public void LogEvent(string eventName, string details = "")
    {
        if (!enableDetailedLogging) return;
        
        float timestamp = Time.time - startTime;
        string logEntry = $"[{timestamp:F3}s] {eventName}";
        if (!string.IsNullOrEmpty(details)) logEntry += $" - {details}";
        
        eventLog.Add(logEntry);
        Debug.Log($"[DeepDebugger] {logEntry}");
        
        // 최대 100개 로그만 유지
        if (eventLog.Count > 100)
            eventLog.RemoveAt(0);
    }

    private IEnumerator MonitorReplayState()
    {
        ReplayPlayer lastReplayPlayer = null;
        bool wasReplaying = false;
        
        while (true)
        {
            var replayManager = ReplayManager.Instance;
            var replayPlayer = FindObjectOfType<ReplayPlayer>(true);
            
            // ReplayPlayer 상태 변화 추적
            if (replayPlayer != lastReplayPlayer)
            {
                if (replayPlayer != null)
                {
                    LogEvent("NEW_REPLAY_PLAYER", $"Found new ReplayPlayer: {replayPlayer.name}");
                }
                else if (lastReplayPlayer != null)
                {
                    LogEvent("REPLAY_PLAYER_DESTROYED", "ReplayPlayer destroyed");
                }
                lastReplayPlayer = replayPlayer;
            }
            
            // 리플레이 상태 변화 추적
            if (replayPlayer != null && trackReplayPlayerState)
            {
                bool isReplaying = replayPlayer.IsPlaying;
                if (isReplaying != wasReplaying)
                {
                    LogEvent("REPLAY_STATE_CHANGED", $"IsPlaying: {wasReplaying} → {isReplaying}");
                    wasReplaying = isReplaying;
                }
            }
            
            yield return new WaitForSeconds(0.1f);
        }
    }

    private void OnStageClearedEvent()
    {
        var replayManager = ReplayManager.Instance;
        LogEvent("STAGE_CLEARED_EVENT", 
            $"IsReplaying: {replayManager?.IsReplaying ?? false}, " +
            $"IsRecording: {replayManager?.IsRecording ?? false}");
    }

    [ContextMenu("Print Full Event Log")]
    public void PrintFullEventLog()
    {
        Debug.Log("=== DEEP REPLAY DEBUGGER - FULL EVENT LOG ===");
        foreach (string entry in eventLog)
        {
            Debug.Log(entry);
        }
        Debug.Log($"=== TOTAL: {eventLog.Count} EVENTS ===");
    }

    [ContextMenu("Monitor All TipTriggers")]
    public void MonitorAllTipTriggers()
    {
        var tipTriggers = FindObjectsOfType<TipTrigger>();
        LogEvent("MONITORING_TIP_TRIGGERS", $"Found {tipTriggers.Length} TipTriggers");
        
        foreach (var trigger in tipTriggers)
        {
            // 각 TipTrigger에 모니터링 컴포넌트 추가
            var monitor = trigger.gameObject.GetComponent<TipTriggerMonitor>();
            if (monitor == null)
            {
                monitor = trigger.gameObject.AddComponent<TipTriggerMonitor>();
                monitor.debugger = this;
            }
        }
    }

    public void OnTipTriggerExecuted(string triggerName, bool isReplayMode)
    {
        LogEvent("TIP_TRIGGER_EXECUTED", $"Name: {triggerName}, ReplayMode: {isReplayMode}");
    }

    public void OnWobbleStarted(string triggerName)
    {
        LogEvent("WOBBLE_STARTED", $"Trigger: {triggerName}");
        wobbleStates[triggerName] = true;
    }

    public void OnWobbleEnded(string triggerName)
    {
        LogEvent("WOBBLE_ENDED", $"Trigger: {triggerName}");
        wobbleStates[triggerName] = false;
    }

    public void OnGameManagerNotified(string triggerName)
    {
        LogEvent("GAME_MANAGER_NOTIFIED", $"From Trigger: {triggerName}");
    }

    [ContextMenu("Force Analysis")]
    public void ForceAnalysis()
    {
        StartCoroutine(AnalyzeCurrentState());
    }

    private IEnumerator AnalyzeCurrentState()
    {
        LogEvent("FORCE_ANALYSIS_START");
        
        // 모든 관련 컴포넌트 상태 체크
        var replayManager = ReplayManager.Instance;
        var replayPlayer = FindObjectOfType<ReplayPlayer>(true);
        var tipTriggers = FindObjectsOfType<TipTrigger>();
        var clearPopup = FindObjectOfType<ClearPopupController>(true);
        
        LogEvent("ANALYSIS_REPLAY_MANAGER", 
            $"Exists: {replayManager != null}, " +
            $"IsRecording: {replayManager?.IsRecording ?? false}, " +
            $"IsReplaying: {replayManager?.IsReplaying ?? false}, " +
            $"HasCache: {replayManager?.HasCache ?? false}");
            
        LogEvent("ANALYSIS_REPLAY_PLAYER", 
            $"Exists: {replayPlayer != null}, " +
            $"IsPlaying: {replayPlayer?.IsPlaying ?? false}");
            
        LogEvent("ANALYSIS_TIP_TRIGGERS", $"Count: {tipTriggers.Length}");
        foreach (var trigger in tipTriggers)
        {
            LogEvent("ANALYSIS_TIP_TRIGGER", 
                $"Name: {trigger.name}, " +
                $"HasTriggered: {trigger.HasTriggered}, " +
                $"Position: {trigger.transform.position}");
        }
        
        LogEvent("ANALYSIS_CLEAR_POPUP", 
            $"Exists: {clearPopup != null}");
            
        // Physics 상태도 체크
        var rigidbodies = FindObjectsOfType<Rigidbody2D>();
        LogEvent("ANALYSIS_RIGIDBODIES", $"Total Count: {rigidbodies.Length}");
        
        int kinematicCount = 0;
        foreach (var rb in rigidbodies)
        {
            if (rb.isKinematic) kinematicCount++;
        }
        LogEvent("ANALYSIS_KINEMATIC_COUNT", $"{kinematicCount}/{rigidbodies.Length}");
        
        yield return null;
        LogEvent("FORCE_ANALYSIS_END");
    }
}

/// <summary>
/// 개별 TipTrigger를 모니터링하는 컴포넌트
/// </summary>
public class TipTriggerMonitor : MonoBehaviour
{
    public DeepReplayDebugger debugger;
    
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (debugger != null)
        {
            debugger.LogEvent("COLLISION_DETECTED", 
                $"TipTrigger: {name}, Other: {other.name}, Tag: {other.tag}");
        }
    }
}
#endif