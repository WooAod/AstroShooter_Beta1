using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class Portal : LoadRoom
{
    [Header("Player 生成点设置")]
    [SerializeField] private bool setPlayerSpawn = true;          // 是否指定玩家生成点
    [SerializeField] private bool useThisTransform = true;        // 使用本 Portal 的位置作为生成点
    [SerializeField] private Vector2 spawnOffset = Vector2.zero;  // 在本地平面上的偏移（2D）
    [SerializeField] private Vector3 explicitSpawnPosition;       // 自定义绝对世界坐标（当不使用本物体位置时）

    private bool _pendingSpawn; // 防重入

    // 2D 触发
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other != null && other.CompareTag("Player"))
        {
            PrepareSpawnForNextLoad();
            GetLoading();
        }
    }

    // 2D 碰撞
    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision != null && collision.collider != null && collision.collider.CompareTag("Player"))
        {
            PrepareSpawnForNextLoad();
            GetLoading();
        }
    }

    private void PrepareSpawnForNextLoad()
    {
        if (!setPlayerSpawn || _pendingSpawn) return;
        _pendingSpawn = true;
        SceneManager.sceneLoaded += OnSceneLoaded_SetPlayerSpawnOnce;
    }

    // 场景加载完成后，设置玩家位置并取消订阅（一次性）
    private void OnSceneLoaded_SetPlayerSpawnOnce(Scene scene, LoadSceneMode mode)
    {
        SceneManager.sceneLoaded -= OnSceneLoaded_SetPlayerSpawnOnce;
        _pendingSpawn = false;

        Vector3 target = useThisTransform
            ? new Vector3(transform.position.x + spawnOffset.x, transform.position.y + spawnOffset.y, transform.position.z)
            : explicitSpawnPosition;

        // 立即尝试放置玩家；若当帧未就绪则下一帧再试一次
        if (!TryPlacePlayer(scene.name, target))
        {
            StartCoroutine(TryPlacePlayerNextFrame(scene.name, target));
        }
    }

    private bool TryPlacePlayer(string sceneName, Vector3 target)
    {
        var playerGO = GameObject.FindGameObjectWithTag("Player");
        if (playerGO == null) return false;

        // 保持玩家原有的 Z（2D 项目避免层级错乱）
        var t = playerGO.transform;
        target.z = t.position.z;
        t.position = target;

        // 同步更新重生点到新场景
        PlayerControl.SetRespawnPoint(target, sceneName);
        return true;
    }

    private IEnumerator TryPlacePlayerNextFrame(string sceneName, Vector3 target)
    {
        yield return null; // 等待一帧，确保玩家已生成/启用
        TryPlacePlayer(sceneName, target);
    }
}
