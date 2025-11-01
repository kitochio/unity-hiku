using UnityEngine;
using System.Collections;

// 2D 向け：行方向に等間隔でプレハブを複数生成し、移動させる
/// <summary>
/// 2D 用：指定間隔で縦/横に並んだ列としてプレハブを複数生成し、
/// 進行方向に移動させます。ループ生成・速度や角度のランダム化、
/// 寿命に応じたフェード演出と自動破棄に対応します。
/// </summary>
public class RowSpawner2D : MonoBehaviour
{
    [Header("Prefab & Physics")]
    [SerializeField, Tooltip("Rigidbody2D + Collider2D を持つプレハブ（Dynamic）")] private GameObject prefab;
    [SerializeField, Tooltip("基準の移動速度（m/s）")] private float speed = 3f;
    [SerializeField, Tooltip("基準の移動方向（ワールド）")] private Vector2 moveDirection = Vector2.right;
    [SerializeField, Tooltip("生成したオブジェクトの寿命（秒、0 以下は無制限、ランダム OFF の時に使用）")] private float lifeTime = 10f;
    
    [Header("Randomize LifeTime")]
    [SerializeField, Tooltip("寿命をランダムに設定する")]
    private bool randomizeLifeTime = false;
    [SerializeField, Tooltip("寿命の最小値（秒）")] private float lifeTimeMin = 5f;
    [SerializeField, Tooltip("寿命の最大値（秒）")] private float lifeTimeMax = 10f;
    [SerializeField, Tooltip("ローカル方向（自分の向き）に合わせる")]
    private bool useLocalSpace = false;

    [Header("Randomize Velocity")]
    [SerializeField, Tooltip("速度と方向をランダム化する")]
    private bool randomizeVelocity = false;
    [SerializeField, Tooltip("速度の最小値（ランダム化 ON のとき）")] private float speedMin = 2f;
    [SerializeField, Tooltip("速度の最大値")] private float speedMax = 4f;
    [Tooltip("moveDirection 基準に±の角度ずらし（度）")]
    [SerializeField] private float angleOffsetMinDeg = 0f;
    [SerializeField] private float angleOffsetMaxDeg = 0f;

    [Header("Row layout")]
    [SerializeField, Tooltip("1 行の生成数")] private int count = 5;
    [SerializeField, Tooltip("行方向の間隔（m）")] private float spacing = 1f;

    [Header("Randomize Slots (Row)")]
    [Range(0f, 1f)]
    [SerializeField, Tooltip("各スロットの出現確率（ランダム ON で有効）")] private float spawnProbability = 1f;
    [SerializeField, Tooltip("各スロットの出現をランダムにする")]
    private bool useRandomSpawn = false;

    [Header("Loop spawn")]
    [SerializeField, Tooltip("生成の間隔（秒）")] private float interval = 1.5f;
    [SerializeField, Tooltip("開始時に自動スタート")] private bool playOnStart = true;
    [SerializeField, Tooltip("行オフセットへ微小なジッターを加える")] private bool randomSmallJitter = true;

    private Coroutine _loop;

    /// <summary>起動時に自動ループが有効なら生成ループを開始します。</summary>
    void Start()
    {
        if (playOnStart) _loop = StartCoroutine(SpawnLoop());
    }

    /// <summary>生成ループを開始します（多重開始はしません）。</summary>
    public void StartLoop()
    {
        if (_loop == null) _loop = StartCoroutine(SpawnLoop());
    }

    /// <summary>生成ループを停止します。</summary>
    public void StopLoop()
    {
        if (_loop != null) { StopCoroutine(_loop); _loop = null; }
    }

    /// <summary>
    /// 一定間隔ごとに 1 列分を生成するループ。
    /// </summary>
    IEnumerator SpawnLoop()
    {
        var wait = new WaitForSeconds(interval);
        while (true)
        {
            SpawnRow();
            yield return wait;
        }
    }

    /// <summary>
    /// 現在の設定に基づき 1 列分のプレハブを生成します。
    /// </summary>
    void SpawnRow()
    {
        if (!prefab) { Debug.LogWarning("Prefab が未設定"); return; }

        Vector2 dirMove = ComputeMoveDir();
        Vector2 dirRow = ComputeRowDir();

        for (int i = 0; i < count; i++)
        {
            if (useRandomSpawn && Random.value > Mathf.Clamp01(spawnProbability)) continue;

            Vector2 offset = dirRow * (i * spacing);
            if (randomSmallJitter)
                offset += new Vector2(Random.Range(-0.02f, 0.02f), Random.Range(-0.02f, 0.02f));

            SpawnOne(transform.position + (Vector3)offset, dirMove);
        }
    }

    /// <summary>
    /// 実際に使用する移動方向を算出します（ローカル空間オプション対応）。
    /// </summary>
    Vector2 ComputeMoveDir()
    {
        var baseDir = (moveDirection == Vector2.zero ? Vector2.right : moveDirection.normalized);
        if (!useLocalSpace) return baseDir;
        Vector3 dm = transform.TransformDirection((Vector3)baseDir);
        return new Vector2(dm.x, dm.y).normalized;
    }

    /// <summary>列方向（ローカルの up かワールド up）を返します。</summary>
    Vector2 ComputeRowDir()
    {
        return useLocalSpace ? (Vector2)transform.up : Vector2.up;
    }

    /// <summary>
    /// 1 体の生成と初期速度設定、寿命設定を行います。
    /// </summary>
    void SpawnOne(Vector3 position, Vector2 dirMove)
    {
        var go = Instantiate(prefab, position, Quaternion.identity);
        var rb = go.GetComponent<Rigidbody2D>();
        if (rb)
        {
            var (dir, spd) = ComputeVelocityDirectionAndSpeed(dirMove);
            rb.linearVelocity = dir * spd;
            rb.freezeRotation = true;
        }
        else
        {
            Debug.LogWarning("Prefab に Rigidbody2D がありません");
        }

        float lt = lifeTime;
        if (randomizeLifeTime)
        {
            float lMin = Mathf.Min(lifeTimeMin, lifeTimeMax);
            float lMax = Mathf.Max(lifeTimeMin, lifeTimeMax);
            lt = Random.Range(lMin, lMax);
        }

        if (lt > 0f)
        {
            var fx = go.GetComponent<MaterialOperations>() ?? go.AddComponent<MaterialOperations>();
            fx.Begin(lt);
            Destroy(go, lt);
        }
    }

    /// <summary>
    /// 速度の大きさと方向を（必要に応じてランダム化して）計算します。
    /// </summary>
    (Vector2 dir, float speed) ComputeVelocityDirectionAndSpeed(Vector2 dirMove)
    {
        if (!randomizeVelocity) return (dirMove, speed);

        float sMin = Mathf.Min(speedMin, speedMax);
        float sMax = Mathf.Max(speedMin, speedMax);
        float spd = Random.Range(sMin, sMax);

        float aMin = Mathf.Min(angleOffsetMinDeg, angleOffsetMaxDeg);
        float aMax = Mathf.Max(angleOffsetMinDeg, angleOffsetMaxDeg);
        float offsetDeg = Random.Range(aMin, aMax);

        float baseDeg = Mathf.Atan2(dirMove.y, dirMove.x) * Mathf.Rad2Deg;
        float finalDeg = baseDeg + offsetDeg;
        float rad = finalDeg * Mathf.Deg2Rad;
        Vector2 dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)).normalized;
        return (dir, spd);
    }

#if UNITY_EDITOR
    /// <summary>エディタ上で列の位置と移動方向を可視化します。</summary>
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        var dirRow = ComputeRowDir();
        for (int i = 0; i < Mathf.Max(1, count); i++)
        {
            Vector3 p = transform.position + (Vector3)(dirRow * (i * spacing));
            Gizmos.DrawWireSphere(p, 0.08f);
        }

        Gizmos.color = Color.yellow;
        Vector3 p0 = transform.position;
        Vector2 gizmoDir = ComputeMoveDir();
        Gizmos.DrawLine(p0, p0 + (Vector3)(gizmoDir * 1.5f));
    }
#endif
}
