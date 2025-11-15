using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Movement : MonoBehaviour
{
    [Header("基础移动参数")]
    [SerializeField] private float speed = 5f;

    [Header("与 Dash 统一的障碍层检测")]
    [SerializeField] private LayerMask obstacleLayers;   // 与 Dash.obstacleLayers 使用同一 Layer
    [SerializeField] private float skin = 0.03f;         // 与障碍保持的最小空隙
    [SerializeField] private bool slideAlongWalls = true;// 碰墙时是否允许沿另一轴滑动

    private Rigidbody2D rb;
    private RaycastHit2D[] hitBuffer = new RaycastHit2D[8];
    private ContactFilter2D contactFilter;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

        // 若本脚本与 Dash 同挂在一个对象上，则自动同步层设置
        var dash = GetComponent<Dash>();
        if (dash != null)
        {
            obstacleLayers = dash.obstacleLayers;
        }

        contactFilter = new ContactFilter2D
        {
            useLayerMask = true,
            layerMask = obstacleLayers,
            useTriggers = false
        };
    }

    private void Update()
    {
        // 读取输入（与原实现保持键位）
        float moveHorizontal = 0f;
        float moveVertical = 0f;
        if (Input.GetKey(KeyCode.A)) moveHorizontal -= 1f;
        if (Input.GetKey(KeyCode.D)) moveHorizontal += 1f;
        if (Input.GetKey(KeyCode.W)) moveVertical += 1f;
        if (Input.GetKey(KeyCode.S)) moveVertical -= 1f;

        Vector2 input = new Vector2(moveHorizontal, moveVertical);
        if (input.sqrMagnitude > 1f) input.Normalize();

        Vector2 desiredVelocity = input * speed;

        // 基于与 Dash 相同的层 (obstacleLayers) 做位移截断
        MoveWithLayerCollision(desiredVelocity);
    }

    /// <summary>
    /// 进行移动并基于 obstacleLayers 预判碰撞：与 Dash 相同层，避免穿过阻挡。
    /// 使用 Rigidbody2D.Cast 做预测截断，不依赖改变刚体类型。
    /// </summary>
    private void MoveWithLayerCollision(Vector2 desiredVelocity)
    {
        if (rb == null)
            return;

        if (desiredVelocity == Vector2.zero)
        {
            rb.velocity = Vector2.zero;
            return;
        }

        Vector2 pos = rb.position;
        Vector2 frameDelta = desiredVelocity * Time.deltaTime;

        if (slideAlongWalls)
        {
            // 允许沿墙滑动：先 X 后 Y
            TryMoveAxis(ref pos, new Vector2(frameDelta.x, 0f));
            TryMoveAxis(ref pos, new Vector2(0f, frameDelta.y));
        }
        else
        {
            // 一次性整体移动（可能在斜角被完全阻挡）
            frameDelta = AdjustDeltaByCast(pos, frameDelta);
            pos += frameDelta;
        }

        rb.MovePosition(pos);
        // 给其他系统（如 Dash 读取方向）保留速度信息
        rb.velocity = desiredVelocity;
    }

    private void TryMoveAxis(ref Vector2 currentPos, Vector2 axisDelta)
    {
        if (axisDelta == Vector2.zero) return;
        Vector2 adjusted = AdjustDeltaByCast(currentPos, axisDelta);
        currentPos += adjusted;
    }

    /// <summary>
    /// 使用刚体的 Cast 预测沿 delta 方向的可行距离，遇到 obstacleLayers 截断。
    /// </summary>
    private Vector2 AdjustDeltaByCast(Vector2 startPos, Vector2 delta)
    {
        float distance = delta.magnitude;
        if (distance <= 0f) return Vector2.zero;

        // 方向归一
        Vector2 dir = delta / distance;

        // Cast：将刚体的形状沿 dir 投射，得到最短可移动距离
        int hitCount = rb.Cast(dir, contactFilter, hitBuffer, distance + skin);
        if (hitCount == 0)
            return delta; // 无阻挡

        float allowed = distance;
        for (int i = 0; i < hitCount; i++)
        {
            var hit = hitBuffer[i];

            // 若需 Tag 进一步过滤可在此加：
            // if (!hit.collider.CompareTag("Block")) continue;

            float d = hit.distance - skin;
            if (d < allowed)
                allowed = Mathf.Max(0f, d);
        }

        return dir * allowed;
    }
}
