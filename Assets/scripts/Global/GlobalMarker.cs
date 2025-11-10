using UnityEngine;

[DisallowMultipleComponent]
public class GlobalMarker : MonoBehaviour
{
    [SerializeField] private string markerId; // 为该实例/预制体配置的唯一标识
    public string Id => markerId;

    // 供代码在运行时确保标识一致（例如首次实例化后写入）
    public void SetId(string id) => markerId = id;
}