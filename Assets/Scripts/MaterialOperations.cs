using UnityEngine;
using System.Collections;

// Particles/Standard Unlit の Emission をフェード＋点滅させるヘルパー
/// <summary>
/// Particles/Standard Unlit を対象に、Emission のブリンク/フェードや色変更を行う補助。
/// SpriteRenderer の MaterialPropertyBlock と子孫の ParticleSystemRenderer に適用します。
/// </summary>
public class MaterialOperations : MonoBehaviour
{
    [Header("Blink/Fade")]
    [SerializeField, Tooltip("フェード時間（秒）")] private float fadeDuration = 0.5f;
    [SerializeField, Tooltip("点滅周波数（Hz）")] private float blinkHz = 8f;

    // 対象: 自身の SpriteRenderer の共有マテリアル
    private SpriteRenderer _renderer;
    private MaterialPropertyBlock _block;

    // Emission property
    private static readonly int PropEmissionColor = Shader.PropertyToID("_EmissionColor");
    private Color _baseEmissionColor;   // ベースの Emission 色
    private Color _unitEmissionColor;   // 単位強度(=1)の色
    private float _baseIntensity;       // ベース強度（RGB の最大）

    // lifeTime の終わりに向けてフェード＋点滅
    /// <summary>
    /// 指定の生存時間に合わせて、末尾でフェードアウトする演出を開始します。
    /// </summary>
    /// <param name="lifeTime">オブジェクトの寿命（秒）</param>
    public void Begin(float lifeTime)
    {
        if (lifeTime <= 0f || fadeDuration <= 0f) return;
        StartCoroutine(FadeRoutine(Mathf.Max(0f, lifeTime - fadeDuration), fadeDuration));
    }

    /// <summary>
    /// 一定時間待機した後、Emission の強度をブリンクしながらフェードさせます。
    /// </summary>
    private IEnumerator FadeRoutine(float delay, float duration)
    {
        if (!TryInitTarget()) yield break; // 未対応マテリアルなら何もしない

        // ベースの Emission と単位色を記録
        var mat = _renderer.sharedMaterial;
        var ec0 = mat.GetColor(PropEmissionColor);
        _baseEmissionColor = ec0;
        _baseIntensity = Mathf.Max(Mathf.Max(ec0.r, ec0.g), ec0.b);
        _unitEmissionColor = _baseIntensity > 0f ? (ec0 / Mathf.Max(_baseIntensity, 1e-6f)) : ec0;

        if (delay > 0f) yield return new WaitForSeconds(delay);

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float fade = 1f - Mathf.Clamp01(t / duration);
            float blink = (Mathf.PingPong(Time.time * blinkHz, 1f) > 0.5f) ? 1f : 0f;
            float scale = fade * blink;

            if (_renderer)
            {
                float curIntensity = _baseIntensity * scale;
                var ec = _unitEmissionColor * curIntensity;
                EnsureBlock();
                _block.SetColor(PropEmissionColor, ec);
                _renderer.SetPropertyBlock(_block);
            }

            yield return null;
        }
    }

    // 現在の Emission 色を即時に設定（Begin の見た目更新にも利用）
    /// <summary>
    /// 現在の Emission 色を明示的に設定し、SpriteRenderer と子孫のパーティクルにも反映します。
    /// </summary>
    public void SetEmissionColor(Color newEmissionColor)
    {
        // Update local SpriteRenderer if available
        bool hasTarget = TryInitTarget();
        if (hasTarget)
        {
            _baseEmissionColor = newEmissionColor;
            _baseIntensity = Mathf.Max(Mathf.Max(newEmissionColor.r, newEmissionColor.g), newEmissionColor.b);
            _unitEmissionColor = _baseIntensity > 0f ? (newEmissionColor / Mathf.Max(_baseIntensity, 1e-6f)) : newEmissionColor;

            EnsureBlock();
            _block.SetColor(PropEmissionColor, newEmissionColor);
            _renderer.SetPropertyBlock(_block);
        }

        // Also apply to all descendant ParticleSystemRenderers
        var psRenderers = GetComponentsInChildren<ParticleSystemRenderer>(true);
        if (psRenderers != null && psRenderers.Length > 0)
        {
            var tmpBlock = new MaterialPropertyBlock();
            tmpBlock.SetColor(PropEmissionColor, newEmissionColor);
            foreach (var psr in psRenderers)
            {
                if (!psr) continue;
                var mat = psr.sharedMaterial;
                if (mat && mat.shader && mat.HasProperty(PropEmissionColor))
                {
                    psr.SetPropertyBlock(tmpBlock);
                }
            }
        }
    }

    /// <summary>
    /// 無効化時に Emission をベース色へ戻します。
    /// </summary>
    private void OnDisable()
    {
        // 無効化時に Emission を元へ戻す
        if (!_renderer) return;
        EnsureBlock();
        _block.SetColor(PropEmissionColor, _baseEmissionColor);
        _renderer.SetPropertyBlock(_block);
    }

    // 対象 SpriteRenderer/Material が要件を満たすか
    /// <summary>
    /// 対象の SpriteRenderer と対応する Material を確保します。
    /// </summary>
    private bool TryInitTarget()
    {
        if (!_renderer) _renderer = GetComponent<SpriteRenderer>();
        var mat = _renderer ? _renderer.sharedMaterial : null;
        return mat && mat.shader && mat.shader.name == "Particles/Standard Unlit" && mat.HasProperty(PropEmissionColor);
    }

    /// <summary>MaterialPropertyBlock を遅延生成します。</summary>
    private void EnsureBlock()
    {
        if (_block == null) _block = new MaterialPropertyBlock();
    }
}
