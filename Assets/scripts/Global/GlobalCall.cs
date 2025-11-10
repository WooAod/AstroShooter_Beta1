using UnityEngine;

public class GlobalCall : MonoBehaviour
{
    [Header("脚本控制在本体生成时生成所需全局实例。\n要生成的实例预制体：")]
    [SerializeField] private GameObject TargetPerfab;

    [Header("用于匹配的唯一标识，请与预制体上的 GlobalMarker.Id 保持一致")]
    [SerializeField] private string markerId = "Global_TargetPrefab";

    private GameObject _instance;

    private void Start()
    {
        if (_instance != null) return;

        if (TargetPerfab == null)
        {
            Debug.LogError("[GlobalCall] TargetPerfab 未指定。", this);
            return;
        }

        // 先按标记查找是否已有实例
        var existing = FindExistingByMarker(markerId);

        // 若未找到，则实例化一个
        _instance = existing != null ? existing : Instantiate(TargetPerfab);

        // 确保实例上存在并设置正确标记（避免预制体未配置或标识不一致）
        var marker = _instance.GetComponent<GlobalMarker>();
        if (marker == null) marker = _instance.AddComponent<GlobalMarker>();
        marker.SetId(markerId);

        // 如需跨场景常驻可启用：
        // DontDestroyOnLoad(_instance);
    }

    private static GameObject FindExistingByMarker(string id)
    {
        var markers = FindObjectsOfType<GlobalMarker>();
        foreach (var m in markers)
        {
            if (m != null && m.Id == id)
                return m.gameObject;
        }
        return null;
    }
}