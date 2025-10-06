using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class Point : MonoBehaviour
{
    [Tooltip("反発係数 e。2 にすると法線成分の速さが2倍で反発（超弾性）")]
    [Range(0f, 10f)] public float restitution = 2f;

    void OnCollisionEnter2D(Collision2D col)
    {
        //if (!ShouldApply(col)) return;
        ApplyImpulse(col);
    }

    bool ShouldApply(Collision2D col)
    {
        var rbA = GetComponent<Rigidbody2D>();
        var rbB = col.rigidbody;

        bool aDyn = rbA && rbA.bodyType == RigidbodyType2D.Dynamic;
        bool bDyn = rbB && rbB.bodyType == RigidbodyType2D.Dynamic;
        if (!aDyn && !bDyn) return false;
        if (aDyn && !bDyn) return true;
        if (!aDyn && bDyn) return false;

        return rbA.GetInstanceID() < rbB.GetInstanceID();
    }

    void ApplyImpulse(Collision2D col)
    {
        var rbA = GetComponent<Rigidbody2D>();
        var rbB = col.rigidbody;

        Vector2 n = col.GetContact(0).normal;

        Vector2 vA = rbA ? rbA.linearVelocity : Vector2.zero;
        Vector2 vB = rbB ? rbB.linearVelocity : Vector2.zero;

        float vrn = Vector2.Dot(vA - vB, n);
        if (vrn >= 0f) return;

        float invMassA = (rbA && rbA.bodyType == RigidbodyType2D.Dynamic) ? 1f / rbA.mass : 0f;
        float invMassB = (rbB && rbB.bodyType == RigidbodyType2D.Dynamic) ? 1f / rbB.mass : 0f;
        float denom = invMassA + invMassB;
        if (denom <= 0f) return;

        float J = -(1f + restitution) * vrn / denom;
        Vector2 impulse = J * n;

        if (invMassA > 0f) rbA.linearVelocity = vA + impulse * invMassA;
        if (invMassB > 0f) rbB.linearVelocity = vB - impulse * invMassB;
    }
}
