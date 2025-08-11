// TipTriggerInstaller.cs
using UnityEngine;

public static class TipTriggerInstaller
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        // Already present somewhere? then do nothing.
        if (Object.FindObjectOfType<TipTriggerEnforcer>() != null)
            return;

        var go = new GameObject("__TipTriggerEnforcer (Auto)");
        Object.DontDestroyOnLoad(go);

        var enforcer = go.AddComponent<TipTriggerEnforcer>();

        // Optional: tweak default fields via reflection if they are private serialized
        SetPrivate(enforcer, "waitFramesBeforeSearch", 2);
        SetPrivate(enforcer, "tipObjectName", "Tip");
        SetPrivate(enforcer, "targetTag", "Target");

        // If your project uses custom timings/feel:
        SetPrivate(enforcer, "pushInDistance", 0.08f);
        SetPrivate(enforcer, "pushInDuration", 0.20f);
        SetPrivate(enforcer, "wobbleAmplitude", 8f);
        SetPrivate(enforcer, "wobbleFrequency", 6f);
        SetPrivate(enforcer, "wobbleDamping", 2f);
        SetPrivate(enforcer, "wobbleDuration", 1.0f);
        SetPrivate(enforcer, "gameManagerMethodName", "StageClear");
    }

    private static void SetPrivate(object obj, string fieldName, object value)
    {
        var f = obj.GetType().GetField(fieldName,
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Public);
        if (f != null && value != null && f.FieldType.IsAssignableFrom(value.GetType()))
            f.SetValue(obj, value);
    }
}
