using System.Collections.Generic;
using UnityEngine;

public class Bullet : MonoBehaviour
{
    public int damage = 1;
    public float knockbackForce = 0.12f;
    public bool hasHit;
    public int maxTargets = 1;
    public CharacterCombat source;
    public bool pixelNeedle;
    [System.NonSerialized] public HashSet<Enemy> hitEnemies = new HashSet<Enemy>();
    SpriteRenderer needleRenderer;
    float needleTimer;

    void Update()
    {
        if (!pixelNeedle || Time.timeScale <= 0f) return;
        if (needleRenderer == null) needleRenderer = GetComponent<SpriteRenderer>();
        if (needleRenderer == null) return;
        needleTimer += Time.deltaTime;
        needleRenderer.sprite = MonsterSurvivorArtLibrary.LoadVfx(
            Mathf.FloorToInt(needleTimer / 0.08f) % 2 == 0 ? "vfx_needle_f0" : "vfx_needle_f1");
    }

    void Awake()
    {
        knockbackForce = GameBalance.Get("attack.bullet_knockback", knockbackForce);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (hasHit || Time.timeScale <= 0f) return;
        Enemy enemy = other.GetComponent<Enemy>();
        if (enemy == null) enemy = other.GetComponentInParent<Enemy>();

        if (enemy != null && enemy.currentHP > 0 && !enemy.deathPending && hitEnemies.Add(enemy))
        {
            hasHit = hitEnemies.Count >= Mathf.Max(1, maxTargets);
            Rigidbody2D bulletBody = GetComponent<Rigidbody2D>();
            Vector2 direction = bulletBody == null ? Vector2.zero : bulletBody.linearVelocity;
            enemy.ApplyKnockback(direction, knockbackForce);
            enemy.TakeDamage(damage);
            if (source != null)
            {
                source.TrySidewallHeal();
                source.ApplySpringCoil(enemy);
            }
            if (hasHit) Destroy(gameObject);
        }
    }
}

