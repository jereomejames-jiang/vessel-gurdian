using UnityEngine;

public class VesselNodeBoss : MonoBehaviour
{
    public Enemy enemy;
    public EnemySpawner spawner;
    public Transform player;
    public SpriteRenderer spriteRenderer;
    public float summonInterval = 9f;
    public float chargeInterval = 12f;
    public float chargeWarning = 0.8f;
    public float chargeDuration = 1f;
    public float chargeSpeed = 8f;
    public float summonTimer;
    public float chargeTimer;
    public float chargeRemaining;
    public float warningRemaining;
    public Vector2 chargeDirection;
    public bool charging;
    public bool halfHealthTriggered;
    public bool defeated;
    public Color normalColor = new Color(0.28f, 0.06f, 0.34f, 1f);
    public Color warningColor = new Color(1f, 0.12f, 0.16f, 1f);

    public void Awake()
    {
        summonInterval = GameBalance.Get("boss.summon_interval", summonInterval);
        chargeInterval = GameBalance.Get("boss.charge_interval", chargeInterval);
        chargeWarning = GameBalance.Get("boss.charge_telegraph", chargeWarning);
        chargeDuration = GameBalance.Get("boss.charge_duration", chargeDuration);
        chargeSpeed = GameBalance.Get("boss.charge_speed", chargeSpeed);
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
    }

    public void HandleEnemyDied(Enemy deadEnemy)
    {
        NotifyDefeated();
    }

    public void Start()
    {
        summonTimer = summonInterval;
        chargeTimer = chargeInterval;
        if (spriteRenderer != null) spriteRenderer.color = normalColor;
    }

    public void Update()
    {
        if (enemy == null || enemy.currentHP <= 0 || Time.timeScale <= 0f) return;

        if (player == null)
        {
            PlayerController controller = FindAnyObjectByType<PlayerController>();
            if (controller != null) player = controller.transform;
        }

        if (!halfHealthTriggered && enemy.currentHP <= enemy.maxHP * GameBalance.Get("boss.reinforce_hp_percent", 0.5f))
        {
            halfHealthTriggered = true;
            SummonRupturedCells();
        }

        summonTimer -= Time.deltaTime;
        if (summonTimer <= 0f)
        {
            summonTimer = summonInterval;
            SummonThrombus();
        }

        if (!charging)
        {
            chargeTimer -= Time.deltaTime;
            if (chargeTimer <= 0f && player != null)
            {
                chargeTimer = chargeInterval;
                BeginCharge();
            }
        }
        else
        {
            if (warningRemaining > 0f)
            {
                warningRemaining -= Time.deltaTime;
                if (warningRemaining <= 0f)
                {
                    chargeRemaining = chargeDuration;
                    if (spriteRenderer != null) spriteRenderer.color = warningColor;
                }
            }
            else
            {
                chargeRemaining -= Time.deltaTime;
                float slow = enemy.moveSpeed <= 0f ? 1f : enemy.EffectiveMoveSpeed / enemy.moveSpeed;
                transform.position += (Vector3)(chargeDirection * chargeSpeed * slow * Time.deltaTime);
                if (chargeRemaining <= 0f)
                {
                    charging = false;
                    if (spriteRenderer != null) spriteRenderer.color = normalColor;
                }
            }
        }
    }

    public void FixedUpdate()
    {
        if (enemy == null || player == null || charging || warningRemaining > 0f ||
            enemy.currentHP <= 0 || Time.timeScale <= 0f) return;

        Vector2 direction = (player.position - transform.position).normalized;
        transform.position += (Vector3)(direction * enemy.EffectiveMoveSpeed * Time.fixedDeltaTime);
    }

    public void BeginCharge()
    {
        if (player == null) return;
        chargeDirection = (player.position - transform.position).normalized;
        if (chargeDirection.sqrMagnitude < 0.01f) chargeDirection = Vector2.up;
        charging = true;
        warningRemaining = chargeWarning;
        if (spriteRenderer != null) spriteRenderer.color = warningColor;
    }

    public void SummonThrombus()
    {
        if (spawner == null) spawner = FindAnyObjectByType<EnemySpawner>();
        if (spawner == null) return;
        for (int i = 0; i < GameBalance.GetInt("boss.summon_count", 2); i++)
        {
            Vector2 offset = Random.insideUnitCircle * GameBalance.Get("boss.summon_offset", 1.2f);
            spawner.SpawnSpecific(EnemyKind.MicroThrombus, transform.position + (Vector3)offset);
        }
    }

    public void SummonRupturedCells()
    {
        if (spawner == null) spawner = FindAnyObjectByType<EnemySpawner>();
        if (spawner == null) return;
        for (int i = 0; i < GameBalance.GetInt("boss.reinforce_count", 2); i++)
        {
            Vector2 offset = Random.insideUnitCircle * GameBalance.Get("boss.reinforce_offset", 1.5f);
            spawner.SpawnSpecific(EnemyKind.RupturedCell, transform.position + (Vector3)offset);
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
    }
}
