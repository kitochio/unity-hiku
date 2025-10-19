using UnityEngine;
using System.Collections;

public class MaterialOperations : MonoBehaviour
{
    [SerializeField] float fadeDuration = 0.5f; // フェード時間
    [SerializeField] float blinkHz = 8f;        // 点滅周波数(Hz)

    // 本体（このGameObject）のみ対象
    SpriteRenderer selfRenderer;
    MaterialPropertyBlock block;

    // Particles/Standard Unlit の Emission を使用
    static readonly int PropEmissionColor = Shader.PropertyToID("_EmissionColor");
    Color baseEmissionColor;   // 元のEmission色
    Color unitEmissionColor;   // 輝度1に正規化した色
    float baseIntensity;       // 元の輝度（maxRGB）

    public void Begin(float lifeTime)
    {
        if (lifeTime <= 0f || fadeDuration <= 0f) return;
        StartCoroutine(FadeRoutine(Mathf.Max(0f, lifeTime - fadeDuration), fadeDuration));
    }

    IEnumerator FadeRoutine(float delay, float duration)
    {
        // 対象: 本体の SpriteRenderer で、Particles/Standard Unlit + _EmissionColor がある場合のみ
        selfRenderer = GetComponent<SpriteRenderer>();
        var mat = selfRenderer ? selfRenderer.sharedMaterial : null;
        if (!(mat != null && mat.shader != null && mat.shader.name == "Particles/Standard Unlit" && mat.HasProperty(PropEmissionColor)))
            yield break;

        block = new MaterialPropertyBlock();

        // 参照となる Emission 色と輝度をキャッシュ
        var ec0 = mat.GetColor(PropEmissionColor);
        baseEmissionColor = ec0;
        baseIntensity = Mathf.Max(Mathf.Max(ec0.r, ec0.g), ec0.b);
        unitEmissionColor = baseIntensity > 0f ? (ec0 / Mathf.Max(baseIntensity, 1e-6f)) : ec0;

        if (delay > 0f) yield return new WaitForSeconds(delay);

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float fade = 1f - Mathf.Clamp01(t / duration);
            float blink = (Mathf.PingPong(Time.time * blinkHz, 1f) > 0.5f) ? 1f : 0f;
            float scale = fade * blink; // 輝度のみスケール

            if (selfRenderer)
            {
                float curIntensity = baseIntensity * scale;
                var ec = unitEmissionColor * curIntensity;
                block.SetColor(PropEmissionColor, ec);
                selfRenderer.SetPropertyBlock(block);
            }

            yield return null;
        }
    }

    // 外部からエミッションカラーを設定する
    public void SetEmissionColor(Color newEmissionColor)
    {
        if (!selfRenderer) selfRenderer = GetComponent<SpriteRenderer>();
        var mat = selfRenderer ? selfRenderer.sharedMaterial : null;
        if (!(mat != null && mat.shader != null && mat.shader.name == "Particles/Standard Unlit" && mat.HasProperty(PropEmissionColor)))
            return;

        if (block == null) block = new MaterialPropertyBlock();

        // ベース情報を更新（フェード/点滅計算用）
        baseEmissionColor = newEmissionColor;
        baseIntensity = Mathf.Max(Mathf.Max(newEmissionColor.r, newEmissionColor.g), newEmissionColor.b);
        unitEmissionColor = baseIntensity > 0f ? (newEmissionColor / Mathf.Max(baseIntensity, 1e-6f)) : newEmissionColor;

        // 即時適用
        block.SetColor(PropEmissionColor, newEmissionColor);
        selfRenderer.SetPropertyBlock(block);
    }

    void OnDisable()
    {
        // 無効化時に Emission を元に戻す
        if (!selfRenderer) return;

        var b = block ?? new MaterialPropertyBlock();
        b.SetColor(PropEmissionColor, baseEmissionColor);
        selfRenderer.SetPropertyBlock(b);
    }
}
