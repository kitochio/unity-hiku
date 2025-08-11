using UnityEngine;
using System.Collections;

public class RowSpawner2D : MonoBehaviour
{
    [Header("Prefab & Physics")]
    [SerializeField] GameObject prefab;         // Rigidbody2D + Collider2D を持つプレハブ（Dynamic）
    [SerializeField] float speed = 3f;          // 移動速度（m/s）
    [SerializeField] Vector2 moveDirection = Vector2.right; // 進行方向（正規化されます）
    [SerializeField] float lifeTime = 10f;      // 何秒後に自動破棄（0以下で無効）

    [Header("Row layout")]
    [SerializeField] int count = 5;             // 1列の個数
    [SerializeField] float spacing = 1f;        // 個体間距離
    [SerializeField] Vector2 rowDirection = Vector2.up; // 並べる方向（正規化されます）

    [Header("Loop spawn")]
    [SerializeField] float interval = 1.5f;     // 列の生成間隔（秒）
    [SerializeField] bool playOnStart = true;   // 自動で生成ループ開始
    [SerializeField] bool randomSmallJitter = true; // 発生時に微小ランダムズレ

    Coroutine loop;

    void Start()
    {
        if (playOnStart) loop = StartCoroutine(SpawnLoop());
    }

    public void StartLoop()
    {
        if (loop == null) loop = StartCoroutine(SpawnLoop());
    }

    public void StopLoop()
    {
        if (loop != null) { StopCoroutine(loop); loop = null; }
    }

    IEnumerator SpawnLoop()
    {
        var wait = new WaitForSeconds(interval);
        while (true)
        {
            SpawnRow();
            yield return wait;
        }
    }

    void SpawnRow()
    {
        if (!prefab) { Debug.LogWarning("Prefab 未設定"); return; }

        var dirMove = (moveDirection == Vector2.zero ? Vector2.right : moveDirection.normalized);
        var dirRow  = (rowDirection  == Vector2.zero ? Vector2.up    : rowDirection.normalized);

        for (int i = 0; i < count; i++)
        {
            // 並べる方向にオフセット
            Vector2 offset = dirRow * (i * spacing);

            // ほんの少しだけランダムにズラす（重なりやトunneling軽減に有効）
            if (randomSmallJitter)
            {
                offset += new Vector2(Random.Range(-0.02f, 0.02f), Random.Range(-0.02f, 0.02f));
            }

            Vector3 pos = transform.position + (Vector3)offset;
            var go = Instantiate(prefab, pos, Quaternion.identity);

            // 物理で移動：速度ベクトルを与える（各個体が独立に衝突）
            var rb = go.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.linearVelocity = dirMove * speed;
                // 横流し用途なら回転を固定すると安定
                rb.freezeRotation = true;
            }
            else
            {
                Debug.LogWarning("Prefab に Rigidbody2D がありません");
            }

            // 自動破棄
            if (lifeTime > 0f) Destroy(go, lifeTime);
        }
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        var dirRow  = (rowDirection == Vector2.zero ? Vector2.up : rowDirection.normalized);
        for (int i = 0; i < Mathf.Max(1, count); i++)
        {
            Vector3 p = transform.position + (Vector3)(dirRow * (i * spacing));
            Gizmos.DrawWireSphere(p, 0.08f);
        }
        Gizmos.color = Color.yellow;
        Vector3 p0 = transform.position;
        Gizmos.DrawLine(p0, p0 + (Vector3)(moveDirection.normalized * 1.5f));
    }
#endif
}
