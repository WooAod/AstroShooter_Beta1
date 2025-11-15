using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Trorn : MonoBehaviour
{
    [Header("最大存活时间（毫秒）")]
    [SerializeField] private int maxExistTime = 500;
    private Coroutine lifeRoutine;

    [Header("纵向生长动画配置")]
    [SerializeField] private float growDuration = 0.2f;
    [SerializeField] private float targetScaleY = 10f;
    [SerializeField] private bool useUnscaledTimeForGrow = false;
    private Coroutine growRoutine;

    private void OnEnable()
    {
        Debug.Log($"[Trorn] OnEnable called.Pos:{transform.position}");

        // 若以后用对象池复用，在 OnEnable 再次启动计时
        lifeRoutine = StartCoroutine(LifeTimer());

        // 启动纵向缩放动画：先将 y 置 0，再在 growDuration 内插值到 targetScaleY
        var ls = transform.localScale;
        transform.localScale = new Vector3(ls.x, 0f, ls.z);
        growRoutine = StartCoroutine(GrowScaleY());
    }

    private void OnDisable()
    {
        // 复用时清理协程
        if (lifeRoutine != null)
        {
            StopCoroutine(lifeRoutine);
            lifeRoutine = null;
        }

        if (growRoutine != null)
        {
            StopCoroutine(growRoutine);
            growRoutine = null;
        }
    }

    private IEnumerator LifeTimer()
    {
        // 如果需要忽略 Time.timeScale 可改为 WaitForSecondsRealtime
        yield return new WaitForSeconds(maxExistTime / 1000f + growDuration);
        Destroy(gameObject);
    }

    // 将 localScale.y 从 0 插值到 targetScaleY，时长 growDuration
    private IEnumerator GrowScaleY()
    {
        float elapsed = 0f;
        float duration = Mathf.Max(0.0001f, growDuration);

        // 固定住 x/z，只改变 y
        float sx = transform.localScale.x;
        float sz = transform.localScale.z;

        while (elapsed < duration)
        {
            float dt = useUnscaledTimeForGrow ? Time.unscaledDeltaTime : Time.deltaTime;
            elapsed += dt;
            float t = Mathf.Clamp01(elapsed / duration);
            float y = Mathf.Lerp(0f, targetScaleY, t);
            transform.localScale = new Vector3(sx, y, sz);
            yield return null;
        }

        transform.localScale = new Vector3(sx, targetScaleY, sz);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        // 检测是否击中玩家
        if (other.CompareTag("Player"))
        {
            PlayerControl.GetHurt(1);
            Debug.Log("[TestShot] Hit Player, dealt 1 damage.");
        }
    }
}
