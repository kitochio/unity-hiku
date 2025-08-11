// Bullet2D.cs
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class Bullet2D : MonoBehaviour
{
    Rigidbody2D rb;

    [Header("Bullet")]
    public float lifetime = 5f;   // 何秒後に消すか

    float timer;

    void Awake() => rb = GetComponent<Rigidbody2D>();

    public void Launch(Vector2 velocity)
    {
        rb.linearVelocity = velocity;
        timer = lifetime;
    }

    void OnEnable() => timer = lifetime;

    void Update()
    {
        timer -= Time.deltaTime;
        if (timer <= 0f) Destroy(gameObject);
    }
}
