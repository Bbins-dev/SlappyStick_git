// Resettable2D.cs
using UnityEngine;
using System.Collections;

[DisallowMultipleComponent]
public class Resettable2D : MonoBehaviour
{
    [SerializeField] bool log;

    private Vector3 initPos;
    private Quaternion initRot;
    private Vector3 initScale;
    private Rigidbody2D rb;

    private void Awake()
    {
        initPos = transform.position;
        initRot = transform.rotation;
        initScale = transform.localScale;
        rb = GetComponent<Rigidbody2D>();
    }

    private void OnEnable()  { ResetBus.OnResetAll += ResetNow; }
    private void OnDisable() { ResetBus.OnResetAll -= ResetNow; }

    public void ResetNow()
    {
        if (log) Debug.Log($"[Resettable2D] {name} reset");
        StopAllCoroutines();
        StartCoroutine(DoReset());
    }

    private IEnumerator DoReset()
    {
        if (rb)
        {
            // 물리 일시 정지 후 위치/회전/속도 복구
            rb.simulated = false;
            rb.velocity = Vector2.zero;
            rb.angularVelocity = 0f;

            transform.position = initPos;
            transform.rotation = initRot;
            transform.localScale = initScale;

            // 한 프레임 쉬어서 충돌 상태 해제
            yield return null;

            rb.simulated = true;
            rb.WakeUp();
        }
        else
        {
            transform.position = initPos;
            transform.rotation = initRot;
            transform.localScale = initScale;
            yield break;
        }
    }
}
