using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum BossType
{
    WineStain,      // Boss1: 葡萄酒样色斑 (毛细血管)
    VaricoseMedusa  // Boss2: 曲张美杜莎 (静脉)
}

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
    public float chargeTrailDuration = 3f;
    public int chargeTrailDamage = 1;
    
    public float plaqueInterval = 18f;
    public float plaqueWarning = 1f;
    public float plaqueBlastRadius = 2.5f;
    public int plaqueBlastDamage = 3;
    public int plaqueMaxThicken = 3;
    
    [Header("Boss2 - 曲张美杜莎")]
    public float snakeInterval = 9f;
    public int snakePaths = 3;
    public float snakeWarning = 0.8f;
    public float snakeSweepDelay = 1.2f;
    public int snakeDamage = 20;
    public int snakeStasisStacks = 1;
    public float snakeTrailDuration = 3f;
    public float snakeTrailSlow = 0.5f;
    
    public float gazeInterval = 12f;
    public float gazeWarning = 1f;
    public float gazeDuration = 1.2f;
    public int gazeStasisPerTick = 1;
    public int gazeRootStacks = 3;
    public float gazeRootDuration = 0.8f;
    
    public float valveInterval = 18f;
    public float valveWarning = 1f;
    public float valveDuration = 2.5f;
    public int valveNodeCount = 2;
    public int valveNodeHP = 5;
    public float valvePoolRadius = 1.8f;
    public float valvePoolSlow = 0.6f;
    public float valveMinInterval = 1.5f;
    
    [Header("Runtime State")]
    public float spreadTimer;
    public float chargeTimer;
    public float plaqueTimer;
    public float snakeTimer;
    public float gazeTimer;
    public float valveTimer;
    
    public bool charging;
    public float chargeRemaining;
    public float warningRemaining;
    public Vector2 chargeDirection;
    
    public bool gazing;
    public float gazeRemaining;
    public float gazeWarningRemaining;
    public Vector2 gazeDirection;
    
    public bool halfHealthTriggered;
    public bool defeated;
    
    public List<BossSpreadLine> activeSpreads = new List<BossSpreadLine>();
    public List<BossPlaque> activePlaques = new List<BossPlaque>();
    public List<BossSnakeHair> activeSnakes = new List<BossSnakeHair>();
    public List<BossValve> activeValves = new List<BossValve>();
    
    public Color normalColor = new Color(0.28f, 0.06f, 0.34f, 1f);
    public Color warningColor = new Color(1f, 0.12f, 0.16f, 1f);
    public Color boss2Color = new Color(0.6f, 0.2f, 0.8f, 1f);

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
        
        // 根据关卡设置Boss类型：关卡1=葡萄酒样色斑，关卡2=曲张美杜莎，关卡3=葡萄酒样色斑
        if (spawner != null)
        {
            bossType = spawner.stageIndex == 2 ? BossType.VaricoseMedusa : BossType.WineStain;
        }
        
        LoadBossConfig();
    }
    
    void LoadBossConfig()
    {
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
            plaqueMaxThicken = GameBalance.GetInt("boss1.plaque_max_thicken", plaqueMaxThicken);
        }
        else
        {
            snakeInterval = GameBalance.Get("boss2.snake_interval", snakeInterval);
            snakePaths = GameBalance.GetInt("boss2.snake_paths", snakePaths);
            snakeWarning = GameBalance.Get("boss2.snake_telegraph", snakeWarning);
            snakeSweepDelay = GameBalance.Get("boss2.snake_sweep_delay", snakeSweepDelay);
            snakeDamage = GameBalance.GetInt("boss2.snake_damage", snakeDamage);
            snakeStasisStacks = GameBalance.GetInt("boss2.snake_stasis_stacks", snakeStasisStacks);
            snakeTrailDuration = GameBalance.Get("boss2.snake_trail_duration", snakeTrailDuration);
            snakeTrailSlow = GameBalance.Get("boss2.snake_trail_slow", snakeTrailSlow);
            
            gazeInterval = GameBalance.Get("boss2.gaze_interval", gazeInterval);
            gazeWarning = GameBalance.Get("boss2.gaze_telegraph", gazeWarning);
            gazeDuration = GameBalance.Get("boss2.gaze_duration", gazeDuration);
            gazeStasisPerTick = GameBalance.GetInt("boss2.gaze_stasis_per_tick", gazeStasisPerTick);
            gazeRootStacks = GameBalance.GetInt("boss2.gaze_root_stacks", gazeRootStacks);
            gazeRootDuration = GameBalance.Get("boss2.gaze_root_duration", gazeRootDuration);
            
            valveInterval = GameBalance.Get("boss2.valve_interval", valveInterval);
            valveWarning = GameBalance.Get("boss2.valve_telegraph", valveWarning);
            valveDuration = GameBalance.Get("boss2.valve_duration", valveDuration);
            valveNodeCount = GameBalance.GetInt("boss2.valve_node_count", valveNodeCount);
            valveNodeHP = GameBalance.GetInt("boss2.valve_node_hp", valveNodeHP);
            valvePoolRadius = GameBalance.Get("boss2.valve_pool_radius", valvePoolRadius);
            valvePoolSlow = GameBalance.Get("boss2.valve_pool_slow", valvePoolSlow);
            valveMinInterval = GameBalance.Get("boss2.valve_min_interval", valveMinInterval);
        }
    }

    public void HandleEnemyDied(Enemy deadEnemy)
    {
        NotifyDefeated();
    }

    public void Start()
    {
        if (bossType == BossType.WineStain)
        {
            spreadTimer = spreadInterval;
            chargeTimer = chargeInterval;
            plaqueTimer = plaqueInterval;
        }
        else
        {
            snakeTimer = snakeInterval;
            gazeTimer = gazeInterval;
            valveTimer = valveInterval;
        }
        
        if (spriteRenderer != null)
            spriteRenderer.color = bossType == BossType.WineStain ? normalColor : boss2Color;
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

        if (bossType == BossType.WineStain)
            UpdateBoss1();
        else
            UpdateBoss2();
            
        UpdateActiveEffects();
    }
    
    void UpdateBoss1()
    {
        spreadTimer -= Time.deltaTime;
        if (spreadTimer <= 0f)
        {
            spreadTimer = spreadInterval;
            StartCoroutine(SpreadCapillaries());
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
            UpdateCharge();
        }
        
        plaqueTimer -= Time.deltaTime;
        if (plaqueTimer <= 0f)
        {
            plaqueTimer = plaqueInterval;
            ThickenPlaques();
        }
    }
    
    void UpdateBoss2()
    {
        snakeTimer -= Time.deltaTime;
        if (snakeTimer <= 0f && player != null)
        {
            snakeTimer = snakeInterval;
            StartCoroutine(SnakeHairAttack());
        }
        
        if (!gazing)
        {
            gazeTimer -= Time.deltaTime;
            if (gazeTimer <= 0f && player != null)
            {
                gazeTimer = gazeInterval;
                BeginGaze();
            }
        }
        else
        {
            UpdateGaze();
        }
        
        valveTimer -= Time.deltaTime;
        if (valveTimer <= 0f && player != null)
        {
            valveTimer = valveInterval;
            StartCoroutine(ValveBackflow());
        }
    }
    
    void UpdateActiveEffects()
    {
        for (int i = activeSpreads.Count - 1; i >= 0; i--)
        {
            if (activeSpreads[i] != null && !activeSpreads[i].IsExpired())
                activeSpreads[i].Update();
            else
                activeSpreads.RemoveAt(i);
        }
        
        for (int i = activeSnakes.Count - 1; i >= 0; i--)
        {
            if (activeSnakes[i] != null && !activeSnakes[i].IsExpired())
                activeSnakes[i].Update();
            else
                activeSnakes.RemoveAt(i);
        }
    }

    public void FixedUpdate()
    {
        if (enemy == null || player == null || charging || gazing ||
            enemy.currentHP <= 0 || Time.timeScale <= 0f) return;

        Vector2 direction = (player.position - transform.position).normalized;
        transform.position += (Vector3)(direction * enemy.EffectiveMoveSpeed * Time.fixedDeltaTime);
    }
    
    #region Boss1 - 葡萄酒样色斑
    
    IEnumerator SpreadCapillaries()
    {
        int count = Random.Range(spreadCountMin, spreadCountMax + 1);
        for (int i = 0; i < count; i++)
        {
            Vector2 direction = Random.insideUnitCircle.normalized;
            BossSpreadLine line = new BossSpreadLine();
            line.startPos = transform.position;
            line.direction = direction;
            line.length = spreadLength;
            line.chargeTime = spreadChargeTime;
            line.damage = spreadDamage;
            line.slowMult = spreadSlowMult;
            line.slowDuration = spreadSlowDuration;
            line.lifetime = 5f;
            activeSpreads.Add(line);
        }
        yield return null;
    }
    
    void BeginCharge()
    {
        if (player == null) return;
        chargeDirection = (player.position - transform.position).normalized;
        if (chargeDirection.sqrMagnitude < 0.01f) chargeDirection = Vector2.up;
        charging = true;
        warningRemaining = chargeWarning;
        if (spriteRenderer != null) spriteRenderer.color = warningColor;
    }
    
    void UpdateCharge()
    {
        if (warningRemaining > 0f)
        {
            warningRemaining -= Time.deltaTime;
            if (warningRemaining <= 0f)
            {
                chargeRemaining = chargeDuration;
            }
        }
        else
        {
            chargeRemaining -= Time.deltaTime;
            float slow = enemy.moveSpeed <= 0f ? 1f : enemy.EffectiveMoveSpeed / enemy.moveSpeed;
            Vector3 oldPos = transform.position;
            transform.position += (Vector3)(chargeDirection * chargeSpeed * slow * Time.deltaTime);
            
            BossChargeTrail trail = new BossChargeTrail();
            trail.position = oldPos;
            trail.radius = 0.5f;
            trail.damage = chargeTrailDamage;
            trail.lifetime = chargeTrailDuration;
            
            if (chargeRemaining <= 0f)
            {
                charging = false;
                if (spriteRenderer != null) spriteRenderer.color = normalColor;
            }
        }
    }
    
    void ThickenPlaques()
    {
        int count = Mathf.Min(2, plaqueMaxThicken - activePlaques.Count);
        for (int i = 0; i < count; i++)
        {
            Vector2 offset = Random.insideUnitCircle * 3f;
            BossPlaque plaque = new BossPlaque();
            plaque.position = transform.position + (Vector3)offset;
            plaque.warningTime = plaqueWarning;
            plaque.blastRadius = plaqueBlastRadius;
            plaque.blastDamage = plaqueBlastDamage;
            plaque.thickenCount = 0;
            plaque.maxThicken = plaqueMaxThicken;
            activePlaques.Add(plaque);
        }
    }
    
    #endregion
    
    #region Boss2 - 曲张美杜莎
    
    IEnumerator SnakeHairAttack()
    {
        for (int i = 0; i < snakePaths; i++)
        {
            Vector2 direction = Random.insideUnitCircle.normalized;
            BossSnakeHair snake = new BossSnakeHair();
            snake.startPos = transform.position;
            snake.direction = direction;
            snake.length = 4f;
            snake.warningTime = snakeWarning;
            snake.sweepDelay = snakeSweepDelay;
            snake.damage = snakeDamage;
            snake.stasisStacks = snakeStasisStacks;
            snake.trailDuration = snakeTrailDuration;
            snake.trailSlow = snakeTrailSlow;
            activeSnakes.Add(snake);
        }
        yield return new WaitForSeconds(snakeWarning + snakeSweepDelay);
    }
    
    void BeginGaze()
    {
        if (player == null) return;
        gazeDirection = (player.position - transform.position).normalized;
        if (gazeDirection.sqrMagnitude < 0.01f) gazeDirection = Vector2.up;
        gazing = true;
        gazeWarningRemaining = gazeWarning;
        if (spriteRenderer != null) spriteRenderer.color = warningColor;
    }
    
    void UpdateGaze()
    {
        if (gazeWarningRemaining > 0f)
        {
            gazeWarningRemaining -= Time.deltaTime;
            if (gazeWarningRemaining <= 0f)
            {
                gazeRemaining = gazeDuration;
            }
        }
        else
        {
            gazeRemaining -= Time.deltaTime;
            
            if (player != null)
            {
                Vector2 toPlayer = (player.position - transform.position).normalized;
                float dot = Vector2.Dot(gazeDirection, toPlayer);
                if (dot > 0.5f)
                {
                    PlayerStats stats = player.GetComponent<PlayerStats>();
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
            }
        }
    }
    
    IEnumerator ValveBackflow()
    {
        for (int i = 0; i < valveNodeCount; i++)
        {
            Vector2 offset = Random.insideUnitCircle * 4f;
            BossValve valve = new BossValve();
            valve.position = transform.position + (Vector3)offset;
            valve.warningTime = valveWarning;
            valve.duration = valveDuration;
            valve.nodeHP = valveNodeHP;
            valve.poolRadius = valvePoolRadius;
            valve.poolSlow = valvePoolSlow;
            activeValves.Add(valve);
        }
        yield return new WaitForSeconds(valveWarning + valveDuration);
    }
    
    #endregion
    
    void SummonReinforcements()
    {
        if (spawner == null) spawner = FindAnyObjectByType<EnemySpawner>();
        if (spawner == null) return;
        
        if (bossType == BossType.WineStain)
        {
            for (int i = 0; i < 2; i++)
            {
                Vector2 offset = Random.insideUnitCircle * 1.5f;
                spawner.SpawnSpecific(EnemyKind.RupturedCell, transform.position + (Vector3)offset);
            }
        }
        else
        {
            for (int i = 0; i < 3; i++)
            {
                Vector2 offset = Random.insideUnitCircle * 1.2f;
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
    
    public void Update()
    {
        elapsed += UnityEngine.Time.deltaTime;
        if (!charged && elapsed >= chargeTime)
        {
            charged = true;
        }
    }
    
    public bool IsExpired()
    {
        return elapsed >= lifetime;
    }
}

public class BossChargeTrail
{
    public Vector3 position;
    public float radius;
    public int damage;
    public float lifetime;
    public float elapsed;
    
    public bool IsExpired()
    {
        return elapsed >= lifetime;
    }
}

public class BossPlaque
{
    public Vector3 position;
    public float warningTime;
    public float elapsed;
    public float blastRadius;
    public int blastDamage;
    public int thickenCount;
    public int maxThicken;
    public bool exploded;
    
    public void Update()
    {
        elapsed += UnityEngine.Time.deltaTime;
    }
    
    public bool IsExpired()
    {
        return exploded || thickenCount >= maxThicken;
    }
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
    
    public void Update()
    {
        elapsed += UnityEngine.Time.deltaTime;
        if (!swept && elapsed >= warningTime + sweepDelay)
        {
            swept = true;
        }
    }
    
    public bool IsExpired()
    {
        return elapsed >= warningTime + sweepDelay + trailDuration;
    }
}

public class BossValve
{
    public Vector3 position;
    public float warningTime;
    public float duration;
    public float elapsed;
    public int nodeHP;
    public float poolRadius;
    public float poolSlow;
    public bool active;
    
    public void Update()
    {
        elapsed += UnityEngine.Time.deltaTime;
        if (!active && elapsed >= warningTime)
        {
            active = true;
        }
    }
    
    public bool IsExpired()
    {
        return elapsed >= warningTime + duration;
    }
}

#endregion
