using UnityEngine;

public class Bullet : MonoBehaviour
{
    public int damage = 1;
    public float knockbackForce = 0.12f;
    public bool hasHit;

    void OnTriggerEnter2D(Collider2D other)
    {
        if (hasHit || Time.timeScale <= 0f) return;
        Enemy enemy = other.GetComponent<Enemy>();
        if (enemy == null) enemy = other.GetComponentInParent<Enemy>();

        if (enemy != null)
        {
            hasHit = true;
            Rigidbody2D bulletBody = GetComponent<Rigidbody2D>();
            Vector2 direction = bulletBody == null ? Vector2.zero : bulletBody.linearVelocity;
            enemy.ApplyKnockback(direction, knockbackForce);
            enemy.TakeDamage(damage);
            Destroy(gameObject);
        }
    }
}

