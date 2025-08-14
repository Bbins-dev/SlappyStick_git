// Assets/Scripts/Replay/ReplayOptIn.cs
using UnityEngine;

[DisallowMultipleComponent]
public class ReplayOptIn : MonoBehaviour {
    [Tooltip("이 오브젝트의 모든 자식까지 함께 기록")]
    public bool includeChildren = false;
}
