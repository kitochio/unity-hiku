using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class Point : MonoBehaviour
{
    /// <summary>
    /// 衝突相手から離れる向きに加速度を加える 2D ポイント挙動。
    /// 対称適用が有効な場合は相手側にも等価に作用させます。
    /// </summary>
    [Header("Force Settings")]
    [Tooltip("相手から離れる加速度の大きさ（m/s^2）")]
    public float acceleration = 8f;
    [Tooltip("加速度の上限（1 フレームあたりの最大換算）")]
    public float maxAcceleration = 20f;

    [Header("Filter")]
    [Tooltip("衝突相手として扱うレイヤー")]
    public LayerMask targetLayers = ~0;
    [Tooltip("相手側にも反作用の力を適用するか")]
    public bool applySymmetrically = true;

    private Rigidbody2D _rb;

    /// <summary>Rigidbody2D をキャッシュします。</summary>
    void Awake() => _rb = GetComponent<Rigidbody2D>();

    /// <summary>
    /// 衝突継続中の相手に対して、離れる方向へ加速（力）を与えます。
    /// </summary>
    void OnCollisionStay2D(Collision2D col)
    {
        var otherRb = col.rigidbody;
        if (!IsTarget(col) || otherRb == _rb) return;

        // 自分の質量中心から相手の質量中心へ向かうベクトルの逆方向（離れる向き）
        Vector2 dir = DirectionAwayFromOther(col, otherRb);
        if (dir.sqrMagnitude < 1e-6f) return;

        // Acceleration ベース -> 力に変換（F = m * a）
        Vector2 a = Vector2.ClampMagnitude(dir * acceleration, maxAcceleration);
        _rb.AddForce(a * _rb.mass, ForceMode2D.Force);

        // 反作用（同一ペアで二重適用を避けるため ID で片側だけ）
        if (applySymmetrically && otherRb && GetInstanceID() < otherRb.GetInstanceID())
        {
            otherRb.AddForce((-a) * otherRb.mass, ForceMode2D.Force);
        }
    }

    /// <summary>対象レイヤーに含まれる衝突か判定します。</summary>
    bool IsTarget(Collision2D col) => ((1 << col.gameObject.layer) & targetLayers) != 0;

    /// <summary>
    /// 相手の重心から自分の重心へ向かう正規化ベクトルを返します。
    /// </summary>
    Vector2 DirectionAwayFromOther(Collision2D col, Rigidbody2D otherRb)
    {
        Vector2 otherCenter = otherRb ? (Vector2)otherRb.worldCenterOfMass : col.collider.bounds.center;
        return ((Vector2)_rb.worldCenterOfMass - otherCenter).normalized;
    }
}
