using UnityEngine;
using System.Collections;

public class Dash : MonoBehaviour
{
    [Header("冲刺设置")]
    public float dashDistance = 5f;          // 冲刺距离
    public float dashDuration = 0.2f;        // 冲刺持续时间
    public float cooldown = 0.5f;            // 冷却时间
    public LayerMask obstacleLayers;         // 障碍物层级
    public KeyCode launchKey = KeyCode.Space;// 冲刺按键

    [Header("无敌效果")]
    public Material invincibleMaterial;      // 无敌时的材质（虚化效果）
    private Material originalMaterial;       // 原始材质

    // 新增：冲刺碰撞鲁棒性
    [Header("冲刺碰撞稳健性")]
    [SerializeField] private float dashSkin = 0.05f;     // 目标点与障碍的最小安全距离
    [SerializeField] private bool useRbMovePosition = true; // 用 MovePosition 驱动位移
    private readonly RaycastHit2D[] _castHits = new RaycastHit2D[8];
    private ContactFilter2D _dashFilter;

    private bool isDashing = false;          // 是否正在冲刺
    private bool isCooldown = false;         // 是否在冷却中
    private float dashTimer = 0f;            // 冲刺计时器
    private SpriteRenderer playerRenderer;   // 玩家精灵渲染组件
    private Collider2D playerCollider;       // 玩家2D碰撞体
    private Vector2 lastMoveDirection;       // 记录最后移动方向
    private Rigidbody2D rb;                  // 2D刚体组件
    void Start()
    {
       
        if (!LevelControl.IsLevelCompleted("AsteroidBelt"))
        {
            // 前置关卡完成，设置为可交互
            this.enabled = false;
        }
        // 获取组件引用
        playerRenderer = GetComponent<SpriteRenderer>();
        playerCollider = GetComponent<Collider2D>();
        rb = GetComponent<Rigidbody2D>();

        // 保存原始材质
        if (playerRenderer != null)
        {
            originalMaterial = playerRenderer.material;
        }

        // 如果没有指定无敌材质，创建一个半透明的默认材质
        if (invincibleMaterial == null)
        {
            CreateDefaultInvincibleMaterial();
        }

        // 初始化移动方向为右方
        lastMoveDirection = Vector2.right;

        // 初始化冲刺接触过滤器（与 obstacleLayers 一致，且忽略触发器）
        _dashFilter = new ContactFilter2D
        {
            useLayerMask = true,
            layerMask = obstacleLayers,
            useTriggers = false
        };
    }

    void Update()
    {
        // 更新移动方向
        UpdateMoveDirection();

        // 检测空格键按下且不在冷却中
        if (Input.GetKeyDown(launchKey) && !isDashing && !isCooldown)
        {
            StartDash();
        }

        // 冲刺状态更新
        if (isDashing)
        {
            UpdateDash();
        }
    }

    void UpdateMoveDirection()
    {
        // 获取输入方向
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");

        // 如果有输入，更新最后移动方向
        Vector2 inputDirection = new Vector2(horizontal, vertical);
        if (inputDirection.magnitude > 0.1f)
        {
            lastMoveDirection = inputDirection.normalized;
        }

        // 如果没有输入但有速度，使用速度方向
        else if (rb != null && rb.velocity.magnitude > 0.1f)
        {
            lastMoveDirection = rb.velocity.normalized;
        }
    }

    // 新增：获取指向鼠标的单位方向
    private Vector2 GetMouseAimDirection()
    {
        Camera cam = Camera.main;
        if (cam == null) return Vector2.zero;

        Vector3 mouseWorld = cam.ScreenToWorldPoint(Input.mousePosition);
        Vector2 dir = (Vector2)mouseWorld - (Vector2)transform.position;
        if (dir.sqrMagnitude < 0.0001f) return Vector2.zero;
        return dir.normalized;
    }

    void StartDash()
    {
        // 改为优先朝鼠标方向冲刺；若不可用则回退到上次移动方向或向右
        Vector2 mouseDir = GetMouseAimDirection();
        Vector2 dashDir = mouseDir != Vector2.zero
            ? mouseDir
            : (lastMoveDirection != Vector2.zero ? lastMoveDirection : Vector2.right);

        // 开始冲刺协程
        StartCoroutine(PerformDash(dashDir));
    }

    IEnumerator PerformDash(Vector2 direction)
    {
        isDashing = true;
        isCooldown = true;

        // 保存当前速度（如果有刚体）
        Vector2 originalVelocity = Vector2.zero;
        if (rb != null)
        {
            originalVelocity = rb.velocity;
            rb.velocity = Vector2.zero; // 停止当前移动
        }

        // 启用无敌状态
        if (LevelControl.IsLevelCompleted("LightningPlanet"))
        {
            // 前置关卡完成，设置为可交互
            SetInvincible(true);
        }
        
        // 1) 冲刺前去穿透：若与障碍重叠/贴面，先把自己沿法线推出“皮肤”距离
        ResolveInitialOverlap();

        Vector2 startPosition = rb ? rb.position : (Vector2)transform.position;
        Vector2 targetPosition = CalculateDashTargetPositionSafe(startPosition, direction);
        float elapsedTime = 0f;

        // 2) 冲刺移动（沿直线插值）
        while (elapsedTime < dashDuration)
        {
            elapsedTime += Time.deltaTime;
            float progress = dashDuration > 0f ? Mathf.Clamp01(elapsedTime / dashDuration) : 1f;
            Vector2 nextPos = Vector2.Lerp(startPosition, targetPosition, progress);

            if (rb != null && useRbMovePosition)
                rb.MovePosition(nextPos);
            else
                transform.position = nextPos;

            yield return null;
        }

        // 确保最终位置准确（仍保持皮肤距离外）
        if (rb != null && useRbMovePosition)
            rb.MovePosition(targetPosition);
        else
            transform.position = targetPosition;

        // 结束冲刺
        isDashing = false;
        SetInvincible(false);

        // 恢复速度（如果有刚体）
        if (rb != null)
        {
            rb.velocity = originalVelocity;
        }

        // 开始冷却
        StartCoroutine(StartCooldown());
    }

    // 用刚体/碰撞体 Cast 计算最远可达位置（与障碍留出 dashSkin）
    private Vector2 CalculateDashTargetPositionSafe(Vector2 startPos, Vector2 direction)
    {
        Vector2 dir = direction.sqrMagnitude > 0f ? direction.normalized : Vector2.right;
        float maxDist = Mathf.Max(0f, dashDistance);

        // 优先使用自身形状 Cast，防止仅用 Ray 导致起点在墙内的边界问题
        if (playerCollider != null)
        {
            int hitCount = playerCollider.Cast(dir, _dashFilter, _castHits, maxDist + dashSkin);
            if (hitCount == 0)
            {
                return startPos + dir * maxDist;
            }

            float allow = maxDist;
            for (int i = 0; i < hitCount; i++)
            {
                var h = _castHits[i];
                // 预留皮肤距离
                float d = h.distance - dashSkin;
                if (d < allow)
                    allow = Mathf.Max(0f, d);
            }
            return startPos + dir * allow;
        }
        else
        {
            // 回退：使用 Raycast（与原实现等价但预留皮肤）
            RaycastHit2D hit = Physics2D.Raycast(startPos, dir, maxDist + dashSkin, obstacleLayers);
            if (hit.collider != null)
            {
                float allow = Mathf.Max(0f, hit.distance - dashSkin);
                return startPos + dir * allow;
            }
            return startPos + dir * maxDist;
        }
    }

    // 若起点与障碍重叠/贴面，按法线推出皮肤距离，避免“从墙内开始冲刺”
    private void ResolveInitialOverlap()
    {
        if (playerCollider == null) return;

        // 收集所有重叠碰撞体
        var results = new Collider2D[8];
        int count = playerCollider.OverlapCollider(_dashFilter, results);
        if (count <= 0) return;

        Vector2 totalPush = Vector2.zero;
        for (int i = 0; i < count; i++)
        {
            var other = results[i];
            if (other == null) continue;

            ColliderDistance2D dist = playerCollider.Distance(other);
            if (dist.isOverlapped)
            {
                // normal 指向从本 Collider 指向对方的方向
                // 将自身沿 -normal 推出 |distance| + skin
                float pushLen = Mathf.Abs(dist.distance) + dashSkin;
                totalPush += (-dist.normal) * pushLen;
            }
            else if (dist.distance < dashSkin)
            {
                // 非重叠但贴面过近，保持皮肤距离
                float need = dashSkin - dist.distance;
                totalPush += (-dist.normal) * need;
            }
        }

        if (totalPush != Vector2.zero)
        {
            Vector2 newPos = (rb ? rb.position : (Vector2)transform.position) + totalPush;
            if (rb != null && useRbMovePosition)
                rb.MovePosition(newPos);
            else
                transform.position = newPos;
        }
    }

    void SetInvincible(bool invincible)
    {
        // 视觉效果
        if (playerRenderer != null && invincibleMaterial != null && originalMaterial != null)
        {
            playerRenderer.material = invincible ? invincibleMaterial : originalMaterial;
        }

        // 使用标签标记无敌状态，便于其他脚本检测
        gameObject.tag = invincible ? "Invincible" : "Player";
    }

    IEnumerator StartCooldown()
    {
        yield return new WaitForSeconds(cooldown);
        isCooldown = false;
    }

    void UpdateDash()
    {
        dashTimer += Time.deltaTime;
        if (dashTimer >= dashDuration)
        {
            dashTimer = 0f;
        }
    }

    void CreateDefaultInvincibleMaterial()
    {
        // 创建一个简单的半透明材质用于无敌效果
        invincibleMaterial = new Material(Shader.Find("Sprites/Default"));
        invincibleMaterial.color = new Color(1, 1, 1, 0.5f); // 半透明
    }

    // 可视化调试（在Scene视图中显示冲刺方向）
    void OnDrawGizmosSelected()
    {
        // 显示移动方向
        Gizmos.color = Color.blue;
        Vector3 direction3D = new Vector3(lastMoveDirection.x, lastMoveDirection.y, 0);
        Gizmos.DrawRay(transform.position, direction3D * 2f);

        // 显示冲刺状态
        if (Application.isPlaying && isDashing)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, 0.5f);
        }
        else if (!isCooldown)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, 0.3f);
        }
        else
        {
            Gizmos.color = Color.gray;
            Gizmos.DrawWireSphere(transform.position, 0.3f);
        }
    }

    // 公共方法，用于UI显示冷却状态
    public bool IsOnCooldown()
    {
        return isCooldown;
    }

    public float GetCooldownProgress()
    {
        return isCooldown ? 0f : 1f;
    }
}