using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum BossType
{
    WineStain,        // Boss1: 葡萄酒样色斑 (毛细血管)
    VaricoseMedusa,   // Boss2: 曲张美杜莎 (静脉)
    AneurysmColossus  // Boss3: 动脉瘤巨像·赤冠 (动脉) —— 设定集(3) §3.3
}

// 设定集(3) §1.3 / §2.3 / §3.3 节点 Boss。
// 多招同时到点时依次施放、至少间隔 1.5s（规则写在 Boss2 瓣膜逆流 cell 末尾，按通用规则应用于三个 Boss，标旗）。
public class VesselNodeBoss : MonoBehaviour
{
    public BossType bossType = BossType.WineStain;
    public Enemy enemy;
    public EnemySpawner spawner;
    public Transform player;
    public SpriteRenderer spriteRenderer;

    [Header("Boss1 - 葡萄酒样色斑")]
    public float spreadInterval = 9f;
    public float spreadChargeTime = 1.5f;
    public int spreadCountMin = 2;
    public int spreadCountMax = 3;
    public float spreadLength = 4f;
    public int spreadDamage = 2;
    public float spreadSlowMult = 0.6f;
    public float spreadSlowDuration = 1.5f;

    public float chargeInterval = 12f;
    public float chargeWarning = 0.8f;
    public float chargeDuration = 1f;
    public float chargeSpeed = 8f;
    public float chargeTrailDuration = 20f;
    public int chargeTrailDamage = 1;

    public float plaqueInterval = 18f;
    public float plaqueWarning = 1f;
    public float plaqueBlastRadius = 2.5f;
    public int plaqueBlastDamage = 3;
    public int plaqueNodeHP = 5;

    [Header("Boss2 - 曲张美杜莎")]
    public float snakeInterval = 9f;
    public int snakePaths = 3;
    public float snakeWarning = 0.8f;
    public float snakeSweepDelay = 1.2f;
    public int snakeDamage = 20;
    public int snakeStasisStacks = 1;
    public float snakeTrailDuration = 3f;
    public float snakeTrailSlow = 0.5f;
    public int snakeRootHP = 5;

    public float gazeInterval = 12f;
    public float gazeWarning = 1f;
    public float gazeDuration = 1.2f;
    public int gazeStasisPerTick = 1;
    public int gazeRootStacks = 3;
    public float gazeRootDuration = 0.8f;
    public float gazeTick = 0.4f;
    public float gazeRange = 6f;

    public float valveInterval = 18f;
    public float valveWarning = 1f;
    public float valveDuration = 2.5f;
    public int valveNodeCount = 2;
    public int valveNodeHP = 5;
    public float valvePoolRadius = 1.8f;
    public float valvePoolSlow = 0.6f;
    public float valvePushSpeed = 2.5f;
    public float valvePushRadius = 4f;

    [Header("Boss3 - 动脉瘤巨像·赤冠")]
    public float ringInterval = 9f;
    public float ringTelegraph = 0.9f;
    public int ringDamage = 18;
    public float ringSpeed = 10f;
    public float ringGapAngle = 55f;
    public float ringBandWidth = 0.6f;
    public float ringMaxRadius = 14f;

    public float budInterval = 12f;
    public float budTelegraph = 1f;
    public int budCount = 2;
    public int budHP = 5;
    public int budDamage = 1;
    public float budSpeed = 14f;
    public float budLifetime = 3f;

    public float crackInterval = 18f;
    public float crackTelegraph = 1.2f;
    public int crackCount = 2;
    public float crackDuration = 2f;
    public int crackDamage = 6;
    public float crackTick = 0.5f;
    public float crackAngle = 60f;
    public float crackRange = 7f;

    [Header("Runtime State")]
    public float minCastInterval = 1.5f;
    public float[] abilityTimers = new float[3];
    public float[] abilityPeriods = new float[3];
    public float nextCastAt;

    public bool charging;
    public float chargeRemaining;
    public float warningRemaining;
    public Vector2 chargeDirection;
    public LineRenderer chargeTelegraphLine;
    public float chargeTrailDropTimer;

    public bool gazing;
    public float gazeRemaining;
    public float gazeWarningRemaining;
    public float gazeTickTimer;
    public Vector2 gazeDirection;
    public GameObject gazeVisual;

    public bool halfHealthTriggered;
    public bool defeated;

    public List<BossSpreadLine> activeSpreads = new List<BossSpreadLine>();
    public List<BossPlaque> activePlaques = new List<BossPlaque>();
    public List<BossSnakeHair> activeSnakes = new List<BossSnakeHair>();
    public List<BossValve> activeValves = new List<BossValve>();
    public List<BossPulseRing> activeRings = new List<BossPulseRing>();
    public List<BossBud> activeBuds = new List<BossBud>();
    public List<BossCrackJet> activeCracks = new List<BossCrackJet>();
    public List<VesselHazardZone> redZones = new List<VesselHazardZone>();
    public List<Enemy> bossNodes = new List<Enemy>();
    public List<GameObject> visualObjects = new List<GameObject>();

    public Color normalColor = new Color(0.28f, 0.06f, 0.34f, 1f);
    public Color warningColor = new Color(1f, 0.12f, 0.16f, 1f);
    public Color boss2Color = new Color(0.6f, 0.2f, 0.8f, 1f);
    public Color boss3Color = new Color(0.5f, 0.08f, 0.1f, 1f);
    public GameObject aneurysmSac;

    public void Awake()
    {
        if (enemy == null) enemy = GetComponent<Enemy>();
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        if (spawner == null) spawner = FindAnyObjectByType<EnemySpawner>();
        if (player == null)
        {
            PlayerController controller = FindAnyObjectByType<PlayerController>();
            if (controller != null) player = controller.transform;
        }
        if (enemy != null) enemy.externalMovement = true;
        if (enemy != null) enemy.OnDied += HandleEnemyDied;

        // 关卡1=葡萄酒样色斑，关卡2=曲张美杜莎，关卡3=动脉瘤巨像·赤冠。
        if (spawner != null)
        {
            bossType = spawner.stageIndex == 3 ? BossType.AneurysmColossus :
                (spawner.stageIndex == 2 ? BossType.VaricoseMedusa : BossType.WineStain);
        }

        LoadBossConfig();
        ApplyBossStats();
    }

    void LoadBossConfig()
    {
        minCastInterval = GameBalance.Get("boss.min_cast_interval", minCastInterval);
        if (bossType == BossType.WineStain)
        {
            spreadInterval = GameBalance.Get("boss1.spread_interval", spreadInterval);
            spreadCountMin = GameBalance.GetInt("boss1.spread_count_min", spreadCountMin);
            spreadCountMax = GameBalance.GetInt("boss1.spread_count_max", spreadCountMax);
            spreadLength = GameBalance.Get("boss1.spread_length", spreadLength);
            spreadChargeTime = GameBalance.Get("boss1.spread_telegraph", spreadChargeTime);
            spreadDamage = GameBalance.GetInt("boss1.spread_damage", spreadDamage);
            spreadSlowMult = GameBalance.Get("boss1.spread_slow", spreadSlowMult);
            spreadSlowDuration = GameBalance.Get("boss1.spread_slow_duration", spreadSlowDuration);

            chargeInterval = GameBalance.Get("boss1.charge_interval", chargeInterval);
            chargeWarning = GameBalance.Get("boss1.charge_telegraph", chargeWarning);
            chargeSpeed = GameBalance.Get("boss1.charge_speed", chargeSpeed);
            chargeDuration = GameBalance.Get("boss1.charge_duration", chargeDuration);
            chargeTrailDuration = GameBalance.Get("boss1.charge_trail_duration", chargeTrailDuration);
            chargeTrailDamage = GameBalance.GetInt("boss1.charge_trail_damage", chargeTrailDamage);

            plaqueInterval = GameBalance.Get("boss1.plaque_interval", plaqueInterval);
            plaqueWarning = GameBalance.Get("boss1.plaque_telegraph", plaqueWarning);
            plaqueBlastRadius = GameBalance.Get("boss1.plaque_blast_radius", plaqueBlastRadius);
            plaqueBlastDamage = GameBalance.GetInt("boss1.plaque_blast_damage", plaqueBlastDamage);
            plaqueNodeHP = GameBalance.GetInt("boss1.plaque_node_hp", plaqueNodeHP);
        }
        else if (bossType == BossType.VaricoseMedusa)
        {
            snakeInterval = GameBalance.Get("boss2.snake_interval", snakeInterval);
            snakePaths = GameBalance.GetInt("boss2.snake_paths", snakePaths);
            snakeWarning = GameBalance.Get("boss2.snake_telegraph", snakeWarning);
            snakeSweepDelay = GameBalance.Get("boss2.snake_sweep_delay", snakeSweepDelay);
            snakeDamage = GameBalance.GetInt("boss2.snake_damage", snakeDamage);
            snakeStasisStacks = GameBalance.GetInt("boss2.snake_stasis_stacks", snakeStasisStacks);
            snakeTrailDuration = GameBalance.Get("boss2.snake_trail_duration", snakeTrailDuration);
            snakeTrailSlow = GameBalance.Get("boss2.snake_trail_slow", snakeTrailSlow);
            snakeRootHP = GameBalance.GetInt("boss2.snake_root_hp", snakeRootHP);

            gazeInterval = GameBalance.Get("boss2.gaze_interval", gazeInterval);
            gazeWarning = GameBalance.Get("boss2.gaze_telegraph", gazeWarning);
            gazeDuration = GameBalance.Get("boss2.gaze_duration", gazeDuration);
            gazeStasisPerTick = GameBalance.GetInt("boss2.gaze_stasis_per_tick", gazeStasisPerTick);
            gazeRootStacks = GameBalance.GetInt("boss2.gaze_root_stacks", gazeRootStacks);
            gazeRootDuration = GameBalance.Get("boss2.gaze_root_duration", gazeRootDuration);
            gazeTick = GameBalance.Get("boss2.gaze_tick", gazeTick);
            gazeRange = GameBalance.Get("boss2.gaze_range", gazeRange);

            valveInterval = GameBalance.Get("boss2.valve_interval", valveInterval);
            valveWarning = GameBalance.Get("boss2.valve_telegraph", valveWarning);
            valveDuration = GameBalance.Get("boss2.valve_duration", valveDuration);
            valveNodeCount = GameBalance.GetInt("boss2.valve_node_count", valveNodeCount);
            valveNodeHP = GameBalance.GetInt("boss2.valve_node_hp", valveNodeHP);
            valvePoolRadius = GameBalance.Get("boss2.valve_pool_radius", valvePoolRadius);
            valvePoolSlow = GameBalance.Get("boss2.valve_pool_slow", valvePoolSlow);
            valvePushSpeed = GameBalance.Get("boss2.valve_push_speed", valvePushSpeed);
            valvePushRadius = GameBalance.Get("boss2.valve_push_radius", valvePushRadius);
        }
        else
        {
            ringInterval = GameBalance.Get("boss3.ring_interval", ringInterval);
            ringTelegraph = GameBalance.Get("boss3.ring_telegraph", ringTelegraph);
            ringDamage = GameBalance.GetInt("boss3.ring_damage", ringDamage);
            ringSpeed = GameBalance.Get("boss3.ring_speed", ringSpeed);
            ringGapAngle = GameBalance.Get("boss3.ring_gap_angle", ringGapAngle);
            ringBandWidth = GameBalance.Get("boss3.ring_band_width", ringBandWidth);
            ringMaxRadius = GameBalance.Get("boss3.ring_max_radius", ringMaxRadius);

            budInterval = GameBalance.Get("boss3.bud_interval", budInterval);
            budTelegraph = GameBalance.Get("boss3.bud_telegraph", budTelegraph);
            budCount = GameBalance.GetInt("boss3.bud_count", budCount);
            budHP = GameBalance.GetInt("boss3.bud_hp", budHP);
            budDamage = GameBalance.GetInt("boss3.bud_damage", budDamage);
            budSpeed = GameBalance.Get("boss3.bud_speed", budSpeed);
            budLifetime = GameBalance.Get("boss3.bud_lifetime", budLifetime);

            crackInterval = GameBalance.Get("boss3.crack_interval", crackInterval);
            crackTelegraph = GameBalance.Get("boss3.crack_telegraph", crackTelegraph);
            crackCount = GameBalance.GetInt("boss3.crack_count", crackCount);
            crackDuration = GameBalance.Get("boss3.crack_duration", crackDuration);
            crackDamage = GameBalance.GetInt("boss3.crack_damage", crackDamage);
            crackTick = GameBalance.Get("boss3.crack_tick", crackTick);
            crackAngle = GameBalance.Get("boss3.crack_angle", crackAngle);
            crackRange = GameBalance.Get("boss3.crack_range", crackRange);
        }
    }

    // 设定集(3) Boss 数值表：基础血量/接触伤害（登场时间 4:15 由 spawn.boss_time=255 承载；
    // Boss2/3 登场时间⸢空⸥→沿用 4:15；常态移速⸢空/页裁⸥→保留现值 enemy.boss.speed_*）。
    public void ApplyBossStats()
    {
        if (enemy == null) enemy = GetComponent<Enemy>();
        if (enemy == null) return;
        int hp; int contact;
        if (bossType == BossType.WineStain)
        {
            hp = GameBalance.GetInt("boss1.hp", 1800);
            contact = GameBalance.GetInt("boss1.contact_damage", 8);
        }
        else if (bossType == BossType.VaricoseMedusa)
        {
            hp = GameBalance.GetInt("boss2.hp", 3000);
            contact = GameBalance.GetInt("boss2.contact_damage", 16);
        }
        else
        {
            hp = GameBalance.GetInt("boss3.hp", 4800);
            contact = GameBalance.GetInt("boss3.contact_damage", 22);
        }
        enemy.maxHP = hp;
        enemy.currentHP = hp;
        enemy.contactDamage = contact;
        enemy.noPickupDrop = true;
    }

    public void HandleEnemyDied(Enemy deadEnemy)
    {
        NotifyDefeated();
    }

    public void Start()
    {
        if (bossType == BossType.WineStain)
        {
            abilityPeriods[0] = spreadInterval; abilityPeriods[1] = chargeInterval; abilityPeriods[2] = plaqueInterval;
        }
        else if (bossType == BossType.VaricoseMedusa)
        {
            abilityPeriods[0] = snakeInterval; abilityPeriods[1] = gazeInterval; abilityPeriods[2] = valveInterval;
        }
        else
        {
            abilityPeriods[0] = ringInterval; abilityPeriods[1] = budInterval; abilityPeriods[2] = crackInterval;
        }
        for (int i = 0; i < 3; i++) abilityTimers[i] = abilityPeriods[i];

        if (spriteRenderer != null)
            spriteRenderer.color = bossType == BossType.WineStain ? normalColor :
                (bossType == BossType.VaricoseMedusa ? boss2Color : boss3Color);
        if (bossType == BossType.AneurysmColossus) BuildAneurysmSac();
    }

    // 瘤囊：半透明囊体子对象，随心跳脉动；高压脉环预警时亮起（程序化美术，沿用现有风格）。
    void BuildAneurysmSac()
    {
        aneurysmSac = new GameObject("瘤囊");
        aneurysmSac.transform.SetParent(transform, false);
        aneurysmSac.transform.localPosition = Vector3.zero;
        SpriteRenderer sr = aneurysmSac.AddComponent<SpriteRenderer>();
        sr.sprite = CharacterAttackEffect.GetWhiteSprite();
        sr.color = new Color(0.95f, 0.25f, 0.2f, 0.3f);
        sr.sortingOrder = 5;
        visualObjects.Add(aneurysmSac);
    }

    public void Update()
    {
        if (enemy == null || enemy.currentHP <= 0 || Time.timeScale <= 0f) return;

        if (player == null)
        {
            PlayerController controller = FindAnyObjectByType<PlayerController>();
            if (controller != null) player = controller.transform;
        }

        if (!halfHealthTriggered && enemy.currentHP <= enemy.maxHP * 0.5f)
        {
            halfHealthTriggered = true;
            SummonReinforcements();
        }

        // 通用施放调度：计时到点且不在冲刺/凝视中时依次施放，施放后至少间隔 minCastInterval。
        for (int i = 0; i < 3; i++) abilityTimers[i] = Mathf.Max(0f, abilityTimers[i] - Time.deltaTime);
        if (!charging && !gazing && player != null && Time.time >= nextCastAt)
        {
            for (int i = 0; i < 3; i++)
            {
                if (abilityTimers[i] > 0f) continue;
                CastAbility(i);
                abilityTimers[i] = abilityPeriods[i];
                nextCastAt = Time.time + minCastInterval;
                break;
            }
        }

        if (charging) UpdateCharge();
        if (gazing) UpdateGaze();
        UpdateActiveEffects();
        UpdateSacPulse();
    }

    void CastAbility(int index)
    {
        if (bossType == BossType.WineStain)
        {
            if (index == 0) SpreadCapillaries();
            else if (index == 1) BeginCharge();
            else ThickenPlaques();
        }
        else if (bossType == BossType.VaricoseMedusa)
        {
            if (index == 0) SnakeHairAttack();
            else if (index == 1) BeginGaze();
            else StartCoroutine(ValveBackflow());
        }
        else
        {
            if (index == 0) StartCoroutine(PulseRing());
            else if (index == 1) StartCoroutine(WallBudRelease());
            else StartCoroutine(WallCrackJets());
        }
    }

    void UpdateSacPulse()
    {
        if (aneurysmSac == null) return;
        float pulse = 1.7f + Mathf.Sin(Time.time * 2.6f) * 0.22f;
        aneurysmSac.transform.localScale = Vector3.one * pulse;
    }

    void UpdateActiveEffects()
    {
        for (int i = activeSpreads.Count - 1; i >= 0; i--)
        {
            if (activeSpreads[i] == null || activeSpreads[i].IsExpired()) { RemoveSpread(i); continue; }
            UpdateSpread(activeSpreads[i]);
        }
        for (int i = activePlaques.Count - 1; i >= 0; i--)
        {
            if (activePlaques[i] == null || activePlaques[i].resolved) { activePlaques.RemoveAt(i); continue; }
            UpdatePlaque(activePlaques[i]);
        }
        for (int i = activeSnakes.Count - 1; i >= 0; i--)
        {
            if (activeSnakes[i] == null || activeSnakes[i].IsExpired()) { RemoveSnake(i); continue; }
            UpdateSnake(activeSnakes[i]);
        }
        for (int i = activeValves.Count - 1; i >= 0; i--)
        {
            if (activeValves[i] == null || activeValves[i].finished) { activeValves.RemoveAt(i); continue; }
            UpdateValve(activeValves[i]);
        }
        for (int i = activeRings.Count - 1; i >= 0; i--)
        {
            if (activeRings[i] == null || activeRings[i].IsExpired()) { RemoveRing(i); continue; }
            UpdateRing(activeRings[i]);
        }
        for (int i = activeBuds.Count - 1; i >= 0; i--)
        {
            if (activeBuds[i] == null || activeBuds[i].resolved) { activeBuds.RemoveAt(i); continue; }
            UpdateBud(activeBuds[i]);
        }
        for (int i = activeCracks.Count - 1; i >= 0; i--)
        {
            if (activeCracks[i] == null || activeCracks[i].IsExpired()) { RemoveCrack(i); continue; }
            UpdateCrack(activeCracks[i]);
        }
        redZones.RemoveAll(zone => zone == null);
        bossNodes.RemoveAll(node => node == null);
    }

    public void FixedUpdate()
    {
        if (enemy == null || player == null || charging || gazing ||
            enemy.currentHP <= 0 || Time.timeScale <= 0f) return;

        Vector2 direction = (player.position - transform.position).normalized;
        transform.position += (Vector3)(direction * enemy.EffectiveMoveSpeed * Time.fixedDeltaTime);
    }

    #region 共用工具

    LineRenderer MakeLine(string name, float width, Color color)
    {
        GameObject obj = new GameObject(name);
        LineRenderer line = obj.AddComponent<LineRenderer>();
        CharacterAttackEffect.EnsureEffectMaterial();
        if (CharacterAttackEffect.effectMaterial != null) line.sharedMaterial = CharacterAttackEffect.effectMaterial;
        line.useWorldSpace = true;
        line.startWidth = line.endWidth = width;
        line.startColor = line.endColor = color;
        line.sortingOrder = 8;
        visualObjects.Add(obj);
        return line;
    }

    void DestroyVisual(GameObject obj)
    {
        if (obj == null) return;
        visualObjects.Remove(obj);
        Destroy(obj);
    }

    static float DistToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(0.0001f, ab.sqrMagnitude));
        return (p - (a + ab * t)).magnitude;
    }

    // 可击破结节/血栓芽/发根：复用敌人对象（无接触伤害、无经验、不掉落），纳入玩家攻击目标列表。
    Enemy SpawnNode(Vector3 position, int hp, Color tint, float scale, Action<Enemy> onDeath)
    {
        if (spawner == null) spawner = FindAnyObjectByType<EnemySpawner>();
        if (spawner == null || spawner.enemyPrefab == null) return null;
        GameObject obj = Instantiate(spawner.enemyPrefab, position, Quaternion.identity);
        obj.name = "BossNode";
        EnemyArchetype archetype = obj.GetComponent<EnemyArchetype>();
        if (archetype != null) Destroy(archetype);
        Enemy node = obj.GetComponent<Enemy>();
        if (node == null) node = obj.AddComponent<Enemy>();
        node.maxHP = hp; node.currentHP = hp;
        node.contactDamage = 0; node.xpValue = 0; node.xpOrbPrefab = null;
        node.externalMovement = true; node.knockbackResistance = 1f;
        node.deathDelay = 0f; node.noPickupDrop = true;
        node.player = player;
        MonsterSurvivorArtLibrary.ApplyEnemy(obj, EnemyKind.CellShadow);
        SpriteRenderer sr = obj.GetComponent<SpriteRenderer>();
        if (sr != null) sr.color = tint;
        node.visualColor = tint;
        obj.transform.localScale = Vector3.one * scale;
        if (onDeath != null) node.OnDied += onDeath;
        bossNodes.Add(node);
        if (!spawner.trackedEnemies.Contains(node))
        {
            spawner.trackedEnemies.Add(node);
            node.OnDied += spawner.HandleTrackedEnemyDied;
        }
        return node;
    }

    void KillNode(Enemy node)
    {
        if (node == null) return;
        bossNodes.Remove(node);
        if (spawner != null)
        {
            node.OnDied -= spawner.HandleTrackedEnemyDied;
            spawner.trackedEnemies.Remove(node);
        }
        Destroy(node.gameObject);
    }

    PlayerStats PlayerStatsRef()
    {
        return player == null ? null : player.GetComponent<PlayerStats>();
    }

    PlayerController PlayerControllerRef()
    {
        return player == null ? null : player.GetComponent<PlayerController>();
    }

    void BurstVisual(Vector3 position, float radius, Color color)
    {
        LineRenderer ring = MakeLine("BossBurst", 0.14f, color);
        ring.positionCount = 49;
        for (int i = 0; i < 49; i++)
        {
            float rad = i * Mathf.PI * 2f / 48f;
            ring.SetPosition(i, position + new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f) * radius);
        }
        Destroy(ring.gameObject, 0.3f);
        visualObjects.Remove(ring.gameObject);
    }

    #endregion

    #region Boss1 - 葡萄酒样色斑

    // 毛细蔓延：2-3 条淡红色血管线，1.5s 后转紫红，踩中受伤+短暂减速（伤害/减速数值未给→保留现值，标旗）。
    void SpreadCapillaries()
    {
        int count = UnityEngine.Random.Range(spreadCountMin, spreadCountMax + 1);
        Vector2 baseDir = player != null ? ((Vector2)(player.position - transform.position)).normalized : Vector2.up;
        float baseAngle = Mathf.Atan2(baseDir.y, baseDir.x) * Mathf.Rad2Deg;
        for (int i = 0; i < count; i++)
        {
            float angle = baseAngle + (i - (count - 1) * 0.5f) * 38f + UnityEngine.Random.Range(-8f, 8f);
            Vector2 direction = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
            BossSpreadLine line = new BossSpreadLine();
            line.startPos = transform.position;
            line.direction = direction;
            line.length = spreadLength;
            line.chargeTime = spreadChargeTime;
            line.damage = spreadDamage;
            line.slowMult = spreadSlowMult;
            line.slowDuration = spreadSlowDuration;
            line.lifetime = 5f;
            line.visual = MakeLine("毛细蔓延", 0.22f, new Color(0.95f, 0.55f, 0.55f, 0.6f));
            line.visual.positionCount = 2;
            activeSpreads.Add(line);
        }
    }

    void UpdateSpread(BossSpreadLine line)
    {
        line.elapsed += Time.deltaTime;
        if (!line.charged && line.elapsed >= line.chargeTime)
        {
            line.charged = true;
            if (line.visual != null) line.visual.startColor = line.visual.endColor = new Color(0.62f, 0.1f, 0.3f, 0.95f);
        }
        float growth = Mathf.Clamp01(line.elapsed / 0.5f);
        Vector3 end = line.startPos + (Vector3)(line.direction * line.length * growth);
        if (line.visual != null)
        {
            line.visual.SetPosition(0, line.startPos);
            line.visual.SetPosition(1, end);
        }
        if (!line.charged || player == null) return;
        if (Time.time < line.nextHitAt) return;
        float dist = DistToSegment(player.position, line.startPos, end);
        if (dist > 0.35f) return;
        PlayerStats stats = PlayerStatsRef();
        if (stats != null) stats.TakeDamage(line.damage);
        PlayerController controller = PlayerControllerRef();
        if (controller != null) controller.ApplyTimedSlow(line.slowMult, line.slowDuration);
        line.nextHitAt = Time.time + 1f;
    }

    void RemoveSpread(int index)
    {
        BossSpreadLine line = activeSpreads[index];
        if (line != null && line.visual != null) DestroyVisual(line.visual.gameObject);
        activeSpreads.RemoveAt(index);
    }

    // 脉冲冲刺：0.8s 红色直线预警 → 固定方向冲刺 1s，撞墙即停，留下红斑带。
    void BeginCharge()
    {
        if (player == null) return;
        chargeDirection = (player.position - transform.position).normalized;
        if (chargeDirection.sqrMagnitude < 0.01f) chargeDirection = Vector2.up;
        charging = true;
        warningRemaining = chargeWarning;
        if (spriteRenderer != null) spriteRenderer.color = warningColor;
        chargeTelegraphLine = MakeLine("冲刺预警", 0.3f, new Color(1f, 0.15f, 0.15f, 0.75f));
        chargeTelegraphLine.positionCount = 2;
        chargeTelegraphLine.SetPosition(0, transform.position);
        chargeTelegraphLine.SetPosition(1, transform.position + (Vector3)(chargeDirection * chargeSpeed * chargeDuration));
    }

    void UpdateCharge()
    {
        if (warningRemaining > 0f)
        {
            warningRemaining -= Time.deltaTime;
            if (chargeTelegraphLine != null)
            {
                chargeTelegraphLine.SetPosition(0, transform.position);
                chargeTelegraphLine.SetPosition(1, transform.position + (Vector3)(chargeDirection * chargeSpeed * chargeDuration));
            }
            if (warningRemaining <= 0f)
            {
                chargeRemaining = chargeDuration;
                chargeTrailDropTimer = 0f;
                if (chargeTelegraphLine != null) { DestroyVisual(chargeTelegraphLine.gameObject); chargeTelegraphLine = null; }
            }
            return;
        }

        chargeRemaining -= Time.deltaTime;
        Vector3 next = transform.position + (Vector3)(chargeDirection * chargeSpeed * Time.deltaTime);
        bool hitWall = false;
        BossArenaController arena = spawner == null ? null : spawner.arena;
        if (arena != null && arena.active)
        {
            float minX = arena.center.x - arena.halfExtents.x + arena.bossMargin;
            float maxX = arena.center.x + arena.halfExtents.x - arena.bossMargin;
            float minY = arena.center.y - arena.halfExtents.y + arena.bossMargin;
            float maxY = arena.center.y + arena.halfExtents.y - arena.bossMargin;
            Vector3 clamped = new Vector3(Mathf.Clamp(next.x, minX, maxX), Mathf.Clamp(next.y, minY, maxY), next.z);
            if ((clamped - next).sqrMagnitude > 0.0001f) hitWall = true;
            next = clamped;
        }
        transform.position = next;

        chargeTrailDropTimer -= Time.deltaTime;
        if (chargeTrailDropTimer <= 0f)
        {
            chargeTrailDropTimer = 0.12f;
            VesselHazardZone zone = VesselHazardZone.Create(transform.position, 0.55f, chargeTrailDamage, 1f, null, chargeTrailDuration, 1f);
            if (zone.spriteRenderer != null) zone.spriteRenderer.color = new Color(0.85f, 0.15f, 0.2f, 0.3f);
            redZones.Add(zone);
        }

        if (chargeRemaining <= 0f || hitWall)
        {
            charging = false;
            if (spriteRenderer != null) spriteRenderer.color = normalColor;
        }
    }

    // 斑块增厚：场上最早生成的两处红斑鼓起成结节，1s 膨胀预警后迸出碎片；预警期击破结节可提前清除红斑。
    void ThickenPlaques()
    {
        int made = 0;
        for (int i = 0; i < redZones.Count && made < 2; i++)
        {
            VesselHazardZone zone = redZones[i];
            if (zone == null) continue;
            bool already = false;
            for (int p = 0; p < activePlaques.Count; p++)
                if (activePlaques[p].zone == zone) { already = true; break; }
            if (already) continue;
            BossPlaque plaque = new BossPlaque();
            plaque.zone = zone;
            plaque.position = zone.transform.position;
            plaque.warningTime = plaqueWarning;
            plaque.blastRadius = plaqueBlastRadius;
            plaque.blastDamage = plaqueBlastDamage;
            plaque.node = SpawnNode(plaque.position, plaqueNodeHP, new Color(0.75f, 0.2f, 0.25f, 1f), 0.9f,
                dead => { plaque.resolved = true; if (plaque.zone != null) Destroy(plaque.zone.gameObject); });
            activePlaques.Add(plaque);
            made++;
        }
    }

    void UpdatePlaque(BossPlaque plaque)
    {
        if (plaque.node == null) { plaque.resolved = true; return; }
        plaque.elapsed += Time.deltaTime;
        float swell = Mathf.Clamp01(plaque.elapsed / plaque.warningTime);
        plaque.node.transform.localScale = Vector3.one * (0.9f + swell * 0.6f);
        if (plaque.elapsed < plaque.warningTime) return;
        // 膨胀完成：迸出碎片。
        plaque.resolved = true;
        if (player != null && (player.position - plaque.position).sqrMagnitude <= plaque.blastRadius * plaque.blastRadius)
        {
            PlayerStats stats = PlayerStatsRef();
            if (stats != null) stats.TakeDamage(plaque.blastDamage);
        }
        BurstVisual(plaque.position, plaque.blastRadius, new Color(0.9f, 0.25f, 0.3f, 0.9f));
        if (plaque.zone != null) Destroy(plaque.zone.gameObject);
        KillNode(plaque.node);
    }

    #endregion

    #region Boss2 - 曲张美杜莎

    // 曲张蛇发：0.8s 紫色脉络标出 3 条分叉路径 → 发根可击破 → 1.2s 后扫击（20 伤害+1 层淤滞）→ 保留 3s 减速窄道。
    void SnakeHairAttack()
    {
        Vector2 baseDir = player != null ? ((Vector2)(player.position - transform.position)).normalized : Vector2.up;
        float baseAngle = Mathf.Atan2(baseDir.y, baseDir.x) * Mathf.Rad2Deg;
        for (int i = 0; i < snakePaths; i++)
        {
            float angle = baseAngle + (i - (snakePaths - 1) * 0.5f) * 35f;
            Vector2 direction = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
            BossSnakeHair snake = new BossSnakeHair();
            snake.startPos = transform.position;
            snake.direction = direction;
            snake.length = 4.5f;
            snake.warningTime = snakeWarning;
            snake.sweepDelay = snakeSweepDelay;
            snake.damage = snakeDamage;
            snake.stasisStacks = snakeStasisStacks;
            snake.trailDuration = snakeTrailDuration;
            snake.trailSlow = snakeTrailSlow;
            snake.visual = MakeLine("曲张蛇发", 0.16f, new Color(0.65f, 0.3f, 0.9f, 0.7f));
            snake.visual.positionCount = 2;
            snake.visual.SetPosition(0, snake.startPos);
            snake.visual.SetPosition(1, snake.startPos + (Vector3)(direction * snake.length));
            snake.root = SpawnNode(snake.startPos + (Vector3)(direction * 0.7f), snakeRootHP,
                new Color(0.7f, 0.35f, 0.95f, 1f), 0.7f, dead => { snake.cut = true; });
            activeSnakes.Add(snake);
        }
    }

    void UpdateSnake(BossSnakeHair snake)
    {
        snake.elapsed += Time.deltaTime;
        if (snake.cut)
        {
            // 击破发根截断蛇发：清除蛇身与减速窄道。
            snake.elapsed = snake.warningTime + snake.sweepDelay + snake.trailDuration;
            return;
        }
        if (snake.visual != null)
        {
            float width = snake.elapsed >= snake.warningTime ? 0.38f : 0.16f;
            snake.visual.startWidth = snake.visual.endWidth = width;
            if (!snake.swept && snake.elapsed >= snake.warningTime + snake.sweepDelay)
            {
                snake.swept = true;
                Vector3 end = snake.startPos + (Vector3)(snake.direction * snake.length);
                if (player != null && DistToSegment(player.position, snake.startPos, end) < 0.5f)
                {
                    PlayerStats stats = PlayerStatsRef();
                    if (stats != null)
                    {
                        stats.TakeDamage(snake.damage);
                        stats.AddStasisStack(snake.stasisStacks);
                    }
                }
                // 蛇发保留 3s，形成减速窄道。
                for (int z = 0; z < 3; z++)
                {
                    Vector3 zonePos = snake.startPos + (Vector3)(snake.direction * snake.length * (0.25f + z * 0.25f));
                    VesselHazardZone zone = VesselHazardZone.Create(zonePos, 0.7f, 0, snake.trailSlow, null, snake.trailDuration, 1f);
                    if (zone.spriteRenderer != null) zone.spriteRenderer.color = new Color(0.55f, 0.3f, 0.85f, 0.22f);
                    snake.trailZones.Add(zone);
                }
            }
        }
    }

    void RemoveSnake(int index)
    {
        BossSnakeHair snake = activeSnakes[index];
        if (snake != null)
        {
            if (snake.visual != null) DestroyVisual(snake.visual.gameObject);
            for (int i = 0; i < snake.trailZones.Count; i++) if (snake.trailZones[i] != null) Destroy(snake.trailZones[i].gameObject);
            if (snake.root != null) KillNode(snake.root);
        }
        activeSnakes.RemoveAt(index);
    }

    void CutAllSnakes()
    {
        for (int i = 0; i < activeSnakes.Count; i++) if (activeSnakes[i] != null) activeSnakes[i].cut = true;
    }

    // 淤血凝视：1s 扇形红紫预警 → 凝视 1.2s，视野内持续叠加淤滞，3 层定身 0.8s；闭合静脉瓣（结节）可遮挡。
    void BeginGaze()
    {
        if (player == null) return;
        gazeDirection = (player.position - transform.position).normalized;
        if (gazeDirection.sqrMagnitude < 0.01f) gazeDirection = Vector2.up;
        gazing = true;
        gazeWarningRemaining = gazeWarning;
        gazeTickTimer = 0f;
        if (spriteRenderer != null) spriteRenderer.color = warningColor;
        gazeVisual = new GameObject("凝视扇区");
        visualObjects.Add(gazeVisual);
        LineRenderer arc = gazeVisual.AddComponent<LineRenderer>();
        CharacterAttackEffect.EnsureEffectMaterial();
        if (CharacterAttackEffect.effectMaterial != null) arc.sharedMaterial = CharacterAttackEffect.effectMaterial;
        arc.useWorldSpace = true;
        arc.positionCount = 33;
        arc.startWidth = arc.endWidth = 0.14f;
        arc.startColor = arc.endColor = new Color(0.8f, 0.2f, 0.5f, 0.6f);
        arc.sortingOrder = 8;
    }

    void UpdateGaze()
    {
        DrawGazeCone(gazeWarningRemaining > 0f ? 0.45f : 0.8f);
        if (gazeWarningRemaining > 0f)
        {
            gazeWarningRemaining -= Time.deltaTime;
            if (gazeWarningRemaining <= 0f) gazeRemaining = gazeDuration;
            return;
        }

        gazeRemaining -= Time.deltaTime;
        gazeTickTimer -= Time.deltaTime;
        if (gazeTickTimer <= 0f && player != null)
        {
            gazeTickTimer = gazeTick;
            Vector2 toPlayer = (player.position - transform.position).normalized;
            float distance = Vector2.Distance(player.position, transform.position);
            if (distance <= gazeRange && Vector2.Dot(gazeDirection, toPlayer) > 0.5f && !BlockedByValveNode())
            {
                PlayerStats stats = PlayerStatsRef();
                if (stats != null)
                {
                    stats.AddStasisStack(gazeStasisPerTick);
                    if (stats.stasisStacks >= gazeRootStacks)
                    {
                        stats.RootPlayer(gazeRootDuration);
                        stats.stasisStacks = 0;
                    }
                }
            }
        }

        if (gazeRemaining <= 0f)
        {
            gazing = false;
            if (spriteRenderer != null) spriteRenderer.color = boss2Color;
            if (gazeVisual != null) { DestroyVisual(gazeVisual); gazeVisual = null; }
        }
    }

    void DrawGazeCone(float alpha)
    {
        if (gazeVisual == null) return;
        LineRenderer arc = gazeVisual.GetComponent<LineRenderer>();
        if (arc == null) return;
        float baseAngle = Mathf.Atan2(gazeDirection.y, gazeDirection.x) * Mathf.Rad2Deg;
        for (int i = 0; i < 33; i++)
        {
            float angle = (baseAngle - 30f + i * 60f / 32f) * Mathf.Deg2Rad;
            arc.SetPosition(i, transform.position + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * gazeRange);
        }
        Color c = new Color(0.8f, 0.2f, 0.5f, alpha);
        arc.startColor = arc.endColor = c;
    }

    bool BlockedByValveNode()
    {
        if (player == null) return false;
        for (int i = 0; i < activeValves.Count; i++)
        {
            BossValve valve = activeValves[i];
            if (valve == null) continue;
            for (int n = 0; n < valve.nodes.Count; n++)
            {
                Enemy node = valve.nodes[n];
                if (node == null || node.currentHP <= 0) continue;
                if (DistToSegment(node.transform.position, transform.position, player.position) < 0.7f) return true;
            }
        }
        return false;
    }

    // 瓣膜逆流：两处静脉瓣发紫预警 1s → 反向推流 2.5s + 末端 2 枚可击破结节与慢速血滴；
    // 结节存活时产生小范围减速血池；全部击破提前结束逆流。（静脉瓣实体不存在→以 Boss 两侧点位替代，标旗）
    IEnumerator ValveBackflow()
    {
        Vector2 toPlayer = player != null ? ((Vector2)(player.position - transform.position)).normalized : Vector2.up;
        Vector2 perpendicular = new Vector2(-toPlayer.y, toPlayer.x);
        BossValve valve = new BossValve();
        valve.warningTime = valveWarning;
        valve.duration = valveDuration;
        for (int i = 0; i < valveNodeCount; i++)
        {
            float sign = i == 0 ? -1f : 1f;
            Vector3 position = transform.position + (Vector3)(perpendicular * sign * 2.8f);
            valve.positions.Add(position);
            LineRenderer ring = MakeLine("瓣膜预警", 0.16f, new Color(0.6f, 0.25f, 0.85f, 0.7f));
            ring.positionCount = 33;
            for (int p = 0; p < 33; p++)
            {
                float rad = p * Mathf.PI * 2f / 32f;
                ring.SetPosition(p, position + new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f) * 0.9f);
            }
            valve.telegraphs.Add(ring.gameObject);
        }
        activeValves.Add(valve);
        yield return new WaitForSeconds(valveWarning);
        if (valve.finished) yield break;
        for (int i = 0; i < valve.telegraphs.Count; i++) DestroyVisual(valve.telegraphs[i]);
        valve.telegraphs.Clear();
        valve.active = true;
        for (int i = 0; i < valve.positions.Count; i++)
        {
            Vector3 position = valve.positions[i];
            BossValve captured = valve;
            Enemy node = SpawnNode(position, valveNodeHP, new Color(0.6f, 0.25f, 0.85f, 1f), 0.95f,
                dead => { captured.nodes.Remove(dead); });
            if (node != null)
            {
                valve.nodes.Add(node);
                VesselHazardZone pool = VesselHazardZone.Create(position, valvePoolRadius, 0, valvePoolSlow, node.gameObject, 9999f, 1f);
                if (pool.spriteRenderer != null) pool.spriteRenderer.color = new Color(0.5f, 0.2f, 0.7f, 0.25f);
            }
        }
        // 慢速血滴：小型低血量追踪敌人。
        int droplets = GameBalance.GetInt("boss2.droplet_count", 3);
        for (int i = 0; i < droplets; i++)
        {
            Vector3 position = valve.positions[i % valve.positions.Count] + (Vector3)(UnityEngine.Random.insideUnitCircle * 0.6f);
            Enemy droplet = SpawnNode(position, GameBalance.GetInt("boss2.droplet_hp", 1),
                new Color(0.9f, 0.3f, 0.35f, 1f), 0.4f, null);
            if (droplet != null)
            {
                droplet.externalMovement = false;
                droplet.moveSpeed = GameBalance.Get("boss2.droplet_speed", 1.5f);
                droplet.contactDamage = GameBalance.GetInt("boss2.droplet_damage", 1);
                valve.droplets.Add(droplet);
            }
        }
    }

    void UpdateValve(BossValve valve)
    {
        valve.elapsed += Time.deltaTime;
        if (!valve.active) return;
        // 全部结节击破 → 提前结束逆流。
        bool allBroken = valve.nodes.Count == 0;
        for (int i = 0; i < valve.nodes.Count; i++) if (valve.nodes[i] == null || valve.nodes[i].currentHP <= 0) { allBroken = true; break; }
        if (valve.elapsed >= valve.warningTime + valve.duration || allBroken)
        {
            EndValve(valve);
            return;
        }
        // 反向推流：把玩家从瓣膜点位推开。
        Rigidbody2D playerRb = player == null ? null : player.GetComponent<Rigidbody2D>();
        if (playerRb != null)
        {
            for (int i = 0; i < valve.positions.Count; i++)
            {
                Vector2 away = (Vector2)(player.position - valve.positions[i]);
                if (away.sqrMagnitude > valvePushRadius * valvePushRadius || away.sqrMagnitude < 0.001f) continue;
                playerRb.position += away.normalized * valvePushSpeed * Time.deltaTime;
            }
        }
    }

    void EndValve(BossValve valve)
    {
        if (valve.finished) return;
        valve.finished = true;
        for (int i = 0; i < valve.telegraphs.Count; i++) DestroyVisual(valve.telegraphs[i]);
        for (int i = 0; i < valve.droplets.Count; i++) if (valve.droplets[i] != null) KillNode(valve.droplets[i]);
        // 静脉瓣闭合截断蛇发（§2.3 蛇发 cell「等待静脉瓣闭合可截断蛇发」）。
        CutAllSnakes();
    }

    #endregion

    #region Boss3 - 动脉瘤巨像·赤冠

    // 高压脉环：瘤囊亮起，0.9s 后释放带一处缺口的环形冲击波（脉环伤害 18）。
    IEnumerator PulseRing()
    {
        if (aneurysmSac != null)
        {
            SpriteRenderer sr = aneurysmSac.GetComponent<SpriteRenderer>();
            if (sr != null) sr.color = new Color(1f, 0.35f, 0.25f, 0.65f);
        }
        BossPulseRing ring = new BossPulseRing();
        ring.center = transform.position;
        ring.gapCenter = UnityEngine.Random.Range(0f, 360f);
        ring.gapAngle = ringGapAngle;
        ring.telegraph = ringTelegraph;
        ring.speed = ringSpeed;
        ring.maxRadius = ringMaxRadius;
        ring.bandWidth = ringBandWidth;
        ring.damage = ringDamage;
        ring.visual = MakeLine("高压脉环", 0.3f, new Color(1f, 0.3f, 0.2f, 0.85f));
        activeRings.Add(ring);
        yield return new WaitForSeconds(ringTelegraph);
        ring.released = true;
        if (aneurysmSac != null)
        {
            SpriteRenderer sr = aneurysmSac.GetComponent<SpriteRenderer>();
            if (sr != null) sr.color = new Color(0.95f, 0.25f, 0.2f, 0.3f);
        }
    }

    void UpdateRing(BossPulseRing ring)
    {
        ring.elapsed += Time.deltaTime;
        if (!ring.released)
        {
            DrawArc(ring.visual, ring.center, 1.6f, ring.gapCenter, ring.gapAngle, 0.5f);
            return;
        }
        ring.radius += ring.speed * Time.deltaTime;
        DrawArc(ring.visual, ring.center, ring.radius, ring.gapCenter, ring.gapAngle, 0.9f);
        if (!ring.hitPlayer && player != null)
        {
            float distance = Vector2.Distance(player.position, ring.center);
            if (Mathf.Abs(distance - ring.radius) <= ring.bandWidth)
            {
                Vector2 toPlayer = (Vector2)(player.position - ring.center);
                float playerAngle = Mathf.Atan2(toPlayer.y, toPlayer.x) * Mathf.Rad2Deg;
                float delta = Mathf.Abs(Mathf.DeltaAngle(playerAngle, ring.gapCenter));
                if (delta > ring.gapAngle * 0.5f)
                {
                    ring.hitPlayer = true;
                    PlayerStats stats = PlayerStatsRef();
                    if (stats != null) stats.TakeDamage(ring.damage);
                }
            }
        }
    }

    void DrawArc(LineRenderer line, Vector3 center, float radius, float gapCenter, float gapAngle, float alpha)
    {
        if (line == null) return;
        int segments = 64;
        line.positionCount = segments + 1;
        float start = gapCenter + gapAngle * 0.5f;
        float span = 360f - gapAngle;
        for (int i = 0; i <= segments; i++)
        {
            float angle = (start + span * i / segments) * Mathf.Deg2Rad;
            line.SetPosition(i, center + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius);
        }
        Color c = line.startColor; c.a = alpha;
        line.startColor = line.endColor = c;
    }

    void RemoveRing(int index)
    {
        BossPulseRing ring = activeRings[index];
        if (ring != null && ring.visual != null) DestroyVisual(ring.visual.gameObject);
        activeRings.RemoveAt(index);
    }

    // 附壁栓脱：两枚血栓芽与对应分支先亮起，1s 后高速沿分支飞出；提前击破血栓芽可取消。
    // 血栓伤害列⸢页裁⸥仅残值 1 → 接触伤害按 1 实现并标旗。
    IEnumerator WallBudRelease()
    {
        Vector2 baseDir = player != null ? ((Vector2)(player.position - transform.position)).normalized : Vector2.up;
        float baseAngle = Mathf.Atan2(baseDir.y, baseDir.x) * Mathf.Rad2Deg;
        List<BossBud> buds = new List<BossBud>();
        for (int i = 0; i < budCount; i++)
        {
            float angle = baseAngle + (i == 0 ? -25f : 25f);
            Vector2 direction = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
            BossBud bud = new BossBud();
            bud.direction = direction;
            bud.telegraph = budTelegraph;
            bud.branch = MakeLine("血栓分支", 0.2f, new Color(1f, 0.25f, 0.2f, 0.7f));
            bud.branch.positionCount = 2;
            bud.branch.SetPosition(0, transform.position);
            bud.branch.SetPosition(1, transform.position + (Vector3)(direction * 8f));
            bud.node = SpawnNode(transform.position + (Vector3)(direction * 1.3f), budHP,
                new Color(0.55f, 0.15f, 0.4f, 1f), 0.75f, dead => { bud.resolved = true; });
            if (bud.node != null) bud.node.contactDamage = budDamage;
            buds.Add(bud);
            activeBuds.Add(bud);
        }
        yield return new WaitForSeconds(budTelegraph);
        for (int i = 0; i < buds.Count; i++)
        {
            BossBud bud = buds[i];
            if (bud.resolved || bud.node == null) continue;
            bud.launched = true;
            bud.remaining = budLifetime;
            if (bud.branch != null) { DestroyVisual(bud.branch.gameObject); bud.branch = null; }
        }
    }

    void UpdateBud(BossBud bud)
    {
        if (!bud.launched)
        {
            if (bud.node == null) bud.resolved = true;
            return;
        }
        if (bud.node == null) { bud.resolved = true; return; }
        bud.remaining -= Time.deltaTime;
        Vector3 next = bud.node.transform.position + (Vector3)(bud.direction * budSpeed * Time.deltaTime);
        bool outOfBounds = false;
        BossArenaController arena = spawner == null ? null : spawner.arena;
        if (arena != null && arena.active)
        {
            outOfBounds = next.x < arena.center.x - arena.halfExtents.x || next.x > arena.center.x + arena.halfExtents.x ||
                next.y < arena.center.y - arena.halfExtents.y || next.y > arena.center.y + arena.halfExtents.y;
        }
        if (bud.remaining <= 0f || outOfBounds)
        {
            bud.resolved = true;
            KillNode(bud.node);
            return;
        }
        bud.node.transform.position = next;
    }

    // 瘤壁渗裂：两处瘤壁裂纹，1.2s 后朝固定扇区喷流 2s。（喷流伤害/扇区角度未给数值→6/60° 标旗；
    // 「喷流后短暂暴露弱点」解法列页裁→不实现，标旗。）
    IEnumerator WallCrackJets()
    {
        Vector2 baseDir = player != null ? ((Vector2)(player.position - transform.position)).normalized : Vector2.up;
        float baseAngle = Mathf.Atan2(baseDir.y, baseDir.x) * Mathf.Rad2Deg;
        for (int i = 0; i < crackCount; i++)
        {
            float angle = baseAngle + (i == 0 ? -55f : 55f);
            Vector2 direction = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
            BossCrackJet crack = new BossCrackJet();
            crack.origin = transform.position + (Vector3)(direction * 1.1f);
            crack.direction = direction;
            crack.telegraph = crackTelegraph;
            crack.duration = crackDuration;
            crack.damage = crackDamage;
            crack.tick = crackTick;
            crack.angle = crackAngle;
            crack.range = crackRange;
            crack.visual = MakeLine("瘤壁裂纹", 0.18f, new Color(0.45f, 0.05f, 0.08f, 0.95f));
            crack.visual.positionCount = 4;
            crack.visual.SetPosition(0, crack.origin - (Vector3)(direction * 0.5f));
            crack.visual.SetPosition(1, crack.origin + (Vector3)(new Vector2(-direction.y, direction.x) * 0.2f));
            crack.visual.SetPosition(2, crack.origin - (Vector3)(new Vector2(-direction.y, direction.x) * 0.2f));
            crack.visual.SetPosition(3, crack.origin + (Vector3)(direction * 0.5f));
            activeCracks.Add(crack);
        }
        yield return null;
    }

    void UpdateCrack(BossCrackJet crack)
    {
        crack.elapsed += Time.deltaTime;
        if (crack.elapsed < crack.telegraph) return;
        if (!crack.jetting)
        {
            crack.jetting = true;
            if (crack.visual != null)
            {
                crack.visual.startColor = crack.visual.endColor = new Color(1f, 0.2f, 0.15f, 0.8f);
                crack.visual.positionCount = 33;
            }
        }
        // 扇区喷流视觉：外弧。
        if (crack.visual != null)
        {
            float baseAngle = Mathf.Atan2(crack.direction.y, crack.direction.x) * Mathf.Rad2Deg;
            for (int i = 0; i < 33; i++)
            {
                float angle = (baseAngle - crack.angle * 0.5f + crack.angle * i / 32f) * Mathf.Deg2Rad;
                crack.visual.SetPosition(i, crack.origin + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * crack.range);
            }
        }
        crack.tickTimer -= Time.deltaTime;
        if (crack.tickTimer <= 0f && player != null)
        {
            crack.tickTimer = crack.tick;
            Vector2 toPlayer = (Vector2)(player.position - crack.origin);
            if (toPlayer.magnitude <= crack.range && Vector2.Angle(crack.direction, toPlayer) <= crack.angle * 0.5f)
            {
                PlayerStats stats = PlayerStatsRef();
                if (stats != null) stats.TakeDamage(crack.damage);
            }
        }
    }

    void RemoveCrack(int index)
    {
        BossCrackJet crack = activeCracks[index];
        if (crack != null && crack.visual != null) DestroyVisual(crack.visual.gameObject);
        activeCracks.RemoveAt(index);
    }

    #endregion

    void SummonReinforcements()
    {
        if (spawner == null) spawner = FindAnyObjectByType<EnemySpawner>();
        if (spawner == null) return;

        // 半血增援沿用现值（v3 未给数值；Boss3 无召唤物描述→不召唤，标旗）。
        if (bossType == BossType.WineStain)
        {
            for (int i = 0; i < 2; i++)
            {
                Vector2 offset = UnityEngine.Random.insideUnitCircle * 1.5f;
                spawner.SpawnSpecific(EnemyKind.RupturedCell, transform.position + (Vector3)offset);
            }
        }
        else if (bossType == BossType.VaricoseMedusa)
        {
            for (int i = 0; i < 3; i++)
            {
                Vector2 offset = UnityEngine.Random.insideUnitCircle * 1.2f;
                spawner.SpawnSpecific(EnemyKind.VenousStasis, transform.position + (Vector3)offset);
            }
        }
    }

    public void NotifyDefeated()
    {
        if (defeated) return;
        defeated = true;
        if (spawner != null) spawner.NotifyBossDefeated();
    }

    public void OnDestroy()
    {
        if (enemy != null) enemy.OnDied -= HandleEnemyDied;
        if (enemy != null) enemy.externalMovement = false;
        if (enemy != null && enemy.currentHP <= 0) NotifyDefeated();
        for (int i = 0; i < visualObjects.Count; i++) if (visualObjects[i] != null) Destroy(visualObjects[i]);
        visualObjects.Clear();
        for (int i = 0; i < bossNodes.Count; i++) if (bossNodes[i] != null) KillNode(bossNodes[i]);
        bossNodes.Clear();
        for (int i = 0; i < redZones.Count; i++) if (redZones[i] != null) Destroy(redZones[i].gameObject);
        redZones.Clear();
    }
}

#region Boss Effect Classes

public class BossSpreadLine
{
    public Vector3 startPos;
    public Vector2 direction;
    public float length;
    public float chargeTime;
    public float elapsed;
    public float lifetime;
    public int damage;
    public float slowMult;
    public float slowDuration;
    public bool charged;
    public float nextHitAt;
    public LineRenderer visual;

    public bool IsExpired() { return elapsed >= lifetime; }
}

public class BossPlaque
{
    public Vector3 position;
    public float warningTime;
    public float elapsed;
    public float blastRadius;
    public int blastDamage;
    public bool resolved;
    public VesselHazardZone zone;
    public Enemy node;
}

public class BossSnakeHair
{
    public Vector3 startPos;
    public Vector2 direction;
    public float length;
    public float warningTime;
    public float sweepDelay;
    public float elapsed;
    public int damage;
    public int stasisStacks;
    public float trailDuration;
    public float trailSlow;
    public bool swept;
    public bool cut;
    public LineRenderer visual;
    public Enemy root;
    public List<VesselHazardZone> trailZones = new List<VesselHazardZone>();

    public bool IsExpired() { return elapsed >= warningTime + sweepDelay + trailDuration; }
}

public class BossValve
{
    public float warningTime;
    public float duration;
    public float elapsed;
    public bool active;
    public bool finished;
    public List<Vector3> positions = new List<Vector3>();
    public List<GameObject> telegraphs = new List<GameObject>();
    public List<Enemy> nodes = new List<Enemy>();
    public List<Enemy> droplets = new List<Enemy>();
}

public class BossPulseRing
{
    public Vector3 center;
    public float gapCenter;
    public float gapAngle;
    public float telegraph;
    public float elapsed;
    public float radius;
    public float speed;
    public float maxRadius;
    public float bandWidth;
    public int damage;
    public bool released;
    public bool hitPlayer;
    public LineRenderer visual;

    public bool IsExpired() { return released && radius >= maxRadius; }
}

public class BossBud
{
    public Vector2 direction;
    public float telegraph;
    public bool launched;
    public bool resolved;
    public float remaining;
    public LineRenderer branch;
    public Enemy node;
}

public class BossCrackJet
{
    public Vector3 origin;
    public Vector2 direction;
    public float telegraph;
    public float duration;
    public float elapsed;
    public int damage;
    public float tick;
    public float tickTimer;
    public float angle;
    public float range;
    public bool jetting;
    public LineRenderer visual;

    public bool IsExpired() { return elapsed >= telegraph + duration; }
}

#endregion