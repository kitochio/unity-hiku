using UnityEngine;

public class PlayParticleOnDestroy : MonoBehaviour
{
    [Tooltip("再生したいパーティクルのプレハブを指定")]
    public GameObject particlePrefab;

    [Tooltip("削除時にパーティクルを再生するかどうか（外部から変更可能）")]
    public bool playOnDestroy = false;

    private bool isQuitting = false;

    void OnApplicationQuit()
    {
        // アプリ終了時はOnDestroyをスキップ
        isQuitting = true;
    }

    void OnDestroy()
    {
        // 再生フラグがオフなら何もしない
        if (isQuitting || !playOnDestroy) return;

        if (particlePrefab != null)
        {
            // 現在の位置と回転でパーティクルを生成
            GameObject particle = Instantiate(particlePrefab, transform.position, Quaternion.identity);

            // ParticleSystemがある場合は再生して寿命後に削除
            ParticleSystem ps = particle.GetComponent<ParticleSystem>();
            if (ps != null)
            {
                ps.Play();
                Destroy(particle, ps.main.duration + ps.main.startLifetime.constantMax);
            }
            else
            {
                Destroy(particle, 2f);
            }
        }
    }

    /// <summary>
    /// 外部スクリプトから再生のオンオフを切り替える関数
    /// </summary>
    public void SetPlayOnDestroy(bool value)
    {
        playOnDestroy = value;
    }
}
