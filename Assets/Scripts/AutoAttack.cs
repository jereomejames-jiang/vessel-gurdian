using System.Collections.Generic;
using UnityEngine;

public class AutoAttack : MonoBehaviour
{
    public float attackInterval = 0.5f;
    public float bulletSpeed = 10f;
    public float bulletLifetime = 2f;
    public int bulletDamage = 1;
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
            // 快速处置（v3 §7）：普攻速度 +20%/级 → 间隔除以倍率。
            float rapid = characterCombat == null ? 1f : characterCombat.BasicAttackSpeedMult();
            // R1-08 替换语义：微量输液泵进化后护士基础攻击改为极短冷却持续发射（F58）。
            float interval = attackInterval * (characterCombat == null ? 1f : characterCombat.IntervalMultiplier());
            if (characterCombat != null) interval = characterCombat.EffectiveAttackInterval(interval);
            attackTimer = interval / Mathf.Max(0.1f, rapid);
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

    public GameObject SpawnBullet(Vector2 direction, int maxTargets, Vector3 scale)
    {
        return SpawnBullet(direction, maxTargets, scale, 1f);
    }

    // 进修培训：整体伤害倍率；深耕科研：弹速倍率（v3 §7）。extraSpeedMult 供吻合钉枪等高速弹体使用。
    public GameObject SpawnBullet(Vector2 direction, int maxTargets, Vector3 scale, float extraSpeedMult)
    {
        if (bulletPrefab == null) return null;
        Quaternion rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f);
        GameObject projectile = Instantiate(bulletPrefab, transform.position, rotation);
        projectile.name = "NurseNeedle";
        projectile.transform.localScale = scale;
        float speedMult = characterCombat == null ? 1f : characterCombat.BulletSpeedMult();
        int damage = characterCombat == null ? bulletDamage : characterCombat.D(bulletDamage);
        Rigidbody2D body = projectile.GetComponent<Rigidbody2D>();
        if (body != null) body.linearVelocity = direction * bulletSpeed * speedMult * extraSpeedMult;
        Bullet bullet = projectile.GetComponent<Bullet>();
        if (bullet != null) { bullet.damage = damage; bullet.maxTargets = maxTargets; bullet.source = characterCombat; }
        Destroy(projectile, bulletLifetime);
        return projectile;
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
