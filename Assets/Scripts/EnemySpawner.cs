using System.Collections.Generic;
using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    public GameObject enemyPrefab;
    public GameObject bossPrefab;
    public float spawnInterval = 1.15f;
    public float spawnRadius = 12f;
    public Transform player;
    public float spawnTimer;
    public float stageElapsed;
    public float bossTime = 255f;
    public int stageIndex = 1;
    public int maxActiveEnemies = 72;
    public float cleanupInterval = 0.5f;
    public float cleanupTimer;
    public List<Enemy> trackedEnemies = new List<Enemy>();
    public bool bossSpawned;
    public bool bossDefeated;
    public EnemyKind lastSpawnedKind;

    public void Start()
    {
        if (player == null)
        {
            PlayerController controller = FindAnyObjectByType<PlayerController>();
            if (controller != null) player = controller.transform;
        }

        if (GameManager.Instance != null)
            stageIndex = Mathf.Clamp(GameManager.Instance.selectedLevel, 1, 3);

        // Keep the live object count bounded. Difficulty comes from cadence and stats.
        // More bodies create the mowing-game rhythm while the registry keeps searches cheap.
        maxActiveEnemies = 72 + stageIndex * 16;
        cleanupTimer = cleanupInterval;
        spawnTimer = 0.6f;
    }

    public void Update()
    {
        if (Time.timeScale <= 0f || enemyPrefab == null || player == null) return;

        if (GameManager.Instance != null)
            stageElapsed = GameManager.Instance.elapsedTime;

        if (!bossSpawned && stageElapsed >= bossTime)
            SpawnBoss();

        cleanupTimer -= Time.deltaTime;
        if (cleanupTimer <= 0f)
        {
            cleanupTimer = cleanupInterval;
            CleanupTrackedEnemies();
        }

        if (trackedEnemies.Count >= maxActiveEnemies)
            return;

        spawnTimer -= Time.deltaTime;
        if (spawnTimer <= 0f)
        {
            spawnTimer = GetSpawnInterval();
            SpawnEnemy();
        }
    }

    public float GetSpawnInterval()
    {
        float levelPressure = (stageIndex - 1) * 0.18f;
        float timelinePressure = Mathf.Clamp01(stageElapsed / 300f) * 0.55f;
        return Mathf.Max(0.55f, spawnInterval - levelPressure - timelinePressure);
    }

    public void SpawnEnemy()
    {
        SpawnSpecific(ChooseKind(), GetSpawnPosition());
    }

    public EnemyKind ChooseKind()
    {
        float roll = Random.value;

        if (stageIndex == 1)
        {
            if (stageElapsed < 90f) return EnemyKind.CellShadow;
            if (stageElapsed < 180f)
                return roll < 0.72f ? EnemyKind.CellShadow : EnemyKind.SpikeSoldier;
            if (stageElapsed < bossTime)
            {
                if (roll < 0.42f) return EnemyKind.CellShadow;
                if (roll < 0.66f) return EnemyKind.SpikeSoldier;
                if (roll < 0.86f) return EnemyKind.RupturedCell;
                if (roll < 0.95f) return EnemyKind.MicroThrombus;
                return EnemyKind.BleedingGap;
            }
        }
        else if (stageIndex == 2)
        {
            if (stageElapsed < 90f)
                return roll < 0.6f ? EnemyKind.CellShadow : EnemyKind.SpikeSoldier;
            if (stageElapsed < 180f)
                return roll < 0.35f ? EnemyKind.CellShadow : (roll < 0.72f ? EnemyKind.SpikeSoldier : EnemyKind.MicroThrombus);
            if (stageElapsed < bossTime)
            {
                if (roll < 0.25f) return EnemyKind.CellShadow;
                if (roll < 0.52f) return EnemyKind.SpikeSoldier;
                if (roll < 0.8f) return EnemyKind.MicroThrombus;
                if (roll < 0.94f) return EnemyKind.RupturedCell;
                return EnemyKind.BleedingGap;
            }
        }
        else
        {
            if (stageElapsed < 90f)
                return roll < 0.45f ? EnemyKind.CellShadow : EnemyKind.SpikeSoldier;
            if (stageElapsed < 180f)
                return roll < 0.25f ? EnemyKind.CellShadow : (roll < 0.62f ? EnemyKind.RupturedCell : EnemyKind.MicroThrombus);
            if (stageElapsed < bossTime)
            {
                if (roll < 0.22f) return EnemyKind.SpikeSoldier;
                if (roll < 0.5f) return EnemyKind.RupturedCell;
                if (roll < 0.78f) return EnemyKind.MicroThrombus;
                return EnemyKind.BleedingGap;
            }
        }

        return EnemyKind.CellShadow;
    }

    public Vector3 GetSpawnPosition()
    {
        float angle = Random.Range(0f, Mathf.PI * 2f);
        Vector3 offset = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * spawnRadius;
        return player.position + offset;
    }

    public GameObject SpawnSpecific(EnemyKind kind, Vector3 position)
    {
        if (enemyPrefab == null) return null;
        GameObject spawned = Instantiate(enemyPrefab, position, Quaternion.identity);
        ConfigureSpawnedEnemy(spawned, kind);
        return spawned;
    }

    public void ConfigureSpawnedEnemy(GameObject spawned, EnemyKind kind)
    {
        if (spawned == null) return;
        Enemy enemy = spawned.GetComponent<Enemy>();
        if (enemy == null) enemy = spawned.AddComponent<Enemy>();
        enemy.player = player;
        EnemyArchetype archetype = spawned.GetComponent<EnemyArchetype>();
        if (archetype == null) archetype = spawned.AddComponent<EnemyArchetype>();
        archetype.Configure(kind, stageIndex);
        ApplyPressureDifficulty(enemy);
        if (!trackedEnemies.Contains(enemy))
        {
            trackedEnemies.Add(enemy);
            enemy.OnDied += HandleTrackedEnemyDied;
        }
        lastSpawnedKind = kind;
    }

    public void ApplyPressureDifficulty(Enemy enemy)
    {
        if (enemy == null) return;
        float timePressure = Mathf.Clamp01(stageElapsed / bossTime);
        float stagePressure = (stageIndex - 1) * 0.12f;
        float hpScale = 1f + timePressure * 0.4f + stagePressure;
        enemy.maxHP = Mathf.Max(1, Mathf.CeilToInt(enemy.maxHP * hpScale));
        enemy.currentHP = enemy.maxHP;
        enemy.moveSpeed *= 1f + timePressure * 0.1f + stagePressure * 0.15f;
        if (timePressure > 0.75f && enemy.contactDamage >= 2)
            enemy.contactDamage += stageIndex >= 3 ? 1 : 0;

        // Keep early upgrades exciting, then taper orb value as the five-minute run advances.
        float lateGame = Mathf.Clamp01((stageElapsed - 120f) / 180f);
        float experienceMultiplier = Mathf.Lerp(1f, 0.65f, lateGame);
        enemy.xpValue = Mathf.Max(1, Mathf.RoundToInt(enemy.xpValue * experienceMultiplier));
    }

    public void HandleTrackedEnemyDied(Enemy deadEnemy)
    {
        if (deadEnemy == null) return;
        deadEnemy.OnDied -= HandleTrackedEnemyDied;
        trackedEnemies.Remove(deadEnemy);
    }

    public void CleanupTrackedEnemies()
    {
        for (int i = trackedEnemies.Count - 1; i >= 0; i--)
        {
            Enemy enemy = trackedEnemies[i];
            if (enemy != null && enemy.currentHP > 0) continue;
            if (enemy != null) enemy.OnDied -= HandleTrackedEnemyDied;
            trackedEnemies.RemoveAt(i);
        }
    }

    public void SpawnBoss()
    {
        if (bossSpawned || player == null) return;
        bossSpawned = true;
        GameObject prefab = bossPrefab == null ? enemyPrefab : bossPrefab;
        if (prefab == null) return;

        Vector3 position = player.position + Vector3.up * spawnRadius;
        GameObject bossObject = Instantiate(prefab, position, Quaternion.identity);
        ConfigureSpawnedEnemy(bossObject, EnemyKind.NodeBoss);
        VesselNodeBoss boss = bossObject.GetComponent<VesselNodeBoss>();
        if (boss == null) boss = bossObject.AddComponent<VesselNodeBoss>();
        boss.spawner = this;
        boss.player = player;
    }

    public void NotifyBossDefeated()
    {
        bossDefeated = true;
    }

    public void OnDestroy()
    {
        for (int i = 0; i < trackedEnemies.Count; i++)
        {
            if (trackedEnemies[i] != null) trackedEnemies[i].OnDied -= HandleTrackedEnemyDied;
        }
        trackedEnemies.Clear();
    }
}
// wave definitions imported
