using UnityEngine;

/// <summary>
/// Emission カラー操作と対応チェックの共通ユーティリティ。
/// 主に SpriteRenderer（Particles/Standard Unlit）用。
/// </summary>
public static class EmissionColorUtil
{
    public static readonly int PropEmissionColor = Shader.PropertyToID("_EmissionColor");
    public const string TargetShaderName = "Particles/Standard Unlit";

    /// <summary>対象 SpriteRenderer が Emission をサポートするか。</summary>
    public static bool Supports(SpriteRenderer sr)
    {
        if (!sr) return false;
        var mat = sr.sharedMaterial;
        return mat && mat.shader && mat.shader.name == TargetShaderName && mat.HasProperty(PropEmissionColor);
    }

    /// <summary>現在の Emission 色を取得（対応していれば true）。</summary>
    public static bool TryGetCurrent(GameObject go, out Color color)
    {
        color = default;
        if (!go) return false;
        var sr = go.GetComponent<SpriteRenderer>();
        var mat = sr ? sr.sharedMaterial : null;
        if (!(mat != null && mat.shader != null && mat.shader.name == TargetShaderName && mat.HasProperty(PropEmissionColor)))
            return false;
        color = mat.GetColor(PropEmissionColor);
        return true;
    }
}

