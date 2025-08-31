// Assets/Scripts/Debug/ReplaySystemTest.cs
#if UNITY_EDITOR
using UnityEngine;

[AddComponentMenu("StickIt/Debug/Replay System Test")]
public class ReplaySystemTest : MonoBehaviour
{
    [Header("테스트 설정")]
    [Tooltip("같은 이름의 테스트 오브젝트 개수")]
    public int duplicateObjectCount = 3;
    
    [Tooltip("테스트 오브젝트 이름")]
    public string testObjectName = "TestObstacle";

    [Header("테스트 실행")]
    [Tooltip("테스트 오브젝트들을 생성합니다")]
    public bool createTestObjects = false;

    [Tooltip("모든 오브젝트의 고유 ID를 출력합니다")]
    public bool printAllUniqueIds = false;

    void Update()
    {
        if (createTestObjects)
        {
            createTestObjects = false;
            CreateTestObjects();
        }

        if (printAllUniqueIds)
        {
            printAllUniqueIds = false;
            PrintAllUniqueIds();
        }
    }

    private void CreateTestObjects()
    {
        Debug.Log($"[ReplayTest] 같은 이름의 테스트 오브젝트 {duplicateObjectCount}개 생성");

        for (int i = 0; i < duplicateObjectCount; i++)
        {
            var go = new GameObject(testObjectName);
            go.transform.position = new Vector3(i * 2f, 0f, 0f);
            
            // Rigidbody2D 추가 (물리 시뮬레이션용)
            var rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Dynamic;
            
            // BoxCollider2D 추가
            var col = go.AddComponent<BoxCollider2D>();
            col.size = Vector2.one;
            
            // ReplayUniqueId 컴포넌트 추가
            var uniqueId = go.AddComponent<ReplayUniqueId>();
            
            // Obstacle 태그 설정 (ReplayManager가 자동으로 캡처하도록)
            go.tag = "Obstacle";
            
            Debug.Log($"[ReplayTest] 생성됨: {go.name} (ID: {uniqueId.UniqueId})");
        }
    }

    private void PrintAllUniqueIds()
    {
        var allUniqueIds = FindObjectsOfType<ReplayUniqueId>();
        
        Debug.Log($"[ReplayTest] 전체 ReplayUniqueId 컴포넌트 개수: {allUniqueIds.Length}");
        
        for (int i = 0; i < allUniqueIds.Length; i++)
        {
            var uid = allUniqueIds[i];
            Debug.Log($"[ReplayTest] [{i}] {uid.name} - ID: {uid.UniqueId} - Full: {uid.GetFullIdentifier()}");
        }
    }

    [ContextMenu("Create Test Objects")]
    public void ContextCreateTestObjects()
    {
        CreateTestObjects();
    }

    [ContextMenu("Print All Unique IDs")]
    public void ContextPrintAllUniqueIds()
    {
        PrintAllUniqueIds();
    }
}
#endif