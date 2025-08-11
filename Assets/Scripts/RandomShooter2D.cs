// RandomShooter2D.cs
using UnityEngine;
using System.Collections;

public class RandomShooter2D : MonoBehaviour
{
    [Header("Spawn")]
    public GameObject bulletPrefab; // 円プレハブ
    public Transform muzzle;        // 発射位置（未設定ならこのオブジェクト位置）
    
    [Header("Fire")]
    public float shotsPerSecond = 5f;  // 1秒あたりの発射数
    public float speedMin = 4f;        // 最小速度
    public float speedMax = 8f;        // 最大速度
    public bool randomizeInterval = false; // 間隔もランダムにするならON

    Coroutine loop;

    void OnEnable() => loop = StartCoroutine(FireLoop());
    void OnDisable() { if (loop != null) StopCoroutine(loop); }

    IEnumerator FireLoop()
    {
        while (true)
        {
            SpawnOne();

            if (randomizeInterval)
            {
                // 平均 shotsPerSecond になるよう指数分布っぽく
                float mean = 1f / Mathf.Max(0.0001f, shotsPerSecond);
                float t = -Mathf.Log(1f - Random.value) * mean;
                yield return new WaitForSeconds(t);
            }
            else
            {
                float interval = 1f / Mathf.Max(0.0001f, shotsPerSecond);
                yield return new WaitForSeconds(interval);
            }
        }
    }

    void SpawnOne()
    {
        var pos = muzzle ? muzzle.position : transform.position;
        var go = Instantiate(bulletPrefab, pos, Quaternion.identity);

        // 0〜2πの角度を一様に選ぶ → 全方位ランダム
        float ang = Random.value * Mathf.PI * 2f;
        Vector2 dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));

        float speed = Random.Range(speedMin, speedMax);

        var b = go.GetComponent<Bullet2D>();
        b.Launch(dir * speed);
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        // 発射位置の目印
        Gizmos.color = new Color(1,1,1,0.5f);
        Vector3 p = muzzle ? muzzle.position : transform.position;
        Gizmos.DrawWireSphere(p, 0.15f);
    }
#endif
}
