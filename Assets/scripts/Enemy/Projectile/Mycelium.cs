using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Mycelium : MonoBehaviour
{
    [Header("最大存活时间（毫秒）")]
    [SerializeField] private int maxExistTime = 5000;

    [Header("荆棘预制体引用")]
    [SerializeField] private GameObject trornPrefab;

    private Transform trornGrowingSpot;
    private bool SpotSetted = false;

    private Coroutine lifeRoutine;

    // 新增：缓存 Obstacle 层索引
    private int obstacleLayer = -1;

    private void Awake()
    {
        // 默认使用自身作为生长点（避免空引用）
        if (trornGrowingSpot == null)
            trornGrowingSpot = transform;

        // 缓存 "Obstacle" 层索引
        obstacleLayer = LayerMask.NameToLayer("Obstacle");
        if (obstacleLayer == -1)
        {
            Debug.LogWarning("[Mycelium] 未找到名为 'Obstacle' 的层，请在 Tags and Layers 中创建。");
        }
    }

    private void OnEnable()
    {
        // 若以后用对象池复用，在 OnEnable 再次启动计时
        lifeRoutine = StartCoroutine(LifeTimer());
    }

    private void OnDisable()
    {
        // 复用时清理协程
        if (lifeRoutine != null)
        {
            StopCoroutine(lifeRoutine);
            lifeRoutine = null;
        }
    }

    private IEnumerator LifeTimer()
    {
        // 如果需要忽略 Time.timeScale 可改为 WaitForSecondsRealtime
        yield return new WaitForSeconds(maxExistTime / 1000f);
        Destroy(gameObject);
    }

    private IEnumerator Seeded()
    {
        // 若没有刚体则跳过速度清零，防止空引用
        var rb = GetComponent<Rigidbody2D>();
        if (rb != null)
            rb.velocity = Vector2.zero;

        // 保护：预制体或生长点缺失时直接退出
        if (trornPrefab == null)
        {
            Debug.LogError("[Mycelium] trornPrefab 未设置，无法生成荆棘。");
            yield break;
        }
        if (trornGrowingSpot == null)
            trornGrowingSpot = transform;

        yield return new WaitForSeconds(0.1f);

        var ins = Instantiate(trornPrefab, trornGrowingSpot.position, Quaternion.identity);
        if (ins != null)
            Destroy(gameObject);
        else
        {
            yield return null;
            Debug.LogError("[Mycelium] Failed to instantiate Trorn prefab.");
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (SpotSetted) return;

        // 兜底：确保生长点非空
        if (trornGrowingSpot == null)
            trornGrowingSpot = transform;

        // 被撞到的对象及其刚体
        var otherGO = other.transform.gameObject;              // 可能是地面或其他物体
        var otherRb = otherGO.GetComponent<Rigidbody2D>();     // 可能为 null（表示无刚体 = 静态）
        bool isStatic = otherRb == null || otherRb.bodyType == RigidbodyType2D.Static;

        // 仅与静态、且处于 "Obstacle" 层的对象作为地面处理
        if (isStatic && otherGO.layer == obstacleLayer)
        {
            var selfCol = GetComponent<Collider2D>();
            if (selfCol != null)
            {
                // 使用几何距离信息获取接触点与法线
                ColliderDistance2D cd = selfCol.Distance(other);

                // 接触点：对方表面的最近点
                Vector2 contactPoint = cd.pointB;

                // 表面法线：对方外法线，注意要取 -cd.normal（cd.normal 从本碰撞体指向对方）
                Vector2 surfaceNormal = -cd.normal;

                // 写入生长点的位置与朝向（up 对齐到法线，使后续对象沿法线方向生长）
                trornGrowingSpot.position = contactPoint;
                trornGrowingSpot.up = surfaceNormal;

                // 立刻播种，在此启动协程
                if (lifeRoutine != null)
                {
                    StopCoroutine(lifeRoutine);
                    lifeRoutine = null;
                }
                StartCoroutine(Seeded());
                SpotSetted = true;
            }
        }
    }

    //[Header("测试用：速度")]
    //[SerializeField] private float Speed = 3f;

    //// 在 Awake 中初始化刚体速度(测试用)
    //private void Awake()
    //{
    //    trornGrowingSpot = transform;
    //    Vector2 vel = new Vector2(0,-1).normalized * Speed;
    //    GetComponent<Rigidbody2D>().velocity = vel;
    //}
}
