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

    public float attackTimer;

    void Start()
    {
        enemySpawner = FindAnyObjectByType<EnemySpawner>();
    }

    void Update()
    {
        if (Time.timeScale <= 0f || (GameManager.Instance != null && GameManager.Instance.gameEnded)) return;
        attackTimer -= Time.deltaTime;
        if (attackTimer <= 0f)
        {
            attackTimer = attackInterval;
            FireAtNearestEnemy();
        }
    }

    public void FireAtNearestEnemy()
    {
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
        if (bullet != null) bullet.damage = bulletDamage;

        Destroy(bulletObject, bulletLifetime);
    }

    public void IncreaseDamage(int amount)
    {
        bulletDamage += amount;
    }

    public void ReduceInterval(float amount)
    {
        attackInterval = Mathf.Max(0.05f, attackInterval * (1f - amount));
    }

    public void IncreaseBulletSpeed(float amount)
    {
        bulletSpeed *= 1f + amount;
    }
}
