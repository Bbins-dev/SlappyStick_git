#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Prevents all GameObjects with the 'Floor' tag from being selected or moved in the Unity Editor.
/// This is applied automatically for all team members.
/// </summary>
[InitializeOnLoad]
public static class FloorEditorLock
{
    static FloorEditorLock()
    {
        EditorApplication.hierarchyChanged += LockFloorObjects;
        LockFloorObjects();
    }

    [MenuItem("Tools/Lock All Floor Objects")]
    public static void LockAllFloorsMenu()
    {
        LockFloorObjects();
        Debug.Log("All Floor objects locked (NotEditable) in the editor.");
    }

    static void LockFloorObjects()
    {
        foreach (var floor in GameObject.FindGameObjectsWithTag("Floor"))
        {
            if ((floor.hideFlags & HideFlags.NotEditable) == 0)
            {
                floor.hideFlags |= HideFlags.NotEditable;
                EditorUtility.SetDirty(floor);
            }
        }
    }

    [MenuItem("Tools/Unlock All Floor Objects")]
    public static void UnlockAllFloors()
    {
        GameObject last = null;
        foreach (var floor in GameObject.FindGameObjectsWithTag("Floor"))
        {
            if ((floor.hideFlags & HideFlags.NotEditable) != 0)
            {
                floor.hideFlags &= ~HideFlags.NotEditable;
                EditorUtility.SetDirty(floor);
                last = floor;
            }
        }
        if (last != null)
            Selection.activeObject = last; // 마지막 Floor를 강제로 선택
        Debug.Log("All Floor objects unlocked for editing.");
    }
}
#endif
