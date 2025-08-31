// Assets/Scripts/Replay/ReplayData.cs
using UnityEngine;

[System.Serializable]
public class ReplayData {
    public float step;          // 샘플 간격(초, unscaled)
    public int   trackCount;    // 트랙 수 (= identifiers.Length)
    public string[] identifiers; // 각 트랙의 고유 식별자 (경로#ID 형태)
    public float[]  times;      // 프레임별 시간 (길이 = frameCount)
    public Vector3[] pos;       // 길이 = frameCount * trackCount
    public float[]  rotZ;       // 길이 = frameCount * trackCount
    
    
    // 하위 호환성을 위한 레거시 필드 (v2 이하)
    [System.Obsolete("Use identifiers instead")]
    public string[] paths;      // 각 트랙의 Transform 풀 경로 (하위 호환용)
}

