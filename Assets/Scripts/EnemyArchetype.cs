using System.Collections;
using UnityEngine;

public enum EnemyKind
{
    CellShadow,
    SpikeSoldier,
    RupturedCell,
    MicroThrombus,
    BleedingGap,
    VenousStasis,
    FibrinNet,
    PrimitiveCell,
    TargetBlocker,
    NodeBoss
}

public class EnemyArchetype : MonoBehaviour
{
    public EnemyKind kind = EnemyKind.CellShadow;
    public int stageIndex = 1;
    public Enemy enemy;
    public SpriteRenderer spriteRenderer;
    public bool configured;
    public bool subscribed;
    public bool obstacleSpawned;
    public VesselHazardZone ownedHazard;
    public float ruptureExplosionRadius = 2f;
    public int ruptureExplosionDamageMultiplier = 2;
    public float bleedingGapRadius = 2.8f;
    public static Collider2D[] nearbyBuffer = new Collider2D[32];

    public void Awake()
    {
        if (enemy == null) enemy = GetComponent<Enemy>();
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
    }

    public void Start()
    {
        if (!configured) Configure(kind, stageIndex);
    }

    public void Configure(EnemyKind newKind, int newStage)
    {
        kind = newKind;
        stageIndex = Mathf.Clamp(newStage, 1, 3);
        if (enemy == null) enemy = GetComponent<Enemy>();
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        enemy.deathDelay = 0f;
        enemy.knockbackResistance = 0f;
        ApplyStats();
        ApplyVisual();
        if (!subscribed && enemy != null)
        {
            enemy.OnDied += HandleEnemyDied;
            subscribed = true;
        }
        configured = true;
    }

    public void ApplyStats()
    {
        if (enemy == null) return;
        string prefix = "enemy." + ConfigKey() + ".";
        int baseHP = 1, hpPerStage = 0, damage = 1, xp = 2;
        float baseSpeed = 2.8f, speedPerStage = 0.2f;
        switch (kind)
        {
            case EnemyKind.CellShadow:
                transform.localScale = Vector3.one * 0.62f;
                break;
            case EnemyKind.SpikeSoldier:
                baseHP = 3; hpPerStage = 1; baseSpeed = 1.65f; speedPerStage = 0.18f; damage = 2; xp = 3;
                enemy.knockbackResistance = GameBalance.Get(prefix + "knockback_resist", 0.9f);
                transform.localScale = Vector3.one * 0.9f;
                break;
            case EnemyKind.RupturedCell:
                baseHP = 4; hpPerStage = 2; baseSpeed = 1.1f; speedPerStage = 0.1f; damage = 2; xp = 5;
                enemy.deathDelay = GameBalance.Get(prefix + "death_delay", 0.45f);
                ruptureExplosionRadius = GameBalance.Get(prefix + "blast_radius", ruptureExplosionRadius);
                ruptureExplosionDamageMultiplier = GameBalance.GetInt(prefix + "blast_damage_mult", ruptureExplosionDamageMultiplier);
                transform.localScale = Vector3.one * 1.05f;
                break;
            case EnemyKind.MicroThrombus:
                baseHP = 8; hpPerStage = 3; baseSpeed = 0.65f; speedPerStage = 0.08f; damage = 2; xp = 6;
                transform.localScale = new Vector3(2.3f, 0.9f, 1f);
                break;
            case EnemyKind.BleedingGap:
                baseHP = 12; hpPerStage = 3; baseSpeed = 0f; speedPerStage = 0f; damage = 1; xp = 5;
                bleedingGapRadius = GameBalance.Get(prefix + "zone_radius", bleedingGapRadius);
                transform.localScale = Vector3.one * 1.25f;
                break;
            case EnemyKind.VenousStasis:
                baseHP = 5; hpPerStage = 2; baseSpeed = 2.2f; speedPerStage = 0.15f; damage = 1; xp = 4;
                transform.localScale = Vector3.one * 0.85f;
                break;
            case EnemyKind.FibrinNet:
                baseHP = 6; hpPerStage = 2; baseSpeed = 1.8f; speedPerStage = 0.12f; damage = 1; xp = 5;
                transform.localScale = Vector3.one * 0.95f;
                break;
            case EnemyKind.PrimitiveCell:
                baseHP = 4; hpPerStage = 2; baseSpeed = 2f; speedPerStage = 0.15f; damage = 2; xp = 5;
                transform.localScale = Vector3.one * 0.8f;
                break;
            case EnemyKind.TargetBlocker:
                baseHP = 8; hpPerStage = 3; baseSpeed = 1.5f; speedPerStage = 0.1f; damage = 2; xp = 7;
                transform.localScale = Vector3.one * 1.1f;
                break;
            case EnemyKind.NodeBoss:
                baseHP = 34; hpPerStage = 12; baseSpeed = 0.7f; speedPerStage = 0.08f; damage = 3; xp = 20;
                transform.localScale = Vector3.one * 2.2f;
                break;
        }
        enemy.maxHP = GameBalance.GetInt(prefix + "hp_base", baseHP) + stageIndex * GameBalance.GetInt(prefix + "hp_per_stage", hpPerStage);
        enemy.moveSpeed = GameBalance.Get(prefix + "speed_base", baseSpeed) + stageIndex * GameBalance.Get(prefix + "speed_per_stage", speedPerStage);
        enemy.contactDamage = GameBalance.GetInt(prefix + "damage", damage);
        enemy.xpValue = GameBalance.GetInt(prefix + "xp", xp);

        // Higher stages reward the extra pressure with faster experience gain.
        float stageXpMultiplier = 1f + (stageIndex - 1) * GameBalance.Get("enemy.xp_stage_bonus", 0.25f);
        enemy.xpValue = Mathf.Max(1, Mathf.RoundToInt(enemy.xpValue * stageXpMultiplier));
        enemy.currentHP = enemy.maxHP;

        if (kind == EnemyKind.BleedingGap && ownedHazard == null)
            ownedHazard = VesselHazardZone.Create(transform.position, bleedingGapRadius,
                GameBalance.GetInt(prefix + "zone_damage", 1), GameBalance.Get(prefix + "zone_slow", 0.55f),
                gameObject, 9999f, GameBalance.Get(prefix + "zone_tick", 1f));
        
        // 静脉淤血：接触玩家时叠加淤滞层数
        if (kind == EnemyKind.VenousStasis)
        {
            enemy.stasisOnContact = true;
            enemy.stasisStacksOnContact = GameBalance.GetInt(prefix + "stasis_stacks", 1);
        }
        
        // 纤维蛋白网：死亡时创建减速区
        if (kind == EnemyKind.FibrinNet)
        {
            enemy.createNetOnDeath = true;
            enemy.netRadius = GameBalance.Get(prefix + "net_radius", 2.5f);
            enemy.netDuration = GameBalance.Get(prefix + "net_duration", 4f);
            enemy.netSlow = GameBalance.Get(prefix + "net_slow", 0.5f);
        }
        
        // 原始细胞：存活时间越长复制越快
        if (kind == EnemyKind.PrimitiveCell)
        {
            enemy.cloneIntervalBase = GameBalance.Get(prefix + "clone_interval_base", 8f);
            enemy.cloneIntervalMin = GameBalance.Get(prefix + "clone_interval_min", 2f);
            enemy.cloneAccel = GameBalance.Get(prefix + "clone_accel", 0.85f);
            enemy.cloneHpMult = GameBalance.Get(prefix + "clone_hp_mult", 0.6f);
            enemy.isPrimitiveCell = true;
        }
        
        // 靶点遮蔽者：周围敌人免疫暴击
        if (kind == EnemyKind.TargetBlocker)
        {
            enemy.auraRadius = GameBalance.Get(prefix + "aura_radius", 3f);
            enemy.auraDuration = GameBalance.Get(prefix + "aura_duration", 1.5f);
            enemy.isTargetBlocker = true;
        }
    }

    public string ConfigKey()
    {
        switch (kind)
        {
            case EnemyKind.SpikeSoldier: return "spike";
            case EnemyKind.RupturedCell: return "ruptured";
            case EnemyKind.MicroThrombus: return "thrombus";
            case EnemyKind.BleedingGap: return "bleeding";
            case EnemyKind.VenousStasis: return "stasis";
            case EnemyKind.FibrinNet: return "fibrin";
            case EnemyKind.PrimitiveCell: return "primitive";
            case EnemyKind.TargetBlocker: return "blocker";
            case EnemyKind.NodeBoss: return "boss";
            default: return "cell";
        }
    }

    public void ApplyVisual()
    {
        if (spriteRenderer == null) return;

        Color tint = Color.white;
        switch (kind)
        {
            case EnemyKind.CellShadow: tint = new Color(0.22f, 0.78f, 0.95f, 1f); break;
            case EnemyKind.SpikeSoldier: tint = new Color(1f, 0.62f, 0.16f, 1f); break;
            case EnemyKind.RupturedCell: tint = new Color(0.95f, 0.18f, 0.28f, 1f); break;
            case EnemyKind.MicroThrombus: tint = new Color(0.56f, 0.22f, 0.78f, 1f); break;
            case EnemyKind.BleedingGap: tint = new Color(1f, 0.12f, 0.52f, 1f); break;
            case EnemyKind.VenousStasis: tint = new Color(0.4f, 0.2f, 0.6f, 1f); break;
            case EnemyKind.FibrinNet: tint = new Color(0.7f, 0.7f, 0.8f, 1f); break;
            case EnemyKind.PrimitiveCell: tint = new Color(0.3f, 0.8f, 0.4f, 1f); break;
            case EnemyKind.TargetBlocker: tint = new Color(0.2f, 0.2f, 0.3f, 1f); break;
            case EnemyKind.NodeBoss: tint = new Color(0.28f, 0.06f, 0.34f, 1f); break;
        }
        spriteRenderer.color = tint;
        if (enemy != null) enemy.visualColor = tint;
        spriteRenderer.sortingOrder = kind == EnemyKind.NodeBoss ? 4 : 2;
    }

    public void HandleEnemyDied(Enemy deadEnemy)
    {
        if (kind == EnemyKind.RupturedCell)
            StartCoroutine(RupturedDeathRoutine());
    }

    public void OnTriggerEnter2D(Collider2D other)
    {
        if (kind != EnemyKind.MicroThrombus || obstacleSpawned) return;
        PlayerStats stats = other.GetComponent<PlayerStats>();
        if (stats == null) stats = other.GetComponentInParent<PlayerStats>();
        if (stats == null) return;
        obstacleSpawned = true;
        VesselHazardZone.Create(transform.position, GameBalance.Get("enemy.thrombus.zone_radius", 1.55f),
            0, GameBalance.Get("enemy.thrombus.zone_slow", 0.45f), null,
            GameBalance.Get("enemy.thrombus.zone_duration", 4.5f));
    }

    public int ModifyIncomingDamage(int amount)
    {
        if (kind != EnemyKind.SpikeSoldier || amount <= 0) return amount;
#pragma warning disable 0618
        int hitCount = Physics2D.OverlapCircleNonAlloc(transform.position, GameBalance.Get("enemy.spike.pack_radius", 1.05f), nearbyBuffer);
#pragma warning restore 0618
        int nearbyGuards = 0;
        for (int i = 0; i < hitCount; i++)
        {
            EnemyArchetype other = nearbyBuffer[i] == null ? null : nearbyBuffer[i].GetComponent<EnemyArchetype>();
            if (other != null && other != this && other.kind == EnemyKind.SpikeSoldier &&
                other.enemy != null && other.enemy.currentHP > 0 && !other.enemy.deathPending)
                nearbyGuards++;
        }
        if (nearbyGuards <= 0) return amount;
        float multiplier = Mathf.Clamp(1f - nearbyGuards * GameBalance.Get("enemy.spike.pack_reduction", 0.18f),
            GameBalance.Get("enemy.spike.pack_min_mult", 0.45f), 1f);
        return Mathf.Max(1, Mathf.CeilToInt(amount * multiplier));
    }

    public IEnumerator RupturedDeathRoutine()
    {
        yield return new WaitForSecondsRealtime(GameBalance.Get("enemy.ruptured.blast_delay", 0.16f));
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, ruptureExplosionRadius);
        for (int i = 0; i < hits.Length; i++)
        {
            PlayerStats stats = hits[i] == null ? null : hits[i].GetComponent<PlayerStats>();
            if (stats == null && hits[i] != null) stats = hits[i].GetComponentInParent<PlayerStats>();
            if (stats != null) stats.TakeDamage(Mathf.Max(1, enemy.contactDamage * ruptureExplosionDamageMultiplier));
        }
        yield return new WaitForSecondsRealtime(GameBalance.Get("enemy.ruptured.spawn_delay", 0.08f));
        EnemySpawner spawner = FindAnyObjectByType<EnemySpawner>();
        if (spawner != null)
        {
            for (int i = 0; i < GameBalance.GetInt("enemy.ruptured.spawn_count", 2); i++)
            {
                Vector2 offset = Random.insideUnitCircle * GameBalance.Get("enemy.ruptured.spawn_offset", 0.55f);
                spawner.SpawnSpecific(EnemyKind.CellShadow, transform.position + (Vector3)offset);
            }
        }
    }

    public void OnDestroy()
    {
        if (subscribed && enemy != null) enemy.OnDied -= HandleEnemyDied;
    }
}


