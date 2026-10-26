using System.Collections;
using UnityEngine;

public enum EnemyKind
{
    CellShadow,
    SpikeSoldier,
    RupturedCell,
    MicroThrombus,
    BleedingGap,
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
        int baseHP = 1, hpPerStage = 0, damage = 20, xp = 2;
        float baseSpeed = 2.8f, speedPerStage = 0.2f;
        switch (kind)
        {
            case EnemyKind.CellShadow:
                transform.localScale = Vector3.one * 0.62f;
                break;
            case EnemyKind.SpikeSoldier:
                baseHP = 3; hpPerStage = 1; baseSpeed = 1.65f; speedPerStage = 0.18f; damage = 40; xp = 3;
                enemy.knockbackResistance = GameBalance.Get(prefix + "knockback_resist", 0.9f);
                transform.localScale = Vector3.one * 0.9f;
                break;
            case EnemyKind.RupturedCell:
                baseHP = 4; hpPerStage = 2; baseSpeed = 1.1f; speedPerStage = 0.1f; damage = 40; xp = 5;
                enemy.deathDelay = GameBalance.Get(prefix + "death_delay", 0.45f);
                ruptureExplosionRadius = GameBalance.Get(prefix + "blast_radius", ruptureExplosionRadius);
                ruptureExplosionDamageMultiplier = GameBalance.GetInt(prefix + "blast_damage_mult", ruptureExplosionDamageMultiplier);
                transform.localScale = Vector3.one * 1.05f;
                break;
            case EnemyKind.MicroThrombus:
                baseHP = 8; hpPerStage = 3; baseSpeed = 0.65f; speedPerStage = 0.08f; damage = 40; xp = 6;
                transform.localScale = new Vector3(2.3f, 0.9f, 1f);
                break;
            case EnemyKind.BleedingGap:
                baseHP = 12; hpPerStage = 3; baseSpeed = 0f; speedPerStage = 0f; damage = 20; xp = 5;
                bleedingGapRadius = GameBalance.Get(prefix + "zone_radius", bleedingGapRadius);
                transform.localScale = Vector3.one * 1.25f;
                break;
            case EnemyKind.NodeBoss:
                baseHP = 34; hpPerStage = 12; baseSpeed = 0.7f; speedPerStage = 0.08f; damage = 60; xp = 20;
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
                GameBalance.GetInt(prefix + "zone_damage", 20), GameBalance.Get(prefix + "zone_slow", 0.55f),
                gameObject, 9999f, GameBalance.Get(prefix + "zone_tick", 1f));
    }

    public string ConfigKey()
    {
        switch (kind)
        {
            case EnemyKind.SpikeSoldier: return "spike";
            case EnemyKind.RupturedCell: return "ruptured";
            case EnemyKind.MicroThrombus: return "thrombus";
            case EnemyKind.BleedingGap: return "bleeding";
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
        // Game time throughout, so a pause freezes the fuse instead of letting the
        // blast land behind a menu.
        float blastDelay = GameBalance.Get("enemy.ruptured.blast_delay", 0.16f);
        // Telegraph the incoming blast: a ring at the exact damage radius shrinks
        // toward the corpse to count down, so the player can see the danger range.
        StartCoroutine(ShowBlastTelegraph(ruptureExplosionRadius, blastDelay));
        yield return new WaitForSeconds(blastDelay);
        if (IsRunOver) yield break;
        // The blast is centred on the corpse's last position and finds the player by
        // that player-owned collider, so disabling the corpse's own colliders on death
        // does not affect it.
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, ruptureExplosionRadius);
        // One player must take the blast exactly once. A player can own several
        // colliders (self plus child hitboxes), each of which resolves to the same
        // PlayerStats; damaging per collider multiplied the blast (40x2 became 100+).
        PlayerStats alreadyHit = null;
        for (int i = 0; i < hits.Length; i++)
        {
            PlayerStats stats = hits[i] == null ? null : hits[i].GetComponent<PlayerStats>();
            if (stats == null && hits[i] != null) stats = hits[i].GetComponentInParent<PlayerStats>();
            if (stats == null || stats == alreadyHit) continue;
            alreadyHit = stats;
            stats.TakeDamage(enemy.contactDamage * ruptureExplosionDamageMultiplier);
        }
        yield return new WaitForSeconds(GameBalance.Get("enemy.ruptured.spawn_delay", 0.08f));
        // A settled run never spawns its split children.
        if (IsRunOver) yield break;
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

    // A shrinking ring around the corpse marks the blast radius and the remaining
    // fuse time. The ring starts at the damage radius and collapses inward, so the
    // moment the ring closes the blast fires. Paused runs keep it frozen too.
    IEnumerator ShowBlastTelegraph(float radius, float duration)
    {
        if (radius <= 0f || duration <= 0f) yield break;
        GameObject obj = new GameObject("RupturedBlastTelegraph");
        obj.transform.position = transform.position;
        SpriteRenderer ring = obj.AddComponent<SpriteRenderer>();
        ring.sprite = VesselHazardZone.GetRingSprite();
        ring.sortingOrder = 3;
        obj.transform.localScale = Vector3.one * (radius * 2f);
        ring.color = new Color(1f, 0.30f, 0.16f, 0.85f);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float k = Mathf.Clamp01(elapsed / duration);
            // Pulse the alpha and collapse the ring toward the centre.
            float alpha = 0.55f + 0.40f * Mathf.Abs(Mathf.Sin(k * Mathf.PI * 5f));
            ring.color = new Color(1f, 0.30f, 0.16f, Mathf.Clamp01(alpha));
            obj.transform.localScale = Vector3.one * (radius * 2f) * (1f - 0.35f * k);
            yield return null;
        }
        Destroy(obj);
    }

    bool IsRunOver
    {
        get { return GameManager.Instance != null && GameManager.Instance.gameEnded; }
    }

    public void OnDestroy()
    {
        if (subscribed && enemy != null) enemy.OnDied -= HandleEnemyDied;
    }
}


