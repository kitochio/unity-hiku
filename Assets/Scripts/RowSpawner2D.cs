using UnityEngine;
using System.Collections;

public class RowSpawner2D : MonoBehaviour
{
    [Header("Prefab & Physics")]
    [SerializeField] GameObject prefab;         // Rigidbody2D + Collider2D を持つプレハブ（Dynamic）
    [SerializeField] float speed = 3f;          // 移動速度（m/s）
    [SerializeField] Vector2 moveDirection = Vector2.right; // 移動方向（ゼロなら右）
    [SerializeField] float lifeTime = 10f;      // 生存時間（秒、0以下で無効）
    [SerializeField] bool useLocalSpace = false; // 方向・オフセットをローカル基準で解釈する

    [Header("Randomize Velocity")]
    [SerializeField] bool randomizeVelocity = false; // 速度と方向をランダム化する
    [SerializeField] float speedMin = 2f;      // 速度の最小値（randomizeVelocityが有効な時）
    [SerializeField] float speedMax = 4f;      // 速度の最大値
    [Tooltip("moveDirection を基準にした角度オフセット範囲（度）")]
    [SerializeField] float angleOffsetMinDeg = 0f;
    [SerializeField] float angleOffsetMaxDeg = 0f;

    [Header("Row layout")]
    [SerializeField] int count = 5;             // 1列の個数
    [SerializeField] float spacing = 1f;        // 隣同士の間隔
    // rowDirection は固定（上方向）

    [Header("Randomize Slots (Row)")]
    [Range(0f, 1f)]
    [SerializeField] float spawnProbability = 1f; // 各スロットの出現確率
    [SerializeField] bool useRandomSpawn = false; // スロットの出現をランダムにする

    [Header("Loop spawn")]
    [SerializeField] float interval = 1.5f;     // 繰り返し生成の間隔（秒）
    [SerializeField] bool playOnStart = true;   // 開始時に自動でループ開始
    [SerializeField] bool randomSmallJitter = true; // 配置に微小なゆらぎを加える

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
        if (!prefab) { Debug.LogWarning("Prefab が未設定"); return; }

        // 進行方向（ローカル/ワールド）
        var dirMoveBase = (moveDirection == Vector2.zero ? Vector2.right : moveDirection.normalized);
        Vector2 dirMove;
        if (useLocalSpace)
        {
            Vector3 dm = transform.TransformDirection((Vector3)dirMoveBase);
            dirMove = new Vector2(dm.x, dm.y).normalized;
        }
        else
        {
            dirMove = dirMoveBase;
        }

        // 列方向は固定の上方向。ローカル基準なら transform.up、ワールド基準なら Vector2.up
        var dirRow = useLocalSpace ? (Vector2)transform.up : Vector2.up;

        for (int i = 0; i < count; i++)
        {
            if (useRandomSpawn && Random.value > Mathf.Clamp01(spawnProbability)) { continue; }

            // 列方向オフセット（ローカル/ワールド）
            Vector2 offset = dirRow * (i * spacing);

            // 重なり・トンネリング対策の微小ランダムゆらぎ
            if (randomSmallJitter)
            {
                offset += new Vector2(Random.Range(-0.02f, 0.02f), Random.Range(-0.02f, 0.02f));
            }

            Vector3 pos = transform.position + (Vector3)offset;
            var go = Instantiate(prefab, pos, Quaternion.identity);

            // 進行方向と速度ベクトルを設定
            var rb = go.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                float sMin = Mathf.Min(speedMin, speedMax);
                float sMax = Mathf.Max(speedMin, speedMax);
                float spd = randomizeVelocity ? Random.Range(sMin, sMax) : speed;

                Vector2 dir = dirMove;
                if (randomizeVelocity)
                {
                    float aMin = Mathf.Min(angleOffsetMinDeg, angleOffsetMaxDeg);
                    float aMax = Mathf.Max(angleOffsetMinDeg, angleOffsetMaxDeg);
                    float offsetDeg = Random.Range(aMin, aMax);

                    // ワールド空間で角度を加算（dirMove はすでにローカル→ワールド適用済み）
                    float baseDeg = Mathf.Atan2(dirMove.y, dirMove.x) * Mathf.Rad2Deg;
                    float finalDeg = baseDeg + offsetDeg;
                    float rad = finalDeg * Mathf.Deg2Rad;
                    dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)).normalized;
                }

                rb.linearVelocity = dir * spd;
                rb.freezeRotation = true;
            }
            else
            {
                Debug.LogWarning("Prefab に Rigidbody2D がありません");
            }

            if (lifeTime > 0f)
            {
                var fx = go.GetComponent<MaterialOperations>();
                if (!fx) fx = go.AddComponent<MaterialOperations>();
                fx.Begin(lifeTime);
                Destroy(go, lifeTime);
            } 
        }
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        var dirRow = useLocalSpace ? (Vector2)transform.up : Vector2.up;
        for (int i = 0; i < Mathf.Max(1, count); i++)
        {
            Vector3 p = transform.position + (Vector3)(dirRow * (i * spacing));
            Gizmos.DrawWireSphere(p, 0.08f);
        }
        Gizmos.color = Color.yellow;
        Vector3 p0 = transform.position;
        Vector2 gizmoDir = (moveDirection == Vector2.zero ? Vector2.right : moveDirection.normalized);
        if (useLocalSpace)
        {
            Vector3 gd = transform.TransformDirection((Vector3)gizmoDir);
            gizmoDir = new Vector2(gd.x, gd.y).normalized;
        }
        Gizmos.DrawLine(p0, p0 + (Vector3)(gizmoDir * 1.5f));
    }
#endif
}

