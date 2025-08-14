// Assets/Scripts/Replay/ReplayData.cs
using UnityEngine;

[System.Serializable]
public class ReplayData {
    public float step;          // 샘플 간격(초, unscaled)
    public int   trackCount;    // 트랙 수 (= paths.Length)
    public string[] paths;      // 각 트랙의 Transform 풀 경로
    public float[]  times;      // 프레임별 시간 (길이 = frameCount)
    public Vector3[] pos;       // 길이 = frameCount * trackCount
    public float[]  rotZ;       // 길이 = frameCount * trackCount
}