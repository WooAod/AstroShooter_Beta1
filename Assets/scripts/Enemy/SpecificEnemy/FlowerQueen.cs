using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FlowerQueen : EnemySet
{
    [Header("菌丝预制体")]
    [SerializeField] private GameObject myceliumPrefab;

    [Header("荆棘预制体")]
    [SerializeField] private GameObject trornPrefab;

    [Header("叶子预制体")]
    [SerializeField] private GameObject rotateLeavePrefab;
    [SerializeField] private GameObject lineLeavePrefab;

    [Header("Idle 接近参数")]
    [SerializeField] private float idleApproachSpeed = 2.0f;        // Idle 时的靠近速度（单位/秒）
    [SerializeField] private float idleApproachStopDistance = 6.0f; // 若距离小于等于该值则不再靠近

    [Header("RotateLeaves 追击参数")]
    [SerializeField] private float rotateLeavesApproachSpeed = 2f;  // 释放 RotateLeaves 时持续靠近玩家的速度

    [Header("RotateLeaves 旋转流程参数")]
    [SerializeField] private float rotateLeavesSpinDuration = 0.4f;      // 技能开始自转一圈用时
    [SerializeField] private float rotateLeavesReturnDuration = 0.1f;    // 自转后回归初始朝向用时
    [SerializeField] private float rotateLeavesSpinRadiusPixels = 100f;  // 自转半径（像素），默认 100px

    [Header("LineLeaves 子弹参数")]
    [SerializeField] private float lineLeaveSpeed = 5f;             // 直线叶子子弹速度

    // 控制本技能协程，避免并发
    private Coroutine myceliumSeqCo;
    private Coroutine trornSeqCo;

    // RotateLeaves 技能体内序列是否仍在进行（自转/回位/生成叶子）
    private bool rotateLeavesSequenceRunning = false;

    // 开场动画控制
    private Coroutine openingCo;
    private bool isOpening = false;
    private string originalTag = null;
    private bool invincibleTagApplied = false;

    protected override void StartApply()
    {
        // 注册技能
        SkillList.Add(new EnemySkill(
            name: "SpawnMycelium",
            action: SpawnMycelium,
            weight: 5,
            preDelay: 0.2f,
            postDelay: 0.6f,
            cooldown: 0.1f));
        SkillList.Add(new EnemySkill(
            name: "SpawnTrorn",
            action: SpawnTrorn,
            weight: 5,
            preDelay: 0.2f,
            postDelay: 0.6f,
            cooldown: 0.1f));
        SkillList.Add(new EnemySkill(
            name: "RotateLeaves",
            action: RotateLeaves,
            weight: 5,
            preDelay: 0.1f,
            postDelay: 0.3f,
            cooldown: 2f));
    }

    protected override void Start()
    {
        base.Start();
        // 进入开场动画：1.5 秒内以 2u/s 向上移动，并将 Tag 设为 Invincible
        if (openingCo != null)
        {
            StopCoroutine(openingCo);
            openingCo = null;
        }
        openingCo = StartCoroutine(OpeningIntroRoutine(1.5f, 2f));
    }

    private void OnDisable()
    {
        // 清理开场动画与 Tag
        if (openingCo != null)
        {
            StopCoroutine(openingCo);
            openingCo = null;
        }
        isOpening = false;
        if (invincibleTagApplied && originalTag != null)
        {
            // 尝试还原 Tag
            try { gameObject.tag = originalTag; }
            catch { /* 忽略还原失败 */ }
            invincibleTagApplied = false;
        }
    }

    private IEnumerator OpeningIntroRoutine(float duration, float speedUp)
    {
        isOpening = true;
        originalTag = gameObject.tag;

        // 设置 Invincible Tag（若项目未创建该 Tag，将给出警告并忽略）
        try
        {
            gameObject.tag = "Invincible";
            invincibleTagApplied = true;
        }
        catch (UnityException)
        {
            Debug.LogWarning("[FlowerQueen] 未找到 Tag 'Invincible'，请在 Tags and Layers 中创建该标签。");
            invincibleTagApplied = false;
        }

        var rb = GetComponent<Rigidbody2D>();
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            if (rb != null)
            {
                rb.velocity = new Vector2(0f, speedUp);
            }
            else
            {
                transform.position += Vector3.up * speedUp * Time.deltaTime;
            }
            yield return null;
        }

        // 停止上移并还原 Tag
        if (rb != null) rb.velocity = Vector2.zero;
        if (invincibleTagApplied && originalTag != null)
        {
            try { gameObject.tag = originalTag; }
            catch { /* 忽略还原失败 */ }
            invincibleTagApplied = false;
        }

        isOpening = false;
        openingCo = null;

        StartAILoop();
    }

    private void SpawnMycelium()
    {
        // 步骤：0.6s 移动到屏幕中心 → 静止 0.2s → 发射菌丝（vy 固定为 -4f 命中底边4个随机点）
        if (myceliumSeqCo != null)
        {
            StopCoroutine(myceliumSeqCo);
            myceliumSeqCo = null;
        }
        myceliumSeqCo = StartCoroutine(SpawnMyceliumSequence());
    }

    private IEnumerator SpawnMyceliumSequence()
    {
        if (myceliumPrefab == null)
        {
            Debug.LogWarning("[FlowerQueen] myceliumPrefab 未设置，无法发射。");
            yield break;
        }

        var cam = Camera.main;
        if (cam == null)
        {
            Debug.LogWarning("[FlowerQueen] 未找到主摄像机，无法计算屏幕信息。");
            yield break;
        }

        // 1) 0.6s 平滑移动到屏幕正中心
        float planeZ = transform.position.z;
        float camToPlaneDist = Mathf.Abs(planeZ - cam.transform.position.z);
        Vector3 centerWorld = cam.ScreenToWorldPoint(new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, camToPlaneDist));
        centerWorld.z = transform.position.z;

        Vector3 startPos = transform.position;
        const float moveDuration = 0.6f;
        float el = 0f;
        while (el < moveDuration)
        {
            el += Time.deltaTime;
            float t = Mathf.Clamp01(el / moveDuration);
            transform.position = Vector3.Lerp(startPos, centerWorld, t);
            yield return null;
        }
        transform.position = centerWorld;

        // 让可能存在的刚体静止
        var selfRb = GetComponent<Rigidbody2D>();
        if (selfRb != null) selfRb.velocity = Vector2.zero;

        // 2) 静止 0.2s
        yield return new WaitForSeconds(0.2f);

        // 3) 发射菌丝：y 轴速度固定为 -4f，命中底边随机 4 个点（像素间距 ≥ 200）
        float bottomY = cam.ScreenToWorldPoint(new Vector3(0f, 0f, camToPlaneDist)).y;

        const float requiredMinSpacingPx = 200f;
        const int targetCount = 4;
        const int maxAttempts = 200;
        float marginPx = 20f;
        float minX = marginPx;
        float maxX = Mathf.Max(minX + 1f, Screen.width - marginPx);

        List<float> chosenXs = new List<float>(targetCount);
        int attempts = 0;
        while (chosenXs.Count < targetCount && attempts < maxAttempts)
        {
            attempts++;
            float x = Random.Range(minX, maxX);
            bool ok = true;
            for (int i = 0; i < chosenXs.Count; i++)
            {
                if (Mathf.Abs(chosenXs[i] - x) < requiredMinSpacingPx)
                {
                    ok = false;
                    break;
                }
            }
            if (ok) chosenXs.Add(x);
        }

        if (chosenXs.Count < targetCount)
        {
            chosenXs.Clear();
            float step = (maxX - minX) / (targetCount + 1);
            for (int i = 1; i <= targetCount; i++)
                chosenXs.Add(minX + step * i);
            Debug.LogWarning("[FlowerQueen] 随机点位不足，使用等距点位作为退化处理。");
        }

        Vector3 shootPos = transform.position;
        const float vy = -4f;
        float distanceY = shootPos.y - bottomY;
        float tToBottom = distanceY > 0f ? distanceY / Mathf.Abs(vy) : 0.0001f;

        for (int i = 0; i < chosenXs.Count; i++)
        {
            float screenX = chosenXs[i];
            Vector3 worldTarget = cam.ScreenToWorldPoint(new Vector3(screenX, 0f, camToPlaneDist));
            float targetX = worldTarget.x;

            float vx = (targetX - shootPos.x) / tToBottom;

            GameObject go = Instantiate(myceliumPrefab, shootPos, Quaternion.identity);

            var rb = go.GetComponent<Rigidbody2D>();
            if (rb == null)
            {
                rb = go.AddComponent<Rigidbody2D>();
                rb.gravityScale = 0f;
                rb.angularDrag = 0f;
                rb.drag = 0f;
                rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            }
            else
            {
                rb.gravityScale = 0f;
            }

            Vector2 vel = new Vector2(vx, vy);
            rb.velocity = vel;

            if (vel.sqrMagnitude > 0.000001f)
                go.transform.right = vel.normalized;
        }

        myceliumSeqCo = null;
    }

    private void SpawnTrorn()
    {
        // 步骤：0.3s 移至玩家位置 → 0.5s 随机向左/右水平移动 400 像素（不出屏）→ 等待 0.2s → 生成棘刺
        if (trornSeqCo != null)
        {
            StopCoroutine(trornSeqCo);
            trornSeqCo = null;
        }
        trornSeqCo = StartCoroutine(SpawnTrornSequence());
    }

    private IEnumerator SpawnTrornSequence()
    {
        if (trornPrefab == null)
        {
            Debug.LogWarning("[FlowerQueen] trornPrefab 未设置，无法生成荆棘。");
            yield break;
        }

        var cam = Camera.main;
        if (cam == null)
        {
            Debug.LogWarning("[FlowerQueen] 未找到主摄像机，无法执行移动与边界检测。");
            yield break;
        }

        // 查找玩家
        Transform player = null;
        var playerGO = GameObject.FindGameObjectWithTag("Player");
        if (playerGO != null) player = playerGO.transform;

        float planeZ = transform.position.z;
        float camToPlaneDist = Mathf.Abs(planeZ - cam.transform.position.z);

        // 屏幕边界（世界坐标，按当前深度）
        Vector3 worldLeft = cam.ScreenToWorldPoint(new Vector3(0f, 0f, camToPlaneDist));
        Vector3 worldRight = cam.ScreenToWorldPoint(new Vector3(Screen.width, 0f, camToPlaneDist));
        float leftX = Mathf.Min(worldLeft.x, worldRight.x);
        float rightX = Mathf.Max(worldLeft.x, worldRight.x);
        float clampMargin = 0.05f; // 给边缘一点余量

        // 可能存在的刚体停下
        var selfRb = GetComponent<Rigidbody2D>();
        if (selfRb != null) selfRb.velocity = Vector2.zero;

        // 1) 0.3s 移动到玩家位置（若未找到玩家，则保持在原地）
        Vector3 startPos = transform.position;
        Vector3 playerPos = startPos;
        if (player != null)
        {
            playerPos = player.position;
            playerPos.z = startPos.z;

            // 夹到屏幕内，避免中心点越界
            playerPos.x = Mathf.Clamp(playerPos.x, leftX + clampMargin, rightX - clampMargin);
        }

        const float toPlayerDuration = 0.3f;
        float el = 0f;
        while (el < toPlayerDuration)
        {
            el += Time.deltaTime;
            float t = Mathf.Clamp01(el / toPlayerDuration);
            transform.position = Vector3.Lerp(startPos, playerPos, t);
            yield return null;
        }
        transform.position = playerPos;
        if (selfRb != null) selfRb.velocity = Vector2.zero;

        // 2) 0.7s 随机向左或右移动 600 像素（水平）
        // 每像素对应的世界单位（X 方向）
        float pxToWorldX = cam.ScreenToWorldPoint(new Vector3(1f, 0f, camToPlaneDist)).x - cam.ScreenToWorldPoint(new Vector3(0f, 0f, camToPlaneDist)).x;
        float moveWorld = pxToWorldX * 600f;
        int dirSign = Random.value < 0.5f ? -1 : 1;

        Vector3 midStart = transform.position;
        float targetX = midStart.x + dirSign * moveWorld;
        // 确保不出屏：夹到左右边界内
        targetX = Mathf.Clamp(targetX, leftX + clampMargin, rightX - clampMargin);
        Vector3 midTarget = new Vector3(targetX, midStart.y, midStart.z);

        const float sideMoveDuration = 0.7f;
        el = 0f;
        while (el < sideMoveDuration)
        {
            el += Time.deltaTime;
            float t = Mathf.Clamp01(el / sideMoveDuration);
            transform.position = Vector3.Lerp(midStart, midTarget, t);
            yield return null;
        }
        transform.position = midTarget;
        if (selfRb != null) selfRb.velocity = Vector2.zero;

        // 3) 等待 0.3s
        yield return new WaitForSeconds(0.3f);

        // 4) 原地生成 6 根荆棘，方向：右、右上30°、左上30°、左、左下30°、右下30°
        Vector3 spawnPos = transform.position;
        float[] angles = { 0f, 30f, 150f, 180f, 210f, -30f };
        for (int i = 0; i < angles.Length; i++)
        {
            float a = angles[i];
            float rad = a * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
            GameObject go = Instantiate(trornPrefab, spawnPos, Quaternion.identity);
            go.transform.up = dir;
        }

        trornSeqCo = null;
    }

    private void RotateLeaves()
    {
        // 启动新的技能内部序列：自转 → 回位 → 生成叶子 → 停顿 → 发射5枚lineLeave
        if (rotateLeavePrefab == null)
        {
            Debug.LogWarning("[FlowerQueen] rotateLeavePrefab 未设置，无法生成叶子。");
            return;
        }
        if (!rotateLeavesSequenceRunning)
        {
            StartCoroutine(RotateLeavesSequence());
        }
    }

    private IEnumerator RotateLeavesSequence()
    {
        rotateLeavesSequenceRunning = true;

        // 记录初始状态
        Vector3 axisCenter = transform.position;          // 旋转轴（以当前世界位置为圆心）
        float startZ = transform.eulerAngles.z;

        // 方向（顺时针用负角，逆时针用正角）
        bool clockwise = Random.value < 0.5f;
        float spinSign = clockwise ? -1f : 1f;

        // 像素到世界单位换算（仅需 X 像素）
        float pixelToWorldX = 1f / 100f; // 回退值
        var cam = Camera.main;
        float planeZ = axisCenter.z;
        if (cam != null)
        {
            float camToPlaneDist = Mathf.Abs(planeZ - cam.transform.position.z);
            Vector3 w0 = cam.ScreenToWorldPoint(new Vector3(0f, 0f, camToPlaneDist));
            Vector3 w1 = cam.ScreenToWorldPoint(new Vector3(1f, 0f, camToPlaneDist));
            pixelToWorldX = Mathf.Abs(w1.x - w0.x);
        }
        else
        {
            Debug.LogWarning("[FlowerQueen] RotateLeavesSequence: 未找到主摄像机，使用默认像素比 (100px=1u)。");
            pixelToWorldX = 1f / 100f;
        }

        float radiusWorld = rotateLeavesSpinRadiusPixels * pixelToWorldX;

        // Phase 1：沿圆周自转一圈（位移 + 朝向）
        float spinDur = Mathf.Max(0f, rotateLeavesSpinDuration);
        float spinAngleTotal = 360f * spinSign;
        float t = 0f;
        while (t < spinDur)
        {
            t += Time.deltaTime;
            float k = spinDur > 0f ? Mathf.Clamp01(t / spinDur) : 1f;
            float angDeg = startZ + spinAngleTotal * k;

            float rad = angDeg * Mathf.Deg2Rad;
            Vector3 offset = new Vector3(Mathf.Cos(rad) * radiusWorld, Mathf.Sin(rad) * radiusWorld, 0f);
            transform.position = axisCenter + offset;

            transform.rotation = Quaternion.Euler(0f, 0f, angDeg);
            yield return null;
        }
        transform.rotation = Quaternion.Euler(0f, 0f, startZ + spinAngleTotal);

        // Phase 2：回位（位置回到轴心 + 朝向回到初始）
        float returnDur = Mathf.Max(0f, rotateLeavesReturnDuration);
        Vector3 fromPos = transform.position;
        float fromZ = transform.eulerAngles.z;
        t = 0f;
        while (t < returnDur)
        {
            t += Time.deltaTime;
            float k = returnDur > 0f ? Mathf.Clamp01(t / returnDur) : 1f;

            transform.position = Vector3.Lerp(fromPos, axisCenter, k);
            float angDeg = Mathf.LerpAngle(fromZ, startZ, k);
            transform.rotation = Quaternion.Euler(0f, 0f, angDeg);
            yield return null;
        }
        transform.position = axisCenter;
        transform.rotation = Quaternion.Euler(0f, 0f, startZ);

        // Phase 3：生成叶子（原逻辑），并统一旋转方向
        int count = 6;
        float step = 360f / count;
        for (int i = 0; i < count; i++)
        {
            GameObject go = Instantiate(rotateLeavePrefab, axisCenter, Quaternion.identity, transform);
            var rot = go.GetComponent<RotateLeave>();
            if (rot != null)
            {
                rot.SetStartAngleDeg(i * step);
                rot.SetClockwise(clockwise);
            }
        }

        // Phase 4：等待所有旋转叶子销毁 → 停顿 0.3s → 发射 5 枚直线叶子子弹
        if (lineLeavePrefab != null)
        {
            // 等待直到所有 RotateLeave 被销毁（带超时保护）
            const float waitTimeout = 30f;
            float wt = 0f;
            while (HasAliveRotateLeaves() && wt < waitTimeout)
            {
                wt += Time.deltaTime;
                yield return null;
            }
            if (HasAliveRotateLeaves())
            {
                Debug.LogWarning("[FlowerQueen] RotateLeavesSequence: 等待旋转叶子销毁超时，仍将进行末尾直线发射。");
            }

            // 末尾停顿
            yield return new WaitForSeconds(0.3f);

            // 从当前所在位置发射（考虑等待期间可能已移动）
            Vector3 shootPos = transform.position;

            // 基于玩家方向的散射（-40/-20/0/20/40°）
            Transform player = PlayerControl.Player ? PlayerControl.Player.transform : null;
            Vector2 baseDir = Vector2.right;
            if (player != null)
            {
                Vector3 tp = player.position; tp.z = shootPos.z;
                Vector3 to = tp - shootPos;
                baseDir = to.sqrMagnitude > 1e-8f ? ((Vector2)to).normalized : Vector2.right;
            }

            float baseAng = Mathf.Atan2(baseDir.y, baseDir.x) * Mathf.Rad2Deg;
            float[] offsets = { -40f, -20f, 0f, 20f, 40f };

            for (int i = 0; i < offsets.Length; i++)
            {
                float ang = baseAng + offsets[i];
                float rad = ang * Mathf.Deg2Rad;
                Vector2 vel = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * lineLeaveSpeed;

                GameObject go = Instantiate(lineLeavePrefab, shootPos, Quaternion.identity);
                var rb = go.GetComponent<Rigidbody2D>();
                if (rb == null)
                {
                    rb = go.AddComponent<Rigidbody2D>();
                    rb.gravityScale = 0f;
                    rb.angularDrag = 0f;
                    rb.drag = 0f;
                    rb.interpolation = RigidbodyInterpolation2D.Interpolate;
                }
                else
                {
                    rb.gravityScale = 0f;
                }

                rb.velocity = vel;
                if (vel.sqrMagnitude > 0.000001f)
                    go.transform.right = vel.normalized;
            }
        }
        else
        {
            Debug.LogWarning("[FlowerQueen] lineLeavePrefab 未设置，跳过末尾直线叶子子弹发射。");
        }

        rotateLeavesSequenceRunning = false;
    }

    // ===================== 关键：等待序列和子物体销毁后再结束技能 =====================
    protected override IEnumerator OnSkillPost(EnemySkill skill)
    {
        yield return base.OnSkillPost(skill);

        if (skill.Name == "RotateLeaves")
        {
            const float safetyTimeout = 30f;
            float timer = 0f;
            Rigidbody2D rb = GetComponent<Rigidbody2D>();

            while (rotateLeavesSequenceRunning || HasAliveRotateLeaves())
            {
                if (timer >= safetyTimeout)
                {
                    Debug.LogWarning("[FlowerQueen] RotateLeaves 等待序列/子物体超时，强制继续。");
                    break;
                }

                if (rb != null) rb.velocity = Vector2.zero;
                MoveTowardsPlayerConstantSpeed(rotateLeavesApproachSpeed, Time.deltaTime);

                timer += Time.deltaTime;
                yield return null;
            }
        }
    }

    // 在技能执行前，若处于开场动画中，则等待其结束，防止开场期间释放技能
    protected override IEnumerator OnSkillPre(EnemySkill skill)
    {
        while (isOpening)
            yield return null;

        yield return base.OnSkillPre(skill);
    }

    private bool HasAliveRotateLeaves()
    {
        var leaves = GetComponentsInChildren<RotateLeave>(includeInactive: false);
        return leaves != null && leaves.Length > 0;
    }

    private void MoveTowardsPlayerConstantSpeed(float speed, float deltaTime)
    {
        Transform player = PlayerControl.Player ? PlayerControl.Player.transform : null;
        if (player == null || speed <= 0f) return;

        Vector3 pos = transform.position;
        Vector3 target = player.position; target.z = pos.z;
        Vector3 to = target - pos;
        float dist = to.magnitude;
        if (dist < 1e-4f) return;

        float step = speed * deltaTime;
        transform.position = step >= dist ? target : pos + to * (step / dist);
    }

    // ===================== 待机动作 =====================
    protected override void OnIdleUpdate(float deltaTime)
    {
        // 开场动画期间不执行 Idle 接近逻辑，避免干扰上移速度
        if (isOpening) return;

        Transform player = PlayerControl.Player ? PlayerControl.Player.transform : null;
        if (player == null) return;

        Vector2 toPlayer = player.position - transform.position;
        float dist = toPlayer.magnitude - idleApproachStopDistance;
        Vector2 dir = GetAimDirNormalized() * dist;
        if (dir.magnitude == 0) return;

        Vector2 vel = gameObject.GetComponent<Rigidbody2D>().velocity;
        vel += dir;
        if (vel.magnitude >= idleApproachSpeed)
            vel = vel.normalized * idleApproachSpeed;
        gameObject.GetComponent<Rigidbody2D>().velocity = vel;
    }

    private Vector2 GetAimDirNormalized()
    {
        Transform player = PlayerControl.Player ? PlayerControl.Player.transform : null;
        if (player == null) return Vector2.right;
        Vector2 dir = (player.position - transform.position);
        return dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector2.right;
    }
}
