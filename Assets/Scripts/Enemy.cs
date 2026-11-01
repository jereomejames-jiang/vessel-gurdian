using System;
using System.Collections;
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
    public float EffectiveMoveSpeed => moveSpeed * (Time.time < slowUntil ? slowMultiplier : 1f);
    public event Action<Enemy> OnDied;

    public int currentHP;
    public Transform player;
    public Rigidbody2D rb;
    public SpriteRenderer spriteRenderer;
    public Color visualColor = Color.white;
    public float hitFlashTimer;
    public float hitFlashDuration = 0.08f;
    
    // 静脉淤血：接触时叠加淤滞
    public bool stasisOnContact;
    public int stasisStacksOnContact = 1;
    
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
    public float aliveTime;
    public float currentCloneInterval;
    public float cloneTimer;
    
    // 靶点遮蔽者：周围敌人免疫暴击
    public bool isTargetBlocker;
    public float auraRadius = 3f;
    public float auraDuration = 1.5f;

    void Awake()
    {
        currentHP = maxHP;
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
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

        Vector3 direction = (player.position - transform.position).normalized;
        if (rb != null) rb.MovePosition(rb.position + (Vector2)direction * EffectiveMoveSpeed * Time.fixedDeltaTime);
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
                ClonePrimitiveCell();
            }
        }
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
            // 静脉淤血：接触时叠加淤滞层数
            if (stasisOnContact)
            {
                stats.AddStasisStack(stasisStacksOnContact);
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
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, 0.1f);
        for (int i = 0; i < hits.Length; i++)
        {
            Enemy blocker = hits[i] == null ? null : hits[i].GetComponent<Enemy>();
            if (blocker != null && blocker.isTargetBlocker && blocker.currentHP > 0)
            {
                // 检查是否在靶点遮蔽者光环范围内
                Collider2D[] auraHits = Physics2D.OverlapCircleAll(blocker.transform.position, blocker.auraRadius);
                for (int j = 0; j < auraHits.Length; j++)
                {
                    if (auraHits[j] != null && auraHits[j].gameObject == gameObject)
                    {
                        // 在光环内，减伤50%
                        return Mathf.Max(1, amount / 2);
                    }
                }
            }
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
