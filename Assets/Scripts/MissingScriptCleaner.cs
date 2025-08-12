// Assets/Editor/MissingScriptsCleaner.cs
using UnityEditor;
using UnityEngine;

public static class MissingScriptsCleaner
{
    [MenuItem("StickIt/Utilities/Clean Missing Scripts In Scene")]
    public static void CleanInScene()
    {
        int removed = 0;
        var all = GameObject.FindObjectsOfType<GameObject>(true);
        foreach (var go in all)
        {
            var so = new SerializedObject(go);
            var compProp = so.FindProperty("m_Component");
            int shift = 0;

            var comps = go.GetComponents<Component>();
            for (int i = 0; i < comps.Length; i++)
            {
                if (comps[i] == null)
                {
                    compProp.DeleteArrayElementAtIndex(i - shift);
                    removed++;
                    shift++;
                }
            }
            if (shift > 0) so.ApplyModifiedProperties();
        }
        Debug.Log($"[MissingScriptsCleaner] Removed {removed} missing script(s) in scene.");
    }
}
