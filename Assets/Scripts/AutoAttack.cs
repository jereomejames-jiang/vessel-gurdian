using System.Collections.Generic;
using UnityEngine;

public class AutoAttack : MonoBehaviour
{
    public float attackInterval = 0.5f;
    public float bulletSpeed = 10f;
    public float bulletLifetime = 2f;
    public int bulletDamage = 10;
    public GameObject bulletPrefab;
    public EnemySpawner enemySpawner;
    public CharacterCombat characterCombat;

    public float attackTimer;

    void Start()
    {
        attackInterval = GameBalance.Get("attack.interval", attackInterval);
        bulletSpeed = GameBalance.Get("attack.bullet_speed", bulletSpeed);
        bulletLifetime = GameBalance.Get("attack.bullet_lifetime", bulletLifetime);
        bulletDamage = GameBalance.GetInt("attack.bullet_damage", bulletDamage);
        enemySpawner = FindAnyObjectByType<EnemySpawner>();
        characterCombat = GetComponent<CharacterCombat>();
        if (characterCombat == null) characterCombat = gameObject.AddComponent<CharacterCombat>();
        characterCombat.Initialize(CharacterCatalog.SelectedRole);
    }

    void Update()
    {
        if (Time.timeScale <= 0f || (GameManager.Instance != null && GameManager.Instance.gameEnded)) return;
        attackTimer -= Time.deltaTime;
        if (attackTimer <= 0f)
        {
            attackTimer = attackInterval * (characterCombat == null ? 1f : characterCombat.IntervalMultiplier());
            FireAtNearestEnemy();
        }
    }

    public void FireAtNearestEnemy()
    {
        if (characterCombat != null && characterCombat.enabled)
        {
            characterCombat.Attack();
            return;
        }
        if (bulletPrefab == null) return;

        if (enemySpawner == null) enemySpawner = FindAnyObjectByType<EnemySpawner>();
        Enemy[] enemies = enemySpawner == null ? FindObjectsByType<Enemy>() : null;
        List<Enemy> trackedEnemies = enemySpawner == null ? null : enemySpawner.trackedEnemies;
        Enemy nearest = null;
        float nearestDistance = float.MaxValue;

        int enemyCount = trackedEnemies == null ? enemies.Length : trackedEnemies.Count;
        for (int i = 0; i < enemyCount; i++)
        {
            Enemy candidate = trackedEnemies == null ? enemies[i] : trackedEnemies[i];
            if (candidate == null || candidate.currentHP <= 0) continue;
            float distance = (candidate.transform.position - transform.position).sqrMagnitude;
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearest = candidate;
            }
        }

        if (nearest == null) return;

        Vector2 direction = (nearest.transform.position - transform.position).normalized;
        GameObject bulletObject = Instantiate(bulletPrefab, transform.position, Quaternion.identity);
        Rigidbody2D bulletBody = bulletObject.GetComponent<Rigidbody2D>();
        if (bulletBody != null) bulletBody.linearVelocity = direction * bulletSpeed;

        Bullet bullet = bulletObject.GetComponent<Bullet>();
        if (bullet != null) { bullet.damage = bulletDamage; bullet.source = characterCombat; }

        Destroy(bulletObject, bulletLifetime);
    }

    public void SpawnBullet(Vector2 direction, int maxTargets, Vector3 scale)
    {
        SpawnBullet(direction, maxTargets, scale, bulletDamage, bulletSpeed, bulletLifetime, "NurseNeedle");
    }

    // Custom projectile path for vessel skills (1010) that need their own damage,
    // speed, lifetime and penetration while reusing the existing bullet prefab.
    public void SpawnBullet(Vector2 direction, int maxTargets, Vector3 scale,
        int damage, float speed, float lifetime, string projectileName)
    {
        if (bulletPrefab == null) return;
        Quaternion rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f);
        GameObject projectile = Instantiate(bulletPrefab, transform.position, rotation);
        projectile.name = string.IsNullOrEmpty(projectileName) ? "SkillProjectile" : projectileName;
        projectile.transform.localScale = scale;
        Rigidbody2D body = projectile.GetComponent<Rigidbody2D>();
        if (body != null) body.linearVelocity = direction * speed;
        Bullet bullet = projectile.GetComponent<Bullet>();
        if (bullet != null)
        {
            bullet.damage = Mathf.Max(1, damage);
            bullet.maxTargets = Mathf.Max(1, maxTargets);
            bullet.source = characterCombat;
        }
        Destroy(projectile, Mathf.Max(0.1f, lifetime));
    }

    public void IncreaseDamage(int amount)
    {
        bulletDamage += amount;
    }

    public void ReduceInterval(float amount)
    {
        attackInterval = Mathf.Max(GameBalance.Get("attack.interval_min", 0.05f), attackInterval * (1f - amount));
    }

    public void IncreaseBulletSpeed(float amount)
    {
        bulletSpeed *= 1f + amount;
    }
}
