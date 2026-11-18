using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Enemy : MonoBehaviour
{
    public int maxHP = 3;
    public float moveSpeed = 2f;
    public int contactDamage = 1;
    public int xpValue = 1;
    public GameObject xpOrbPrefab;
    public bool externalMovement;
    public float knockbackResistance;
    public float deathDelay;
    public bool deathPending;
    public float slowMultiplier = 1f;
    public float slowUntil;
    // 迅捷拾取物（v3 §9）：全场敌人减速 30%，10s。
    public static float globalSlowMultiplier = 1f;
    public static float globalSlowUntil;
    public float EffectiveMoveSpeed => moveSpeed * (Time.time < slowUntil ? slowMultiplier : 1f) *
        (Time.time < globalSlowUntil ? globalSlowMultiplier : 1f);
    public event Action<Enemy> OnDied;

    public int currentHP;
    public Transform player;
    public Rigidbody2D rb;
    public SpriteRenderer spriteRenderer;
    public Color visualColor = Color.white;
    public float hitFlashTimer;
    public float hitFlashDuration = 0.08f;
    
    // 静脉淤血/动脉栓塞：接触时叠加淤滞与减速（§2.2/§3.2「靠近后叠加减速与淤滞」）。
    public bool stasisOnContact;
    public int stasisStacksOnContact = 1;
    // R1-02：接触减速复用 PlayerController.ApplyTimedSlow 通道；判定用现有接触碰撞半径（slow_radius 键已删，F60）。
    public float contactSlowMult = 0.6f;
    public float contactSlowDuration = 2f;
    
    // 纤维蛋白网：死亡时创建减速区
    public bool createNetOnDeath;
    public float netRadius = 2.5f;
    public float netDuration = 4f;
    public float netSlow = 0.5f;
    
    // 原始细胞：复制加速
    public bool isPrimitiveCell;
    public float cloneIntervalBase = 8f;
    public float cloneIntervalMin = 2f;
    public float cloneAccel = 0.85f;
    public float cloneHpMult = 0.6f;
    public int cloneMaxCount = 6;
    public float aliveTime;
    public float currentCloneInterval;
    public float cloneTimer;
    
    // 靶点遮蔽者：为光环内的敌人减伤
    public bool isTargetBlocker;
    public float auraRadius = 3f;
    public float auraDamageReduction = 0.2f;

    // 高压飞沫（v3 §3.2）：从左往右批次弹幕，固定方向匀速，不索敌。
    public bool isPressureSpray;
    public Vector2 sprayDirection = Vector2.right;
    // 剥落栓子（v3 §3.2）：身后留下持续减速的地面痕迹。
    public bool dropsTrail;
    public float trailInterval = 0.5f;
    public float trailRadius = 0.8f;
    public float trailSlow = 0.6f;
    public float trailDuration = 3f;
    float trailTimer;
    // Boss 与 Boss 召唤结节不参与拾取物掉落。
    public bool noPickupDrop;

    void Awake()
    {
        currentHP = maxHP;
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    // Enemies steer straight at the player and use trigger colliders, so without
    // a separation force they pile into a single overlapping blob.
    static readonly List<Enemy> alive = new List<Enemy>();
    const float SeparationRadius = 0.55f;
    const float SeparationStrength = 1.6f;

    void OnEnable() { alive.Add(this); }
    void OnDisable() { alive.Remove(this); }

    Vector2 ComputeSeparation()
    {
        Vector2 pos = rb != null ? rb.position : (Vector2)transform.position;
        Vector2 push = Vector2.zero;
        for (int i = 0; i < alive.Count; i++)
        {
            Enemy other = alive[i];
            if (other == this || other == null || other.currentHP <= 0) continue;
            Vector2 otherPos = other.rb != null ? other.rb.position : (Vector2)other.transform.position;
            Vector2 delta = pos - otherPos;
            float dist = delta.magnitude;
            if (dist >= SeparationRadius) continue;
            if (dist < 0.0001f)
            {
                delta = UnityEngine.Random.insideUnitCircle;
                dist = delta.magnitude;
                if (dist < 0.0001f) continue;
            }
            push += delta / dist * (1f - dist / SeparationRadius);
        }
        if (push.sqrMagnitude > 1f) push.Normalize();
        return push * SeparationStrength;
    }

    void Start()
    {
        PlayerController controller = FindAnyObjectByType<PlayerController>();
        if (controller != null) player = controller.transform;
    }

    void FixedUpdate()
    {
        if (externalMovement || deathPending || currentHP <= 0) return;
        if (player == null)
        {
            PlayerController controller = FindAnyObjectByType<PlayerController>();
            if (controller != null) player = controller.transform;
        }

        if (player == null) return;

        if (isPressureSpray)
        {
            Vector2 sprayVelocity = sprayDirection.normalized * EffectiveMoveSpeed;
            if (rb != null) rb.MovePosition(rb.position + sprayVelocity * Time.fixedDeltaTime);
            else transform.position += (Vector3)(sprayVelocity * Time.fixedDeltaTime);
            return;
        }

        Vector3 direction = (player.position - transform.position).normalized;
        Vector2 velocity = (Vector2)direction * EffectiveMoveSpeed + ComputeSeparation();
        if (rb != null) rb.MovePosition(rb.position + velocity * Time.fixedDeltaTime);
        else transform.position += (Vector3)(velocity * Time.fixedDeltaTime);
    }

    void Update()
    {
        if (spriteRenderer == null) return;
        if (hitFlashTimer > 0f)
        {
            hitFlashTimer -= Time.deltaTime;
            spriteRenderer.color = Color.Lerp(Color.white, visualColor, 1f - Mathf.Clamp01(hitFlashTimer / hitFlashDuration));
        }
        else if (spriteRenderer.color != visualColor) spriteRenderer.color = visualColor;
        
        // 原始细胞：存活时间越长，复制越快
        if (isPrimitiveCell && currentHP > 0 && !deathPending)
        {
            aliveTime += Time.deltaTime;
            currentCloneInterval = Mathf.Max(cloneIntervalMin, cloneIntervalBase * Mathf.Pow(cloneAccel, aliveTime / 10f));
            cloneTimer += Time.deltaTime;
            if (cloneTimer >= currentCloneInterval)
            {
                cloneTimer = 0f;
                if (CountPrimitiveCells() < cloneMaxCount) ClonePrimitiveCell();
            }
        }

        if (currentHP <= 0 || deathPending) return;

        // 高压飞沫：飞出战场后移除（批次弹幕不回头）。
        if (isPressureSpray && player != null &&
            (transform.position - player.position).sqrMagnitude > 32f * 32f)
        {
            Destroy(gameObject);
            return;
        }

        // 剥落栓子：身后留下持续减速痕迹。
        if (dropsTrail)
        {
            trailTimer -= Time.deltaTime;
            if (trailTimer <= 0f)
            {
                trailTimer = trailInterval;
                VesselHazardZone.Create(transform.position, trailRadius, 0, trailSlow, null, trailDuration, 0.5f);
            }
        }
    }

    // 复制体本身也会继续复制，所以按场上原始细胞总数封顶，避免指数增长挤满屏幕。
    int CountPrimitiveCells()
    {
        int count = 0;
        for (int i = 0; i < alive.Count; i++)
        {
            Enemy other = alive[i];
            if (other != null && other != this && other.isPrimitiveCell && other.currentHP > 0 && !other.deathPending) count++;
        }
        return count + 1;
    }
    
    void ClonePrimitiveCell()
    {
        EnemySpawner spawner = FindAnyObjectByType<EnemySpawner>();
        if (spawner == null || player == null) return;
        
        Vector2 offset = UnityEngine.Random.insideUnitCircle * 1.5f;
        GameObject clone = spawner.SpawnSpecific(EnemyKind.PrimitiveCell, transform.position + (Vector3)offset);
        if (clone != null)
        {
            Enemy cloneEnemy = clone.GetComponent<Enemy>();
            if (cloneEnemy != null)
            {
                cloneEnemy.maxHP = Mathf.Max(1, Mathf.CeilToInt(maxHP * cloneHpMult));
                cloneEnemy.currentHP = cloneEnemy.maxHP;
            }
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        PlayerStats stats = other.GetComponent<PlayerStats>();
        if (stats == null) stats = other.GetComponentInParent<PlayerStats>();
        if (stats != null && currentHP > 0 && !deathPending)
        {
            stats.TakeDamage(contactDamage);
            // 静脉淤血/动脉栓塞：接触时叠加淤滞层数与短暂减速（R1-02）。
            if (stasisOnContact)
            {
                stats.AddStasisStack(stasisStacksOnContact);
                PlayerController controller = stats.GetComponent<PlayerController>();
                if (controller != null) controller.ApplyTimedSlow(contactSlowMult, contactSlowDuration);
            }
        }
    }

    public void TakeDamage(int amount)
    {
        if (amount <= 0 || currentHP <= 0 || deathPending) return;

        EnemyArchetype archetype = GetComponent<EnemyArchetype>();
        if (archetype != null) amount = archetype.ModifyIncomingDamage(amount);
        
        // 靶点遮蔽者光环：附近敌人减伤
        amount = ApplyTargetBlockerReduction(amount);

        hitFlashTimer = hitFlashDuration;
        currentHP -= amount;
        if (currentHP <= 0)
        {
            currentHP = 0;
            deathPending = true;
            OnDied?.Invoke(this);
            if (deathDelay > 0f)
            {
                Collider2D[] colliders = GetComponents<Collider2D>();
                for (int i = 0; i < colliders.Length; i++) colliders[i].enabled = false;
                if (rb != null) rb.simulated = false;
                StartCoroutine(FinalizeDeathAfterDelay());
            }
            else FinalizeDeath();
        }
    }
    
    int ApplyTargetBlockerReduction(int amount)
    {
        if (amount <= 0) return amount;
        Vector2 pos = rb != null ? rb.position : (Vector2)transform.position;
        for (int i = 0; i < alive.Count; i++)
        {
            Enemy blocker = alive[i];
            if (blocker == null || blocker == this || !blocker.isTargetBlocker || blocker.currentHP <= 0) continue;
            Vector2 blockerPos = blocker.rb != null ? blocker.rb.position : (Vector2)blocker.transform.position;
            if ((pos - blockerPos).sqrMagnitude > blocker.auraRadius * blocker.auraRadius) continue;
            // 多个遮蔽者不叠加，取一层减伤
            return Mathf.Max(1, Mathf.RoundToInt(amount * (1f - blocker.auraDamageReduction)));
        }
        return amount;
    }

    public IEnumerator FinalizeDeathAfterDelay()
    {
        yield return new WaitForSecondsRealtime(Mathf.Max(0f, deathDelay));
        FinalizeDeath();
    }

    public void FinalizeDeath()
    {
        if (!deathPending) return;
        deathPending = false;
        if (GameManager.Instance != null) GameManager.Instance.RegisterEnemyDefeated(this);
        
        // 纤维蛋白网：死亡时创建减速区
        if (createNetOnDeath)
        {
            VesselHazardZone.Create(transform.position, netRadius, 0, netSlow, null, netDuration, 0.5f);
        }
        
        if (xpOrbPrefab != null)
        {
            GameObject orbObject = Instantiate(xpOrbPrefab, transform.position, Quaternion.identity);
            ExperienceOrb orb = orbObject.GetComponent<ExperienceOrb>();
            if (orb != null) orb.xpValue = Mathf.Max(1, xpValue);
        }
        // 特殊拾取物（v3 §9）：血包/迅捷/磁铁。掉落概率未给数值→3% 并标旗。
        if (!noPickupDrop && UnityEngine.Random.value < GameBalance.Get("pickup.drop_chance", 0.03f))
            PickupItem.SpawnRandom(transform.position);
        Destroy(gameObject);
    }

    public void IncreaseMaxHP(int amount)
    {
        if (amount <= 0) return;
        maxHP += amount;
        currentHP += amount;
    }

    public void ApplyKnockback(Vector2 direction, float force)
    {
        if (deathPending || force <= 0f || direction.sqrMagnitude < 0.001f) return;
        float effectiveForce = force * Mathf.Clamp01(1f - knockbackResistance);
        if (effectiveForce <= 0f) return;
        Vector2 normalized = direction.normalized;
        if (rb != null && rb.bodyType == RigidbodyType2D.Dynamic)
            rb.AddForce(normalized * effectiveForce, ForceMode2D.Impulse);
        else
            transform.position += (Vector3)(normalized * effectiveForce);
    }

    public void ApplySlow(float multiplier, float duration)
    {
        if (currentHP <= 0 || duration <= 0f) return;
        multiplier = Mathf.Clamp(multiplier, 0.05f, 1f);
        slowMultiplier = Time.time >= slowUntil ? multiplier : Mathf.Min(slowMultiplier, multiplier);
        slowUntil = Mathf.Max(slowUntil, Time.time + duration);
    }
}
