using System.Collections.Generic;
using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    public GameObject enemyPrefab;
    public GameObject bossPrefab;
    public float spawnInterval = 1.0f;
    public float spawnRadius = 12f;
    public Transform player;
    public float spawnTimer;
    public float stageElapsed;
    public float bossTime = 255f;
    public int stageIndex = 1;
    public int maxActiveEnemies = 74;
    public float cleanupInterval = 0.5f;
    public float cleanupTimer;
    public List<Enemy> trackedEnemies = new List<Enemy>();
    public int spikeClusterSize = 3;
    public float spikeClusterRadius = 0.7f;
    public bool bossSpawned;
    public bool bossDefeated;
    public EnemyKind lastSpawnedKind;
    public float surgeInterval = 25f;
    public float surgeTimer = 20f;
    public int packSizeMin = 2;
    public int packSizeMax = 4;

    public void Start()
    {
        spawnRadius = GameBalance.Get("spawn.radius", spawnRadius);
        bossTime = GameBalance.Get("spawn.boss_time", bossTime);
        cleanupInterval = GameBalance.Get("spawn.cleanup_interval", cleanupInterval);
        spikeClusterSize = GameBalance.GetInt("spawn.spike_batch", spikeClusterSize);
        surgeInterval = GameBalance.Get("spawn.surge_interval", surgeInterval);
        surgeTimer = GameBalance.Get("spawn.surge_first", surgeTimer);
        packSizeMin = GameBalance.GetInt("spawn.batch_min", packSizeMin);
        packSizeMax = GameBalance.GetInt("spawn.batch_max", packSizeMax);
        if (player == null)
        {
            PlayerController controller = FindAnyObjectByType<PlayerController>();
            if (controller != null) player = controller.transform;
        }

        if (GameManager.Instance != null)
            stageIndex = Mathf.Clamp(GameManager.Instance.selectedLevel, 1, 3);

        // Keep the live object count bounded. Difficulty comes from cadence and stats.
        // More bodies create the mowing-game rhythm while the registry keeps searches cheap.
        maxActiveEnemies = GameBalance.GetInt("spawn.max_active_base", 74) +
            stageIndex * GameBalance.GetInt("spawn.max_active_per_stage", 16);
        cleanupTimer = cleanupInterval;
        spawnTimer = GameBalance.Get("spawn.first_delay", 0.6f);
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

        surgeTimer -= Time.deltaTime;
        if (surgeTimer <= 0f)
        {
            surgeTimer = surgeInterval;
            SpawnSurgeWave();
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
        float levelPressure = (stageIndex - 1) * GameBalance.Get("spawn.interval_stage_factor", 0.18f);
        float timelinePressure = Mathf.Clamp01(stageElapsed / GameBalance.Get("spawn.interval_ramp_time", 300f)) *
            GameBalance.Get("spawn.interval_time_factor", 0.6f);
        return Mathf.Max(GameBalance.Get("spawn.interval_min", 0.4f), spawnInterval - levelPressure - timelinePressure);
    }

    public void SpawnEnemy()
    {
        EnemyKind kind = ChooseKind();
        Vector3 position = GetSpawnPosition();
        // Spawn in small packs so the field feels alive instead of a sparse trickle.
        int packSize = kind == EnemyKind.SpikeSoldier ? spikeClusterSize : Random.Range(packSizeMin, packSizeMax + 1);
        for (int i = 0; i < packSize && trackedEnemies.Count < maxActiveEnemies; i++)
        {
            Vector2 offset = i == 0 ? Vector2.zero : Random.insideUnitCircle * GameBalance.Get("spawn.batch_offset", 1.2f);
            SpawnSpecific(kind, position + (Vector3)offset);
        }
    }

    public void SpawnSurgeWave()
    {
        if (player == null) return;
        // A directional flood from one side — the classic survivor-genre pressure spike.
        int count = Random.Range(GameBalance.GetInt("spawn.surge_count_min", 8),
            GameBalance.GetInt("spawn.surge_count_base", 15) + stageIndex * GameBalance.GetInt("spawn.surge_count_per_stage", 2));
        float baseAngle = Random.Range(0f, Mathf.PI * 2f);
        for (int i = 0; i < count && trackedEnemies.Count < maxActiveEnemies; i++)
        {
            float surgeAngle = GameBalance.Get("spawn.surge_angle", 0.55f);
            float angle = baseAngle + Random.Range(-surgeAngle, surgeAngle);
            float radius = spawnRadius + Random.Range(0f, GameBalance.Get("spawn.surge_distance_max", 3.5f));
            Vector3 position = player.position + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius;
            SpawnSpecific(ChooseKind(), position);
        }
    }

    public EnemyKind ChooseKind()
    {
        if (stageElapsed >= bossTime)
            return ChooseWeightedKind("spawn.comp.post_boss.", Random.value, 100, 0, 0, 0, 0);

        float firstEnd = GameBalance.Get("spawn.comp.s" + stageIndex + "_t1", stageIndex == 1 ? 60f : 90f);
        float secondEnd = GameBalance.Get("spawn.comp.s" + stageIndex + "_t2", stageIndex == 1 ? 150f : 180f);
        int phase = stageElapsed < firstEnd ? 1 : (stageElapsed < secondEnd ? 2 : 3);
        string prefix = "spawn.comp.s" + stageIndex + "_p" + phase + ".";
        float roll = Random.value;
        switch (stageIndex * 10 + phase)
        {
            case 11: return ChooseWeightedKind(prefix, roll, 100, 0, 0, 0, 0);
            case 12: return ChooseWeightedKind(prefix, roll, 70, 30, 0, 0, 0);
            case 13: return ChooseWeightedKind(prefix, roll, 32, 20, 21, 13, 14);
            case 21: return ChooseWeightedKind(prefix, roll, 60, 40, 0, 0, 0);
            case 22: return ChooseWeightedKind(prefix, roll, 35, 37, 0, 28, 0);
            case 23: return ChooseWeightedKind(prefix, roll, 20, 25, 14, 25, 16);
            case 31: return ChooseWeightedKind(prefix, roll, 45, 55, 0, 0, 0);
            case 32: return ChooseWeightedKind(prefix, roll, 25, 0, 37, 38, 0);
            default: return ChooseWeightedKind(prefix, roll, 0, 18, 24, 26, 32);
        }
    }

    public EnemyKind ChooseWeightedKind(string prefix, float roll, int cell, int spike, int ruptured, int thrombus, int bleeding)
    {
        float[] weights = {
            GameBalance.Get(prefix + "cell", cell), GameBalance.Get(prefix + "spike", spike),
            GameBalance.Get(prefix + "ruptured", ruptured), GameBalance.Get(prefix + "thrombus", thrombus),
            GameBalance.Get(prefix + "bleeding", bleeding)
        };
        EnemyKind[] kinds = { EnemyKind.CellShadow, EnemyKind.SpikeSoldier, EnemyKind.RupturedCell,
            EnemyKind.MicroThrombus, EnemyKind.BleedingGap };
        float total = 0f;
        for (int i = 0; i < weights.Length; i++) total += Mathf.Max(0f, weights[i]);
        if (total <= 0f) return EnemyKind.CellShadow;
        float cumulative = 0f;
        for (int i = 0; i < weights.Length; i++)
        {
            cumulative += Mathf.Max(0f, weights[i]);
            if (roll < cumulative / total) return kinds[i];
        }
        for (int i = weights.Length - 1; i >= 0; i--)
            if (weights[i] > 0f) return kinds[i];
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
        MonsterSurvivorArtLibrary.ApplyEnemy(spawned, kind);
        // Fit the collider after the final scale is settled, independent of whether
        // the procedural art loaded (ApplyEnemy returns early when a sheet is missing).
        MonsterSurvivorArtLibrary.FitEnemyBodyCollider(spawned, kind);
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
        float stagePressure = (stageIndex - 1) * GameBalance.Get("spawn.stage_pressure", 0.12f);
        float hpScale = 1f + timePressure * GameBalance.Get("spawn.hp_time_factor", 0.4f) + stagePressure;
        enemy.maxHP = Mathf.Max(1, Mathf.CeilToInt(enemy.maxHP * hpScale));
        enemy.currentHP = enemy.maxHP;
        enemy.moveSpeed *= 1f + timePressure * GameBalance.Get("spawn.speed_time_factor", 0.1f) +
            stagePressure * GameBalance.Get("spawn.speed_stage_factor", 0.15f);
        // Late-stage damage buff only applies to hits at or above the migrated
        // 40-damage threshold, so 20-damage cell shadows are never reinforced.
        if (timePressure > GameBalance.Get("spawn.late_damage_threshold", 0.75f) &&
            enemy.contactDamage >= GameBalance.GetInt("spawn.late_damage_min", 40))
            enemy.contactDamage += stageIndex >= 3 ? GameBalance.GetInt("spawn.late_damage_stage3_add", 20) : 0;

        // Keep early upgrades exciting, then taper orb value as the five-minute run advances.
        float lateGame = Mathf.Clamp01((stageElapsed - GameBalance.Get("spawn.xp_decay_start", 120f)) /
            GameBalance.Get("spawn.xp_decay_duration", 180f));
        float experienceMultiplier = Mathf.Lerp(1f, GameBalance.Get("spawn.xp_decay_min", 0.65f), lateGame);
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
        if (GameManager.Instance != null) GameManager.Instance.HandleBossDefeated();
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


