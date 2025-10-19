using UnityEngine;
using System.Collections;

public class BlinkFadeBeforeDestroy : MonoBehaviour
{
    [SerializeField] float fadeDuration = 0.5f; // フェード時間
    [SerializeField] float blinkHz = 8f;        // 点滅周波数(Hz)

    SpriteRenderer[] renderers;
    MaterialPropertyBlock[] blocks;

    // Particles/Standard Unlit の Emission を使用
    static readonly int PropEmissionColor = Shader.PropertyToID("_EmissionColor");
    Color[] baseEmissionColors;      // 復元用の元色
    Color[] unitEmissionColors;      // 強度1に正規化した色
    float[] baseIntensities;         // 元の強度（maxRGB）

    public void Begin(float lifeTime)
    {
        if (lifeTime <= 0f || fadeDuration <= 0f) return;
        StartCoroutine(FadeRoutine(Mathf.Max(0f, lifeTime - fadeDuration), fadeDuration));
    }

    IEnumerator FadeRoutine(float delay, float duration)
    {
        // 対象: 子孫の SpriteRenderer のうち Particles/Standard Unlit + _EmissionColor を持つもの
        var all = GetComponentsInChildren<SpriteRenderer>(true);
        int n = 0;
        for (int i = 0; i < all.Length; i++)
        {
            var mat = all[i] ? all[i].sharedMaterial : null;
            if (mat != null && mat.shader != null && mat.shader.name == "Particles/Standard Unlit" && mat.HasProperty(PropEmissionColor))
                n++;
        }

        renderers = new SpriteRenderer[n];
        blocks = new MaterialPropertyBlock[n];
        baseEmissionColors = new Color[n];
        unitEmissionColors = new Color[n];
        baseIntensities = new float[n];

        int idx = 0;
        for (int i = 0; i < all.Length; i++)
        {
            var r = all[i];
            var mat = r ? r.sharedMaterial : null;
            if (!(mat != null && mat.shader != null && mat.shader.name == "Particles/Standard Unlit" && mat.HasProperty(PropEmissionColor)))
                continue;

            renderers[idx] = r;
            var b = new MaterialPropertyBlock();
            blocks[idx] = b;

            // 基準となる Emission 色と強度をキャッシュ
            var ec0 = mat.GetColor(PropEmissionColor);
            baseEmissionColors[idx] = ec0;
            float inten = Mathf.Max(Mathf.Max(ec0.r, ec0.g), ec0.b);
            baseIntensities[idx] = inten;
            unitEmissionColors[idx] = inten > 0f ? (ec0 / Mathf.Max(inten, 1e-6f)) : ec0;

            idx++;
        }

        if (delay > 0f) yield return new WaitForSeconds(delay);

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float fade = 1f - Mathf.Clamp01(t / duration);
            float blink = (Mathf.PingPong(Time.time * blinkHz, 1f) > 0.5f) ? 1f : 0f;
            float scale = fade * blink; // 強度のみスケール

            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (!r) continue;

                var b = blocks[i];
                float curIntensity = baseIntensities[i] * scale;
                var ec = unitEmissionColors[i] * curIntensity;
                b.SetColor(PropEmissionColor, ec);
                r.SetPropertyBlock(b);
            }
            yield return null;
        }
    }

    void OnDisable()
    {
        // 破棄時に Emission を元に戻す
        if (renderers == null) return;
        for (int i = 0; i < renderers.Length; i++)
        {
            var r = renderers[i];
            if (!r) continue;

            var b = blocks != null && i < blocks.Length && blocks[i] != null ? blocks[i] : new MaterialPropertyBlock();
            if (baseEmissionColors != null && i < baseEmissionColors.Length)
            {
                b.SetColor(PropEmissionColor, baseEmissionColors[i]);
            }
            r.SetPropertyBlock(b);
        }
    }
}

