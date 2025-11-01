using UnityEngine;

// 破棄（OnDestroy）時にパーティクルを再生する簡易ユーティリティ
/// <summary>
/// 破棄時（OnDestroy）にパーティクルを再生するための軽量ユーティリティ。
/// </summary>
public class PlayParticleOnDestroy : MonoBehaviour
{
    [Tooltip("再生に使うパーティクルのプレハブ")]
    public GameObject particlePrefab;

    [Tooltip("破棄時にパーティクルを再生するか（実行時に切替可）")]
    public bool playOnDestroy = false;

    private bool _isQuitting = false;

    /// <summary>
    /// アプリ終了中は OnDestroy をスキップするためのフラグを立てます。
    /// </summary>
    void OnApplicationQuit()
    {
        // 終了時の OnDestroy はスキップ
        _isQuitting = true;
    }

    /// <summary>
    /// 終了中でなく、設定が ON かつプレハブがあればパーティクルを生成・再生します。
    /// </summary>
    void OnDestroy()
    {
        if (_isQuitting || !playOnDestroy || !particlePrefab) return;
        InstantiateAndPlay(particlePrefab, transform.position);
    }

    // Editor/Runtime 両方から切り替えやすいよう公開
    /// <summary>Editor/Runtime 双方からトグルできる公開セッター。</summary>
    public void SetPlayOnDestroy(bool value) => playOnDestroy = value;

    /// <summary>
    /// プレハブを生成してパーティクルを再生し、寿命に応じて自動破棄します。
    /// </summary>
    private static void InstantiateAndPlay(GameObject prefab, Vector3 position)
    {
        var go = Object.Instantiate(prefab, position, Quaternion.identity);
        var ps = go.GetComponent<ParticleSystem>();
        if (ps != null)
        {
            ps.Play();
            Object.Destroy(go, ps.main.duration + ps.main.startLifetime.constantMax);
        }
        else
        {
            Object.Destroy(go, 2f);
        }
    }
}
