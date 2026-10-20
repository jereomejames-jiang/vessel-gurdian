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
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        PlayerStats stats = other.GetComponent<PlayerStats>();
        if (stats == null) stats = other.GetComponentInParent<PlayerStats>();
        if (stats != null && currentHP > 0 && !deathPending) stats.TakeDamage(contactDamage);
    }

    public void TakeDamage(int amount)
    {
        if (amount <= 0 || currentHP <= 0 || deathPending) return;

        EnemyArchetype archetype = GetComponent<EnemyArchetype>();
        if (archetype != null) amount = archetype.ModifyIncomingDamage(amount);

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
