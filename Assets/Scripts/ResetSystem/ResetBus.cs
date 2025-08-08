// ResetBus.cs
using System;
public static class ResetBus
{
    public static event Action OnResetAll;
    public static void Raise() => OnResetAll?.Invoke();
}
