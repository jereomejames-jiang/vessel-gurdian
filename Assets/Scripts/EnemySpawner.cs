using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    public GameObject enemyPrefab;
    public GameObject bossPrefab;
    public float spawnInterval = 1.0f;
    public float spawnRadius = 12f;
    [Range(0f, 1f)] public float horizontalSideSpawnWeight = 0.78f;
    public float sideSpawnAngleSpread = 0.38f;
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
    public BossArenaController arena;

    public void Start()
    {
        spawnRadius = GameBalance.Get("spawn.radius", spawnRadius);
        horizontalSideSpawnWeight = GameBalance.Get("spawn.horizontal_side_weight", horizontalSideSpawnWeight);
        sideSpawnAngleSpread = GameBalance.Get("spawn.side_angle_spread", sideSpawnAngleSpread);
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
        maxActiveEnemies = GameBalance.GetInt("spawn.max_active_base", 40) +
            stageIndex * GameBalance.GetInt("spawn.max_active_per_stage", 8);
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

        // Boss 竞技场是封闭单挑空间：Boss 存活期间不再刷常规怪与涌浪。
        if (bossSpawned) return;

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
        Vector2 baseDirection = GetSpawnDirection();
        float baseAngle = Mathf.Atan2(baseDirection.y, baseDirection.x);
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
        float bossTime = GameBalance.Get("game.boss_time", 270f);

        if (stageElapsed >= bossTime)
            return ChooseWeightedKind("spawn.comp.post_boss.", Random.value, 100, 0, 0, 0, 0, 0, 0, 0, 0);

        float firstEnd = GameBalance.Get("spawn.comp.s" + stageIndex + "_t1", 90f);
        float secondEnd = GameBalance.Get("spawn.comp.s" + stageIndex + "_t2", 180f);
        int phase = stageElapsed < firstEnd ? 1 : (stageElapsed < secondEnd ? 2 : 3);
        string prefix = "spawn.comp.s" + stageIndex + "_p" + phase + ".";
        float roll = Random.value;

        switch (stageIndex * 10 + phase)
        {
            case 11: return ChooseWeightedKind(prefix, roll, 100, 0, 0, 0, 0, 0, 0, 0, 0);
            case 12: return ChooseWeightedKind(prefix, roll, 60, 25, 15, 0, 0, 0, 0, 0, 0);
            case 13: return ChooseWeightedKind(prefix, roll, 30, 20, 20, 15, 15, 0, 0, 0, 0);
            case 21: return ChooseWeightedKind(prefix, roll, 0, 0, 0, 0, 0, 60, 40, 0, 0);
            case 22: return ChooseWeightedKind(prefix, roll, 0, 0, 0, 0, 0, 35, 30, 35, 0);
            case 23: return ChooseWeightedKind(prefix, roll, 0, 0, 0, 0, 0, 25, 20, 30, 25);
            case 31: return ChooseWeightedKind(prefix, roll, 0, 50, 0, 30, 20, 0, 0, 0, 0);
            case 32: return ChooseWeightedKind(prefix, roll, 0, 30, 25, 25, 0, 0, 0, 0, 20);
            default: return ChooseWeightedKind(prefix, roll, 0, 20, 25, 25, 15, 0, 0, 0, 15);
        }
    }

    public EnemyKind ChooseWeightedKind(string prefix, float roll, int cell, int spike, int ruptured, int thrombus, int bleeding, int stasis, int fibrin, int primitive, int blocker)
    {
        float[] weights = {
            GameBalance.Get(prefix + "cell", cell), GameBalance.Get(prefix + "spike", spike),
            GameBalance.Get(prefix + "ruptured", ruptured), GameBalance.Get(prefix + "thrombus", thrombus),
            GameBalance.Get(prefix + "bleeding", bleeding), GameBalance.Get(prefix + "stasis", stasis),
            GameBalance.Get(prefix + "fibrin", fibrin), GameBalance.Get(prefix + "primitive", primitive),
            GameBalance.Get(prefix + "blocker", blocker)
        };
        EnemyKind[] kinds = { EnemyKind.CellShadow, EnemyKind.SpikeSoldier, EnemyKind.RupturedCell,
            EnemyKind.MicroThrombus, EnemyKind.BleedingGap, EnemyKind.VenousStasis,
            EnemyKind.FibrinNet, EnemyKind.PrimitiveCell, EnemyKind.TargetBlocker };
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
        Vector2 direction = GetSpawnDirection();
        Vector3 offset = new Vector3(direction.x, direction.y, 0f) * spawnRadius;
        return player.position + offset;
    }

    public Vector2 GetSpawnDirection()
    {
        float angle;
        if (Random.value < horizontalSideSpawnWeight)
        {
            float side = Random.value < 0.5f ? 0f : Mathf.PI;
            angle = side + Random.Range(-sideSpawnAngleSpread, sideSpawnAngleSpread);
        }
        else
        {
            float side = Random.value < 0.5f ? Mathf.PI * 0.5f : Mathf.PI * 1.5f;
            angle = side + Random.Range(-sideSpawnAngleSpread, sideSpawnAngleSpread);
        }
        return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
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
        if (timePressure > GameBalance.Get("spawn.late_damage_threshold", 0.75f) && enemy.contactDamage >= 2)
            enemy.contactDamage += stageIndex >= 3 ? GameBalance.GetInt("spawn.late_damage_stage3_add", 1) : 0;

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
        StartCoroutine(BossIntroRoutine());
    }

    IEnumerator BossIntroRoutine()
    {
        GameObject prefab = bossPrefab == null ? enemyPrefab : bossPrefab;
        if (prefab == null) yield break;

        if (arena == null) arena = GetComponent<BossArenaController>();
        if (arena == null) arena = gameObject.AddComponent<BossArenaController>();
        LevelUpManager levels = FindAnyObjectByType<LevelUpManager>();
        if (levels != null) levels.deferChoices = true;

        // 1) 全场时停（用真实时间计 2 秒，不受 timeScale 影响）。
        Time.timeScale = 0f;
        string bossName = stageIndex == 2 ? "曲张美杜莎" : "葡萄酒样色斑";
        arena.ShowBanner("节点守护者 · " + bossName + " 来袭");

        // 2) 以摄像机视野为准计算竞技场范围。
        Camera cam = Camera.main != null ? Camera.main : FindAnyObjectByType<Camera>();
        float rawHalfH = cam != null ? cam.orthographicSize : 7.2f;
        float aspect = cam != null ? cam.aspect : 16f / 9f;
        float margin = GameBalance.Get("boss.arena_margin", 0.95f);
        Vector2 arenaCenter = player.position;
        Vector2 halfExtents = new Vector2(rawHalfH * aspect * margin, rawHalfH * margin);

        // 3) 清除全场小怪与残留危险区，并吸入所有经验球。
        ClearAllEnemies();
        ExperienceOrb[] orbs = FindObjectsByType<ExperienceOrb>(FindObjectsSortMode.None);
        for (int i = 0; i < orbs.Length; i++) if (orbs[i] != null) orbs[i].CollectImmediate();

        yield return new WaitForSecondsRealtime(GameBalance.Get("boss.intro_freeze", 2f));

        // 4) 在竞技场内部生成 Boss（玩家上方），随后封墙锁定摄像机。
        Vector3 bossPos = arenaCenter + Vector2.up * (halfExtents.y * 0.55f);
        GameObject bossObject = Instantiate(prefab, bossPos, Quaternion.identity);
        ConfigureSpawnedEnemy(bossObject, EnemyKind.NodeBoss);
        VesselNodeBoss boss = bossObject.GetComponent<VesselNodeBoss>();
        if (boss == null) boss = bossObject.AddComponent<VesselNodeBoss>();
        boss.spawner = this;
        boss.player = player;

        arena.BuildArena(arenaCenter, halfExtents, rawHalfH, player, boss);
        arena.HideBanner();

        // 5) 演出期间累计的升级此时再弹卡；无升级则直接恢复战斗。
        if (levels != null)
        {
            levels.deferChoices = false;
            levels.ResumeDeferredChoices();
        }
        if ((levels == null || !levels.isChoosing) &&
            (GameManager.Instance == null || !GameManager.Instance.gameEnded))
            Time.timeScale = 1f;
    }

    public void ClearAllEnemies()
    {
        for (int i = trackedEnemies.Count - 1; i >= 0; i--)
        {
            Enemy enemy = trackedEnemies[i];
            if (enemy != null)
            {
                enemy.OnDied -= HandleTrackedEnemyDied;
                Destroy(enemy.gameObject);
            }
            trackedEnemies.RemoveAt(i);
        }
        VesselHazardZone[] hazards = FindObjectsByType<VesselHazardZone>(FindObjectsSortMode.None);
        for (int i = 0; i < hazards.Length; i++) if (hazards[i] != null) Destroy(hazards[i].gameObject);
    }

    public void NotifyBossDefeated()
    {
        bossDefeated = true;
        if (arena != null) arena.Release();
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


