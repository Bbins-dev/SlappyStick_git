// Assets/Scripts/Replay/ReplayUniqueId.cs
using UnityEngine;

/// <summary>
/// 리플레이 시스템에서 같은 이름의 오브젝트들을 구분하기 위한 고유 식별자 컴포넌트
/// </summary>
[AddComponentMenu("StickIt/Replay Unique ID")]
public class ReplayUniqueId : MonoBehaviour
{
    [SerializeField, Tooltip("고유 식별자 (자동 생성되거나 수동 설정 가능)")]
    private string uniqueId = "";

    public string UniqueId 
    { 
        get 
        {
            if (string.IsNullOrEmpty(uniqueId))
            {
                GenerateNewId();
            }
            return uniqueId;
        } 
        set => uniqueId = value;
    }

    private void Awake()
    {
        // ID가 없으면 자동 생성
        if (string.IsNullOrEmpty(uniqueId))
        {
            GenerateNewId();
        }
    }

    /// <summary>
    /// 새로운 고유 ID 생성
    /// </summary>
    public void GenerateNewId()
    {
        uniqueId = System.Guid.NewGuid().ToString("N")[..8]; // 8자리 단축 ID
    }

    /// <summary>
    /// 경로와 고유 ID를 결합한 풀 식별자 반환
    /// </summary>
    public string GetFullIdentifier()
    {
        string path = BuildFullPath(transform);
        return $"{path}#{UniqueId}";
    }

    /// <summary>
    /// Transform의 풀 경로 생성
    /// </summary>
    private static string BuildFullPath(Transform t)
    {
        var stack = new System.Collections.Generic.Stack<string>();
        while (t != null) 
        { 
            stack.Push(t.name); 
            t = t.parent; 
        }
        return string.Join("/", stack);
    }

#if UNITY_EDITOR
    [ContextMenu("Generate New ID")]
    public void EditorGenerateNewId()
    {
        GenerateNewId();
        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif
}