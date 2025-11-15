using System.Collections;
using UnityEngine;

/// <summary>
/// 半径从 minRadiusPixels 扩张到 targetRadiusPixels，再从最大半径收缩到 minRadiusPixels；围绕“轴”做圆周旋转。
/// 轴为父节点当前位置（若无父节点则保持上次轴位置）。
/// </summary>
public class RotateLeave : MonoBehaviour
{
    [Header("轨迹参数")]
    [SerializeField] private float expandDuration = 2f;          // 扩大半径用时
    [SerializeField] private float shrinkDuration = 2f;          // 缩小半径用时
    [SerializeField] private float targetRadiusPixels = 800f;    // 目标半径（像素）
    [SerializeField] private float minRadiusPixels = 10f;        // 起止半径（像素），默认10px
    [SerializeField] private float angularSpeedDeg = 360f;       // 角速度（度/秒）
    [SerializeField] private bool randomStartAngle = true;       // 随机初始角
    [SerializeField] private bool randomDirection = true;        // 随机顺/逆时针
    [SerializeField] private bool destroyOnFinish = true;        // 完成后销毁

    [Header("固定初始角（可选）")]
    [Tooltip("为 true 时使用 fixedStartAngleDeg 作为初始角，覆盖随机初始角设置。")]
    [SerializeField] private bool useFixedStartAngle = false;
    [Tooltip("固定初始角（度，0~360）")]
    [Range(0f, 360f)]
    [SerializeField] private float fixedStartAngleDeg = 0f;

    private float pixelToWorld;          // 屏幕像素到世界单位的换算
    private float dirSign = 1f;          // 顺/逆时针标记（+1=逆时针，-1=顺时针）
    private float angleDeg;              // 当前角度（度）
    private float planeZ;                // 固定 z 平面

    // 轴位置（父物体的世界位置，按帧更新）
    private Vector3 axisWorldPos;

    private Coroutine runCo;

    private void OnEnable()
    {
        Initialize();
        runCo = StartCoroutine(RotateRoutine());
    }

    private void OnDisable()
    {
        if (runCo != null)
        {
            StopCoroutine(runCo);
            runCo = null;
        }
    }

    /// <summary>
    /// 运行时设置固定初始角（度）。会立即生效并更新当前位置。
    /// </summary>
    public void SetStartAngleDeg(float deg)
    {
        useFixedStartAngle = true;
        fixedStartAngleDeg = deg;
        angleDeg = Mathf.Repeat(deg, 360f);

        // 若已在运行中，立即按当前半径刷新位置以体现新角度
        UpdateAxisFromParent();
        float currentRadius = Vector3.Distance(transform.position, axisWorldPos);
        UpdatePosition(currentRadius);
    }

    /// <summary>
    /// 运行时设置旋转方向。true=顺时针，false=逆时针；将覆盖随机方向。
    /// </summary>
    public void SetClockwise(bool clockwise)
    {
        randomDirection = false;
        dirSign = clockwise ? -1f : 1f;
    }

    private void Initialize()
    {
        planeZ = transform.position.z;

        // 计算像素到世界单位的换算（按当前相机+深度）
        var cam = Camera.main;
        if (cam != null)
        {
            float camToPlaneDist = Mathf.Abs(planeZ - cam.transform.position.z);
            Vector3 w0 = cam.ScreenToWorldPoint(new Vector3(0f, 0f, camToPlaneDist));
            Vector3 w1 = cam.ScreenToWorldPoint(new Vector3(1f, 0f, camToPlaneDist));
            pixelToWorld = Mathf.Abs(w1.x - w0.x);
        }
        else
        {
            pixelToWorld = 1f / 100f; // 回退：100px = 1u
            Debug.LogWarning("[RotateLeave] 未找到主相机，使用默认像素比 (100px=1u)。");
        }

        // 初始角优先级：固定初始角 > 随机初始角 > 默认 0°
        if (useFixedStartAngle)
        {
            angleDeg = Mathf.Repeat(fixedStartAngleDeg, 360f);
        }
        else if (randomStartAngle)
        {
            angleDeg = Random.Range(0f, 360f);
        }
        // else 保持默认 0°

        if (randomDirection)
            dirSign = Random.value < 0.5f ? -1f : 1f;

        // 初始位置：半径=minRadiusPixels，位于轴上对应方向
        UpdateAxisFromParent();
        float startRadius = Mathf.Max(0f, minRadiusPixels) * pixelToWorld;
        UpdatePosition(startRadius);
    }

    private IEnumerator RotateRoutine()
    {
        float minRadiusWorld = Mathf.Max(0f, minRadiusPixels) * pixelToWorld;
        float maxRadiusWorld = Mathf.Max(minRadiusWorld, targetRadiusPixels * pixelToWorld);

        // Phase 1：半径 min -> max
        float t = 0f;
        while (t < expandDuration)
        {
            t += Time.deltaTime;
            float k = expandDuration > 0f ? Mathf.Clamp01(t / expandDuration) : 1f;
            float radius = Mathf.Lerp(minRadiusWorld, maxRadiusWorld, k);
            angleDeg += angularSpeedDeg * dirSign * Time.deltaTime;

            UpdateAxisFromParent();
            UpdatePosition(radius);
            yield return null;
        }

        // Phase 2：半径 max -> min
        t = 0f;
        while (t < shrinkDuration)
        {
            t += Time.deltaTime;
            float k = shrinkDuration > 0f ? Mathf.Clamp01(t / shrinkDuration) : 1f;
            float radius = Mathf.Lerp(maxRadiusWorld, minRadiusWorld, k);
            angleDeg += angularSpeedDeg * dirSign * Time.deltaTime;

            UpdateAxisFromParent();
            UpdatePosition(radius);
            yield return null;
        }

        // 结束时确保回到最小半径位置
        UpdatePosition(minRadiusWorld);

        if (destroyOnFinish)
            Destroy(gameObject);
    }

    /// <summary>
    /// 根据当前半径与角度，更新物体位置
    /// </summary>
    private void UpdatePosition(float radius)
    {
        float rad = angleDeg * Mathf.Deg2Rad;
        Vector3 offset = new Vector3(Mathf.Cos(rad) * radius, Mathf.Sin(rad) * radius, 0f);
        transform.position = axisWorldPos + offset;
    }

    /// <summary>
    /// 轴位置：使用父节点的当前世界位置（若无父节点则保持上次记录的轴）
    /// </summary>
    private void UpdateAxisFromParent()
    {
        if (transform.parent != null)
        {
            axisWorldPos = transform.parent.position;
            axisWorldPos.z = planeZ;
        }
        // parent == null 时，保留上次 axisWorldPos
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        // 检测是否击中玩家
        if (other.CompareTag("Player"))
        {
            PlayerControl.GetHurt(1);
            Debug.Log("[RotateLeave] Hit Player, dealt 1 damage.");
        }
    }
}