using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class Point : MonoBehaviour
{
    public float acceleration = 8f; // 毎FixedUpdateで付与したい“加速度”の大きさ
    public float maxAcceleration = 20f; // 1回あたりの上限（暴走・震え防止）
    public LayerMask targetLayers = ~0; // 反発対象のレイヤー（既定は全レイヤー）
    public bool applySymmetrically = true; // 片側のスクリプトから相手にも同量を与えるか

    Rigidbody2D rb;

    void Awake() => rb = GetComponent<Rigidbody2D>();

    void OnCollisionStay2D(Collision2D col)
    {
        var otherRb = col.rigidbody; // 衝突相手のRigidbody
        if (otherRb == rb) return; // 自己参照防止
        if (((1 << col.gameObject.layer) & targetLayers) == 0) return;

        // 反発方向の決定：質量中心同士のベクトル（自分→相手を逆にとる）
        Vector2 otherCenter = otherRb ? (Vector2)otherRb.worldCenterOfMass : col.collider.bounds.center;
        Vector2 dir = ((Vector2)rb.worldCenterOfMass - otherCenter).normalized;
        if (dir.sqrMagnitude < 1e-6f) return;

        // 2Dには Acceleration モードがないので「F = m * a」で力に変換
        Vector2 a = Vector2.ClampMagnitude(dir * acceleration, maxAcceleration);
        Vector2 f = a * rb.mass;
        rb.AddForce(f, ForceMode2D.Force);

        if (applySymmetrically && otherRb && GetInstanceID() < otherRb.GetInstanceID())
        {
            Vector2 fOther = (-a) * otherRb.mass;
            otherRb.AddForce(fOther, ForceMode2D.Force);
        }
    }
}
