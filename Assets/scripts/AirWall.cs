using UnityEngine;

public class AirWall : MonoBehaviour
{
    [Header("透明配置")]
    [SerializeField, Range(0f, 1f)] private float targetAlpha = 0f; // 目标透明度（默认全透明）
    [SerializeField] private bool applyToChildren = false;         // 是否包含子对象全部 Renderer

    private void Awake()
    {
        MakeTransparentInstant();
    }

    /// <summary>
    /// 立即令所有材质进入可透明混合并设置目标 Alpha。
    /// </summary>
    public void MakeTransparentInstant()
    {
        var renderers = applyToChildren ? GetComponentsInChildren<Renderer>() : GetComponents<Renderer>();
        foreach (var r in renderers)
        {
            if (r == null) continue;
            foreach (var mat in r.materials) // 使用实例材质避免影响其他对象
            {
                if (mat == null) continue;
                SetupFadeMode(mat);
                var c = mat.color;
                c.a = targetAlpha;
                mat.color = c;
            }
        }
    }

    /// <summary>
    /// 恢复为不透明。
    /// </summary>
    public void RestoreOpaque()
    {
        var renderers = applyToChildren ? GetComponentsInChildren<Renderer>() : GetComponents<Renderer>();
        foreach (var r in renderers)
        {
            if (r == null) continue;
            foreach (var mat in r.materials)
            {
                if (mat == null) continue;
                SetupOpaqueMode(mat);
                var c = mat.color; c.a = 1f; mat.color = c;
            }
        }
    }

    // 设为可透明混合（针对 Standard Shader；其它 Shader 尝试开启常规混合关键字）
    private void SetupFadeMode(Material mat)
    {
        if (mat.shader != null && mat.shader.name.Contains("Standard"))
        {
            mat.SetFloat("_Mode", 2); // Fade
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = 3000;
        }
        else
        {
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.renderQueue = 3000;
        }
    }

    private void SetupOpaqueMode(Material mat)
    {
        if (mat.shader != null && mat.shader.name.Contains("Standard"))
        {
            mat.SetFloat("_Mode", 0); // Opaque
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
            mat.SetInt("_ZWrite", 1);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.DisableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = -1;
        }
        else
        {
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
            mat.renderQueue = -1;
        }
    }
}