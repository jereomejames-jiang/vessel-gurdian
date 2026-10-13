using UnityEngine;

public class Bullet : MonoBehaviour
{
    public int damage = 1;
    public bool hasHit;

    void OnTriggerEnter2D(Collider2D other)
    {
        if (hasHit || Time.timeScale <= 0f) return;
        Enemy enemy = other.GetComponent<Enemy>();
        if (enemy == null) enemy = other.GetComponentInParent<Enemy>();

        if (enemy != null)
        {
            hasHit = true;
            enemy.TakeDamage(damage);
            Destroy(gameObject);
        }
    }
}
