using UnityEngine; // 引入 UnityEngine 命名空间，以使用 MonoBehaviour、GameObject、DontDestroyOnLoad 等 Unity API

/// <summary>
/// 泛型单例基类：为每个具体的派生类型 T 维护一个且仅一个常驻实例。
/// - 用法：public class AudioManager : Singleton&lt;AudioManager&gt; {}
/// - 作用：在首次创建时注册为单例并跨场景常驻，后续重复实例会被自动销毁。同时提供静态访问接口 T.Instance 获取单例引用。
/// </summary>
/// <typeparam name="T">
/// 派生类型本身（自限制约要求 T : Singleton&lt;T&gt;），
/// 这样可以在基类里安全地将 this 转为 T，并为每个 T 分别维护独立的静态实例。
/// </typeparam>
public abstract class Singleton<T> : MonoBehaviour where T : Singleton<T> // 自限制约，确保派生类以自身作为类型参数
{
    private static T _instance;                // 静态字段：保存当前 T 类型的唯一实例引用（对全局共享，跨场景有效）
    public static T Instance => _instance;     // 公共只读访问器，通过 T.Instance 获取当前单例；未初始化时为 null
    public static bool IsInitialized => _instance != null; // 是否已完成单例注册，便于外部做空判断

    /// <summary>
    /// Unity 生命周期回调：在对象启用时最先执行之一。
    /// 这里负责：去重（销毁重复实例）+ 注册单例 + 标记为跨场景常驻 + 通知派生类可初始化。
    /// </summary>
    protected virtual void Awake()
    {
        // 若已有同类型单例且不是当前对象，说明这是重复实例（可能来自新场景或重复放置的预制体）
        if (_instance != null && _instance != (T)this)
        {
            Destroy(gameObject); // 销毁重复的 GameObject，避免出现多个副本
            return;              // 立刻返回，阻止继续注册
        }

        _instance = (T)this;             // 将当前组件注册为该类型的唯一实例
        DontDestroyOnLoad(gameObject);   // 将承载该组件的 GameObject 设为跨场景常驻
        OnSingletonReady();              // 通知派生类：单例已就绪，可安全执行初始化逻辑
    }

    /// <summary>
    /// 单例就绪回调：替代在派生类 Awake 中初始化，避免与基类注册顺序冲突。
    /// 可在此处加载资源、订阅事件、创建子对象等。
    /// </summary>
    protected virtual void OnSingletonReady() { } // 默认空实现，派生类按需重写

    /// <summary>
    /// Unity 生命周期回调：当对象销毁时触发。
    /// 若当前对象正是已注册的单例，需将静态引用清空，避免“脏引用”残留。
    /// </summary>
    protected virtual void OnDestroy()
    {
        if (_instance == (T)this) // 仅在自己是当前单例时清理（防止误清理其他仍存活的实例引用）
            _instance = null;     // 清空静态引用，便于下次重新创建或正确判定未初始化
    }
}