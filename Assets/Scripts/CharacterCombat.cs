using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(AutoAttack), typeof(PlayerStats))]
public class CharacterCombat : MonoBehaviour
{
    public CharacterRole role;
    public int rank;
    public AutoAttack weapon;
    public PlayerStats stats;
    public PlayerController controller;
    public EnemySpawner spawner;
    public bool initialized;
    public float specialTimer = 10f;
    public float evolutionNoticeTimer;
    public string evolutionNotice;
    public List<Enemy> targets = new List<Enemy>(128);
    public List<CharacterAttackEffect> medicinePools = new List<CharacterAttackEffect>(2);
    public PlayerSkillCollection skillCollection;
    public float vesselSkillTimer;
    public float catheterTimer;
    public List<GameObject> flushNeedles = new List<GameObject>(4);
    public List<GameObject> catheters = new List<GameObject>(3);
    public GameObject membraneShield;
    public GameObject sidewallAura;
    [System.NonSerialized] public LineRenderer[] sidewallRings;
    public List<CharacterAttackEffect> hemostatPools = new List<CharacterAttackEffect>(3);
    public HashSet<Enemy> hookedEnemies = new HashSet<Enemy>();
    public float ablationBombTimer;
    public float needleBulletTimer;
    public int needleVolleyIndex;
    public Dictionary<Enemy, float> rinseNextHitTime = new Dictionary<Enemy, float>();
    public float rinseHitCleanupTimer;

    public void Start() { Initialize(CharacterCatalog.SelectedRole); }

    public void Initialize(CharacterRole selected)
    {
        if (initialized) return;
        initialized = true;
        role = selected;
        weapon = GetComponent<AutoAttack>();
        stats = GetComponent<PlayerStats>();
        controller = GetComponent<PlayerController>();
        spawner = FindAnyObjectByType<EnemySpawner>();
        skillCollection = FindAnyObjectByType<PlayerSkillCollection>();
        stats.OnLevelUp += HandleLevelUp;
        SpriteRenderer renderer = GetComponent<SpriteRenderer>();
        if (renderer != null) renderer.color = CharacterCatalog.Colors[(int)role];
        MonsterSurvivorArtLibrary.ApplyPlayer(gameObject);
        HandleLevelUp(stats.level);
    }

    // Orbit and knockback live on the fixed step so the shove no longer depends on
    // render rate. The configured value stays a 60 Hz reference strength: each step
    // applies value * 60 * fixedDeltaTime, so accumulated knockback over time is the
    // same at 30, 60 or 120 FPS.
    public void FixedUpdate()
    {
        if (!initialized || Time.timeScale <= 0f || stats.currentHP <= 0 ||
            (GameManager.Instance != null && GameManager.Instance.gameEnded)) return;
        int flush = GetVesselSkillLevel(UpgradeType.FlushNeedle);
        if (flush <= 0) return;
        EnsureFlushNeedles(FlushNeedleCount(flush));
        if (flushNeedles.Count == 0) return;

        // Workbook 1001: the orbit radius is a flat 1.5 at every level, counts are
        // 2..6, and each needle now deals its per-level collision damage.
        float radius = GameBalance.Get("skill.rinse.radius_base", 1.5f) +
            (flush - 1) * GameBalance.Get("skill.rinse.radius_per_level", 0f);
        float hitRadius = GameBalance.Get("skill.rinse.hit_radius", 0.55f);
        float spin = RinseAngularSpeed(flush);
        float knockback = GameBalance.Get("skill.rinse.knockback", 0.18f) * 60f * Time.fixedDeltaTime;
        float hitInterval = GameBalance.Get("skill.rinse.hit_interval", 0.5f);

        RefreshTargets();
        for (int i = 0; i < flushNeedles.Count; i++)
        {
            float angle = Time.time * spin + i * 360f / flushNeedles.Count;
            Vector3 pos = transform.position + Quaternion.Euler(0f, 0f, angle) * Vector3.right * radius;
            flushNeedles[i].transform.position = pos;
            for (int j = 0; j < targets.Count; j++)
            {
                Enemy enemy = targets[j];
                if (enemy == null || enemy.currentHP <= 0) continue;
                if ((enemy.transform.position - pos).sqrMagnitude >= hitRadius * hitRadius) continue;
                enemy.ApplyKnockback((enemy.transform.position - transform.position).normalized, knockback);
                float nextHit;
                if (!rinseNextHitTime.TryGetValue(enemy, out nextHit) || Time.time >= nextHit)
                {
                    enemy.TakeDamage(Mathf.Max(1, RinseDamage(flush)));
                    rinseNextHitTime[enemy] = Time.time + Mathf.Max(0.05f, hitInterval);
                }
            }
        }
        if (Time.time >= rinseHitCleanupTimer)
        {
            rinseHitCleanupTimer = Time.time + 5f;
            List<Enemy> stale = null;
            foreach (KeyValuePair<Enemy, float> pair in rinseNextHitTime)
                if (pair.Key == null || pair.Key.currentHP <= 0)
                    (stale ?? (stale = new List<Enemy>())).Add(pair.Key);
            if (stale != null) for (int i = 0; i < stale.Count; i++) rinseNextHitTime.Remove(stale[i]);
        }
    }

    public void Update()
    {
        if (!initialized || Time.timeScale <= 0f || stats.currentHP <= 0 ||
            (GameManager.Instance != null && GameManager.Instance.gameEnded)) return;
        evolutionNoticeTimer = Mathf.Max(0f, evolutionNoticeTimer - Time.deltaTime);
        VesselSkillUpdate();
        UpdateSidewallAura();
        if (rank < 4) return;
        specialTimer -= Time.deltaTime;
        if (specialTimer > 0f) return;
        specialTimer = role == CharacterRole.Nurse ? GameBalance.Get("character.special_cd_nurse", 8f) :
            GameBalance.Get("character.special_cd_other", 12f);
        PerformSpecial();
    }

    public void HandleLevelUp(int playerLevel)
    {
        int newRank = CharacterCatalog.RankForLevel(playerLevel);
        if (newRank <= rank) return;
        // Apply crossed thresholds once, including XP gains spanning several levels.
        if (role == CharacterRole.Pharmacist && rank < 1 && newRank >= 1)
        {
            if (controller != null) controller.moveSpeed *= GameBalance.Get("pharmacist.r1_move_mult", 1.1f);
            stats.IncreaseMaxHP(GameBalance.Get("pharmacist.r1_hp_bonus", 20f));
        }
        if (rank < 4 && newRank >= 4) specialTimer = GameBalance.Get("character.special_first_delay", 3f);
        rank = newRank;
        evolutionNotice = CharacterCatalog.Name(role) + "进化 " + rank + "：" + CharacterCatalog.Route(role, rank);
        evolutionNoticeTimer = 5f;
    }

    public float IntervalMultiplier()
    {
        if (role == CharacterRole.Doctor) return 1f;
        if (role == CharacterRole.Nurse) return rank >= 5 ? GameBalance.Get("nurse.interval_mult_r5", 0.32f) :
            (rank >= 1 ? GameBalance.Get("nurse.interval_mult_r1", 0.72f) : GameBalance.Get("nurse.interval_mult_r0", 1f));
        return rank >= 5 ? GameBalance.Get("pharmacist.interval_mult_r5", 0.5f) :
            (rank >= 2 ? GameBalance.Get("pharmacist.interval_mult_r2", 0.75f) : GameBalance.Get("pharmacist.interval_mult_r0", 1f));
    }

    public Vector2 AimDirection()
    {
        RefreshTargets();
        Enemy nearest = null;
        float distance = float.MaxValue;
        for (int i = 0; i < targets.Count; i++)
        {
            Enemy enemy = targets[i];
            if (enemy == null || enemy.currentHP <= 0) continue;
            float candidate = ((Vector2)(enemy.transform.position - transform.position)).sqrMagnitude;
            if (candidate < distance) { distance = candidate; nearest = enemy; }
        }
        if (nearest != null) return ((Vector2)(nearest.transform.position - transform.position)).normalized;
        return controller == null ? Vector2.up : controller.Heading;
    }

    public void RefreshTargets()
    {
        if (spawner == null) spawner = FindAnyObjectByType<EnemySpawner>();
        targets.Clear();
        if (spawner != null) targets.AddRange(spawner.trackedEnemies);
        else targets.AddRange(FindObjectsByType<Enemy>());
        for (int i = 0; i < targets.Count; i++)
        {
            Enemy enemy = targets[i];
            if (enemy != null && hookedEnemies.Add(enemy)) enemy.OnDied += HandleEnemyDied;
        }
    }

    public void Attack()
    {
        if (!initialized) Initialize(CharacterCatalog.SelectedRole);
        Vector2 aim = AimDirection();
        if (role == CharacterRole.Doctor)
        {
            Slash(aim);
            if (rank >= 2) StartCoroutine(SecondSlash(aim));
        }
        else if (role == CharacterRole.Nurse)
        {
            if (rank >= 5) DrugStream(aim);
            else FireNeedles(aim);
        }
        else ThrowAmpoule(aim);
        HandleAttackSkills();
    }

    public int GetVesselSkillLevel(UpgradeType type)
    {
        return skillCollection == null ? 0 : skillCollection.GetLevel(type);
    }

    public void ApplyVesselSkill(UpgradeType type, int level)
    {
        level = Mathf.Clamp(level, 1, VesselSkillCatalog.MaxLevelFor(type));
        if (type == UpgradeType.MembraneStent && stats != null) stats.ConfigureMembraneStent(level);
        if (type == UpgradeType.MembraneStent) EnsureMembraneShield(level);
        if (type == UpgradeType.HeartAmulet && stats != null) stats.ApplyHeartAmulet(level);
        if (type == UpgradeType.FlushNeedle) EnsureFlushNeedles(FlushNeedleCount(level));
        if (type == UpgradeType.ThrombusCatheter) EnsureCatheters(CatheterCount(level));
        if (type == UpgradeType.SidewallForceps || type == UpgradeType.BloodRecycleForceps)
        {
            EnsureSidewallAura(level);
            // A one-shot ring on acquisition/level-up so the passive upgrade is
            // immediately visible even before the next lifesteal proc.
            CharacterAttackEffect.SidewallHealPulse(this, transform.position,
                type == UpgradeType.BloodRecycleForceps ? 1.25f : 0.9f,
                type == UpgradeType.BloodRecycleForceps);
        }
        evolutionNotice = VesselSkillCatalog.GlyphFor(type) + "  " + CharacterCatalog.Name(role) + "获得 " + VesselSkillCatalog.EducationFor(type).Split('。')[0];
        evolutionNoticeTimer = 3.5f;
    }

    public static int FlushNeedleCount(int level)
    {
        int fallback = level + GameBalance.GetInt("skill.rinse.count_base", 1);
        return Mathf.Clamp(GameBalance.GetInt("skill.rinse.count_lv" + level, fallback), 1, 8);
    }

    public static int RinseDamage(int level)
    {
        switch (Mathf.Clamp(level, 1, VesselSkillCatalog.StandardMaxLevel))
        {
            case 1: return GameBalance.GetInt("skill.rinse.damage_lv1", 5);
            case 2: return GameBalance.GetInt("skill.rinse.damage_lv2", 10);
            case 3: return GameBalance.GetInt("skill.rinse.damage_lv3", 10);
            case 4: return GameBalance.GetInt("skill.rinse.damage_lv4", 15);
            default: return GameBalance.GetInt("skill.rinse.damage_lv5", 20);
        }
    }

    public static float RinseAngularSpeed(int level)
    {
        switch (Mathf.Clamp(level, 1, VesselSkillCatalog.StandardMaxLevel))
        {
            case 1: return GameBalance.Get("skill.rinse.angular_lv1", 200f);
            case 2: return GameBalance.Get("skill.rinse.angular_lv2", 200f);
            case 3: return GameBalance.Get("skill.rinse.angular_lv3", 200f);
            case 4: return GameBalance.Get("skill.rinse.angular_lv4", 250f);
            default: return GameBalance.Get("skill.rinse.angular_lv5", 300f);
        }
    }

    public static int CatheterCount(int level)
    {
        return Mathf.Clamp(GameBalance.GetInt("skill.suction.count_lv" + level, level), 1, 8);
    }

    public void EnsureMembraneShield(int level)
    {
        CharacterAttackEffect.EnsureEffectMaterial();
        Material mat = CharacterAttackEffect.effectMaterial;
        if (mat == null)
        {
            // The player's own sprite material always exists and is safe for LineRenderer.
            SpriteRenderer selfRenderer = GetComponent<SpriteRenderer>();
            if (selfRenderer != null) mat = selfRenderer.sharedMaterial;
        }
        if (membraneShield == null)
        {
            membraneShield = new GameObject("覆膜支架护盾");
            membraneShield.transform.SetParent(transform, false);

            LineRenderer ring = membraneShield.AddComponent<LineRenderer>();
            if (mat != null) ring.sharedMaterial = mat;
            ring.useWorldSpace = false;
            ring.positionCount = 65;
            ring.loop = true;
            ring.startWidth = ring.endWidth = 0.12f;
            ring.sortingOrder = 6;
            for (int i = 0; i < 65; i++)
            {
                float radians = i * Mathf.PI * 2f / 64f;
                ring.SetPosition(i, new Vector3(Mathf.Cos(radians), Mathf.Sin(radians), 0f) * 1.35f);
            }

            // Unity 6: a second LineRenderer on the same GameObject throws on material assignment — use a child object.
            GameObject innerObj = new GameObject("内环");
            innerObj.transform.SetParent(membraneShield.transform, false);
            LineRenderer innerRing = innerObj.AddComponent<LineRenderer>();
            if (mat != null) innerRing.sharedMaterial = mat;
            innerRing.useWorldSpace = false;
            innerRing.positionCount = 49;
            innerRing.loop = true;
            innerRing.startWidth = innerRing.endWidth = 0.06f;
            innerRing.sortingOrder = 6;
            for (int i = 0; i < 49; i++)
            {
                float radians = i * Mathf.PI * 2f / 48f;
                innerRing.SetPosition(i, new Vector3(Mathf.Cos(radians), Mathf.Sin(radians), 0f) * 1.15f);
            }
        }
        float alpha = 0.42f + 0.1f * Mathf.Clamp01(level - 1);
        LineRenderer[] rings = membraneShield.GetComponentsInChildren<LineRenderer>();
        if (rings.Length > 0 && rings[0] != null)
            rings[0].startColor = rings[0].endColor = new Color(0.35f, 0.85f, 1f, alpha);
        if (rings.Length > 1 && rings[1] != null)
            rings[1].startColor = rings[1].endColor = new Color(0.55f, 0.92f, 1f, alpha * 0.6f);
        membraneShield.transform.localScale = Vector3.one * (1f + 0.09f * level);
    }

    // A persistent red "blood ring" makes the passive forceps state readable at a
    // glance and gets stronger with each level. It is intentionally subtle enough
    // not to hide enemies, but visible enough to confirm the upgrade landed.
    public void EnsureSidewallAura(int level)
    {
        CharacterAttackEffect.EnsureEffectMaterial();
        Material mat = CharacterAttackEffect.effectMaterial;
        if (mat == null)
        {
            SpriteRenderer selfRenderer = GetComponent<SpriteRenderer>();
            if (selfRenderer != null) mat = selfRenderer.sharedMaterial;
        }
        if (sidewallAura == null)
        {
            sidewallAura = new GameObject("侧壁钳血环");
            sidewallAura.transform.SetParent(transform, false);
            CreateSidewallRing("外环", 1.05f, 0.08f, mat);
            CreateSidewallRing("内环", 0.82f, 0.045f, mat);
            sidewallRings = sidewallAura.GetComponentsInChildren<LineRenderer>();
        }
        if (sidewallRings == null || sidewallRings.Length == 0)
            sidewallRings = sidewallAura.GetComponentsInChildren<LineRenderer>();
        UpdateSidewallAura();
    }

    void CreateSidewallRing(string ringName, float radius, float width, Material mat)
    {
        GameObject obj = new GameObject(ringName);
        obj.transform.SetParent(sidewallAura.transform, false);
        LineRenderer ring = obj.AddComponent<LineRenderer>();
        if (mat != null) ring.sharedMaterial = mat;
        ring.useWorldSpace = false;
        ring.positionCount = 65;
        ring.loop = true;
        ring.startWidth = ring.endWidth = width;
        ring.sortingOrder = 7;
        for (int i = 0; i < 65; i++)
        {
            float radians = i * Mathf.PI * 2f / 64f;
            ring.SetPosition(i, new Vector3(Mathf.Cos(radians), Mathf.Sin(radians), 0f) * radius);
        }
    }

    public void UpdateSidewallAura()
    {
        if (sidewallAura == null) return;
        int sidewall = GetVesselSkillLevel(UpgradeType.SidewallForceps);
        int evolution = GetVesselSkillLevel(UpgradeType.BloodRecycleForceps);
        if (sidewall <= 0 && evolution <= 0)
        {
            sidewallAura.SetActive(false);
            return;
        }
        sidewallAura.SetActive(true);
        int level = Mathf.Clamp(sidewall, 1, VesselSkillCatalog.MaxLevelFor(UpgradeType.SidewallForceps));
        bool evolved = evolution > 0;
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 3.5f);
        float alpha = (evolved ? 0.40f : 0.26f) + 0.035f * level + 0.06f * pulse;
        Color tint = evolved
            ? new Color(0.92f, 0.04f, 0.10f, 1f)
            : new Color(0.95f, 0.20f, 0.26f, 1f);
        LineRenderer[] rings = sidewallRings;
        if (rings == null || rings.Length == 0) rings = sidewallAura.GetComponentsInChildren<LineRenderer>();
        for (int i = 0; i < rings.Length; i++)
        {
            Color ringColor = tint;
            ringColor.a = Mathf.Clamp01(alpha * (i == 0 ? 1f : 0.68f));
            rings[i].startColor = rings[i].endColor = ringColor;
        }
        float scale = 1f + 0.04f * level + 0.03f * pulse;
        sidewallAura.transform.localScale = Vector3.one * scale;
        sidewallAura.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(Time.time * 0.8f) * 6f);
    }

    public void HandleAttackSkills()
    {
        int suture = GetVesselSkillLevel(UpgradeType.Suture);
        if (suture > 0) SutureStrike(suture);
        // Sidewall/evolution healing is triggered by each hit in DamageArea/Bullet.
    }

    public void VesselSkillUpdate()
    {
        int scissors = GetVesselSkillLevel(UpgradeType.DissectionScissors);
        if (scissors > 0)
        {
            vesselSkillTimer -= Time.deltaTime;
            if (vesselSkillTimer <= 0f)
            {
                // Workbook 1002: 2.8 / 2.45 / 2.1 / 1.75 / 1.4. The first level is
                // the unmodified base, so the reduction starts at Lv2.
                float fallbackPeriod = Mathf.Max(GameBalance.Get("skill.scissors.period_min", 1.2f),
                    GameBalance.Get("skill.scissors.period_base", 2.8f) -
                    (scissors - 1) * GameBalance.Get("skill.scissors.period_per_level", 0.35f));
                vesselSkillTimer = Mathf.Max(GameBalance.Get("skill.scissors.period_min", 1.2f),
                    GameBalance.Get("skill.scissors.period_lv" + scissors, fallbackPeriod));
                Vector2 aim = AimDirection();
                float radius = scissors >= 2 ? GameBalance.Get("skill.scissors.radius_lv2", 4.1f) : GameBalance.Get("skill.scissors.radius_lv1", 3.3f);
                CharacterAttackEffect.Sector(this, transform.position, aim, radius,
                    scissors >= 2 ? GameBalance.Get("skill.scissors.angle_lv2", 100f) : GameBalance.Get("skill.scissors.angle_lv1", 82f),
                    Mathf.Max(1, weapon.bulletDamage), true);
                if (scissors >= 3)
                    CharacterAttackEffect.Sector(this, transform.position, Quaternion.Euler(0f, 0f, 90f) * aim, radius,
                        GameBalance.Get("skill.scissors.cross_angle", 42f), Mathf.Max(1, weapon.bulletDamage), true);
            }
        }

        int flush = GetVesselSkillLevel(UpgradeType.FlushNeedle);
        if (flush > 0) EnsureFlushNeedles(FlushNeedleCount(flush));

        int catheter = GetVesselSkillLevel(UpgradeType.ThrombusCatheter);
        if (catheter > 0)
        {
            EnsureCatheters(CatheterCount(catheter));
            catheterTimer -= Time.deltaTime;
            for (int i = 0; i < catheters.Count; i++)
            {
                float angle = Time.time * GameBalance.Get("skill.suction.angular_speed", 55f) + i * 360f / catheters.Count;
                catheters[i].transform.position = transform.position + Quaternion.Euler(0f, 0f, angle) * Vector3.up *
                    GameBalance.Get("skill.suction.orbit_radius", 1.55f);
            }
            if (catheterTimer <= 0f)
            {
                catheterTimer = GameBalance.Get("skill.suction.laser_interval", 0.9f);
                RefreshTargets();
                Enemy target = null; float nearest = float.MaxValue;
                for (int i = 0; i < targets.Count; i++) if (targets[i] != null && targets[i].currentHP > 0)
                {
                    float d = (targets[i].transform.position - transform.position).sqrMagnitude;
                    if (d < nearest) { nearest = d; target = targets[i]; }
                }
                if (target != null)
                    for (int i = 0; i < catheters.Count; i++)
                    {
                        Vector2 direction = (target.transform.position - catheters[i].transform.position).normalized;
                        CharacterAttackEffect.Beam(this, catheters[i].transform.position, direction,
                            GameBalance.Get("skill.suction.laser_length", 4.2f), GameBalance.Get("skill.suction.laser_width", 0.15f),
                            Mathf.Max(1, Mathf.FloorToInt(weapon.bulletDamage / GameBalance.Get("skill.suction.damage_div", 2f))),
                            GameBalance.Get("skill.suction.laser_duration", 0.18f));
                    }
            }
        }

        int bomb = GetVesselSkillLevel(UpgradeType.AblationBomb);
        if (bomb > 0)
        {
            ablationBombTimer -= Time.deltaTime;
            if (ablationBombTimer <= 0f)
            {
                ablationBombTimer = GameBalance.Get("skill.ablation.interval", 5f);
                ThrowAblationBombs(bomb);
            }
        }

        UpdateNeedleBullet();
    }

    // 1010 Needle Bullet: auto-targets the nearest living enemy and fires the
    // workbook tiers. Lv4 adds a missile on a cadence; Lv5 adds a drug-stream beam.
    public void UpdateNeedleBullet()
    {
        int level = GetVesselSkillLevel(UpgradeType.NeedleBullet);
        if (level <= 0) return;
        needleBulletTimer -= Time.deltaTime;
        if (needleBulletTimer > 0f) return;

        RefreshTargets();
        Enemy target = null;
        float best = float.MaxValue;
        for (int i = 0; i < targets.Count; i++)
        {
            Enemy enemy = targets[i];
            if (enemy == null || enemy.currentHP <= 0 || enemy.deathPending) continue;
            float distance = (enemy.transform.position - transform.position).sqrMagnitude;
            if (distance < best) { best = distance; target = enemy; }
        }
        if (target == null)
        {
            needleBulletTimer = 0.15f;
            return;
        }

        needleBulletTimer = NeedleInterval(level);
        needleVolleyIndex++;
        Vector2 aim = ((Vector2)(target.transform.position - transform.position)).normalized;
        int fallbackCount = level >= 2 ? 2 : 1;
        int count = Mathf.Max(1, GameBalance.GetInt("skill.needle.count_lv" + level, fallbackCount));
        float spread = level >= 2 ? GameBalance.Get("skill.needle.spread", 5f) : 0f;
        int maxTargets = level >= 3 ? GameBalance.GetInt("skill.needle.pierce", 2) : 1;
        int damage = NeedleDamage(level);
        float speed = NeedleSpeed(level);
        float lifetime = GameBalance.Get("skill.needle.lifetime", 5f);
        for (int i = 0; i < count; i++)
        {
            float angle = count <= 1 ? 0f : (i == 0 ? -spread : spread);
            Vector2 direction = Quaternion.Euler(0f, 0f, angle) * aim;
            weapon.SpawnBullet(direction, maxTargets, new Vector3(0.14f, 0.52f, 1f),
                damage, speed, lifetime, "NeedleBullet");
        }

        // Lv4 missile: reuse the nurse projectile's D x 4 + 2 blast. It fires on a
        // predictable cadence instead of every 0.35 s volley.
        int missileEvery = Mathf.Max(1, GameBalance.GetInt("skill.needle.missile_every", 4));
        if (level >= 4 && needleVolleyIndex % missileEvery == 0)
            CharacterAttackEffect.Throw(this, target.transform.position, ProjectilePayload.Missile);

        // Lv5 drug-stream beam: use the current attack damage, matching the
        // existing nurse beam rather than the needle's 20-damage table row.
        int beamEvery = Mathf.Max(1, GameBalance.GetInt("skill.needle.beam_every", 3));
        if (level >= 5 && needleVolleyIndex % beamEvery == 0)
        {
            float length = Mathf.Clamp(speed, GameBalance.Get("skill.needle.beam_length_min", 7f),
                GameBalance.Get("skill.needle.beam_length_max", 16f));
            CharacterAttackEffect.Beam(this, transform.position, aim, length,
                GameBalance.Get("skill.needle.beam_width", 0.85f),
                Mathf.Max(1, weapon.bulletDamage),
                GameBalance.Get("skill.needle.beam_duration", 0.22f));
        }
    }

    public static int NeedleDamage(int level)
    {
        switch (Mathf.Clamp(level, 1, VesselSkillCatalog.StandardMaxLevel))
        {
            case 1: return GameBalance.GetInt("skill.needle.damage_lv1", 10);
            case 2: return GameBalance.GetInt("skill.needle.damage_lv2", 10);
            case 3: return GameBalance.GetInt("skill.needle.damage_lv3", 15);
            case 4: return GameBalance.GetInt("skill.needle.damage_lv4", 20);
            default: return GameBalance.GetInt("skill.needle.damage_lv5", 20);
        }
    }

    public static float NeedleInterval(int level)
    {
        switch (Mathf.Clamp(level, 1, VesselSkillCatalog.StandardMaxLevel))
        {
            case 1: return GameBalance.Get("skill.needle.interval_lv1", 1f);
            case 2: return GameBalance.Get("skill.needle.interval_lv2", 0.5f);
            case 3: return GameBalance.Get("skill.needle.interval_lv3", 0.4f);
            case 4: return GameBalance.Get("skill.needle.interval_lv4", 0.35f);
            default: return GameBalance.Get("skill.needle.interval_lv5", 0.25f);
        }
    }

    public static float NeedleSpeed(int level)
    {
        switch (Mathf.Clamp(level, 1, VesselSkillCatalog.StandardMaxLevel))
        {
            case 1: return GameBalance.Get("skill.needle.speed_lv1", 5f);
            case 2: return GameBalance.Get("skill.needle.speed_lv2", 6f);
            case 3: return GameBalance.Get("skill.needle.speed_lv3", 7f);
            case 4: return GameBalance.Get("skill.needle.speed_lv4", 8f);
            default: return GameBalance.Get("skill.needle.speed_lv5", 8f);
        }
    }

    public void EnsureFlushNeedles(int count)
    {
        while (flushNeedles.Count < count)
        {
            GameObject obj = new GameObject("冲洗针");
            obj.transform.SetParent(transform, true);
            SpriteRenderer sr = obj.AddComponent<SpriteRenderer>(); sr.sprite = CharacterAttackEffect.GetWhiteSprite(); sr.color = new Color(0.45f, 0.9f, 1f, 0.95f); sr.sortingOrder = 9;
            obj.transform.localScale = new Vector3(0.14f, 0.48f, 1f);
            GameObject tip = new GameObject("针尖");
            tip.transform.SetParent(obj.transform, false);
            SpriteRenderer tipSr = tip.AddComponent<SpriteRenderer>(); tipSr.sprite = CharacterAttackEffect.GetWhiteSprite(); tipSr.color = new Color(0.85f, 1f, 1f, 1f); tipSr.sortingOrder = 10;
            tip.transform.localPosition = new Vector3(0f, 0.24f, 0f);
            tip.transform.localScale = new Vector3(0.18f, 0.12f, 1f);
            flushNeedles.Add(obj);
        }
    }

    public void EnsureCatheters(int count)
    {
        while (catheters.Count < count)
        {
            GameObject obj = new GameObject("血栓抽吸导管");
            obj.transform.SetParent(transform, true);
            SpriteRenderer sr = obj.AddComponent<SpriteRenderer>(); sr.sprite = CharacterAttackEffect.GetWhiteSprite(); sr.color = new Color(0.3f, 1f, 0.72f, 0.95f); sr.sortingOrder = 9;
            obj.transform.localScale = new Vector3(0.22f, 0.55f, 1f);
            GameObject tip = new GameObject("导管头");
            tip.transform.SetParent(obj.transform, false);
            SpriteRenderer tipSr = tip.AddComponent<SpriteRenderer>(); tipSr.sprite = CharacterAttackEffect.GetWhiteSprite(); tipSr.color = new Color(0.7f, 1f, 0.9f, 1f); tipSr.sortingOrder = 10;
            tip.transform.localPosition = new Vector3(0f, 0.28f, 0f);
            tip.transform.localScale = new Vector3(0.28f, 0.15f, 1f);
            catheters.Add(obj);
        }
    }

    public void SutureStrike(int level)
    {
        RefreshTargets();
        List<Enemy> visible = new List<Enemy>();
        Camera camera = Camera.main;
        for (int i = 0; i < targets.Count; i++)
        {
            Enemy enemy = targets[i]; if (enemy == null || enemy.currentHP <= 0) continue;
            Vector3 viewport = camera == null ? Vector3.one * 0.5f : camera.WorldToViewportPoint(enemy.transform.position);
            if (viewport.z > 0f && viewport.x >= 0f && viewport.x <= 1f && viewport.y >= 0f && viewport.y <= 1f) visible.Add(enemy);
        }
        int maxTargets = SutureHitCount(level);
        int count = Mathf.Min(visible.Count, maxTargets);
        int damage = SutureDamage(level);
        for (int i = 0; i < count; i++)
        {
            int swap = Random.Range(i, visible.Count); Enemy temp = visible[i]; visible[i] = visible[swap]; visible[swap] = temp;
            visible[i].TakeDamage(damage);
            if (GetVesselSkillLevel(UpgradeType.SidewallForceps) > 0 ||
                GetVesselSkillLevel(UpgradeType.BloodRecycleForceps) > 0) TrySidewallHeal();
            CharacterAttackEffect.SutureLightning(this, visible[i].transform.position, level >= 3);
        }
    }

    public void TrySidewallHeal()
    {
        // 203 replaces the base forceps effect entirely: a single 50% roll heals
        // 8% of max HP and turns any overheal into a temporary shield.
        int evolutionLevel = GetVesselSkillLevel(UpgradeType.BloodRecycleForceps);
        if (evolutionLevel > 0)
        {
            if (stats != null && Random.value <= GameBalance.Get("skill.bloodforceps.chance", 0.5f))
            {
                float amount = stats.maxHP * GameBalance.Get("skill.bloodforceps.heal_percent", 0.08f);
                stats.HealWithOverflowToShield(amount);
                CharacterAttackEffect.SidewallHealPulse(this, transform.position, 1.05f, true);
                UpdateSidewallAura();
            }
            return;
        }

        int level = GetVesselSkillLevel(UpgradeType.SidewallForceps);
        if (level <= 0) return;
        float chance = ForcepsChance(level);
        if (Random.value <= chance && stats != null)
        {
            // True proportional heal: no rounding up, no minimum 1.
            float amount = stats.maxHP * ForcepsHealPercent(level);
            stats.Heal(amount);
            CharacterAttackEffect.SidewallHealPulse(this, transform.position, 0.7f + 0.05f * level, false);
            UpdateSidewallAura();
        }
    }

    public static float ForcepsChance(int level)
    {
        switch (Mathf.Clamp(level, 1, VesselSkillCatalog.StandardMaxLevel))
        {
            case 1: return GameBalance.Get("skill.forceps.chance_lv1", 0.15f);
            case 2: return GameBalance.Get("skill.forceps.chance_lv2", 0.20f);
            case 3: return GameBalance.Get("skill.forceps.chance_lv3", 0.30f);
            case 4: return GameBalance.Get("skill.forceps.chance_lv4", 0.40f);
            default: return GameBalance.Get("skill.forceps.chance_lv5", 0.50f);
        }
    }

    public static float ForcepsHealPercent(int level)
    {
        switch (Mathf.Clamp(level, 1, VesselSkillCatalog.StandardMaxLevel))
        {
            case 1: return GameBalance.Get("skill.forceps.heal_lv1", 0.05f);
            case 2: return GameBalance.Get("skill.forceps.heal_lv2", 0.10f);
            case 3: return GameBalance.Get("skill.forceps.heal_lv3", 0.15f);
            case 4: return GameBalance.Get("skill.forceps.heal_lv4", 0.20f);
            default: return GameBalance.Get("skill.forceps.heal_lv5", 0.30f);
        }
    }

    public static int SutureHitCount(int level)
    {
        switch (Mathf.Clamp(level, 1, VesselSkillCatalog.StandardMaxLevel))
        {
            case 1: return GameBalance.GetInt("skill.suture.count_lv1", 1);
            case 2: return GameBalance.GetInt("skill.suture.count_lv2", 2);
            case 3: return GameBalance.GetInt("skill.suture.count_lv3", 2);
            case 4: return GameBalance.GetInt("skill.suture.count_lv4", 3);
            default: return GameBalance.GetInt("skill.suture.count_lv5", 3);
        }
    }

    public static int SutureDamage(int level)
    {
        switch (Mathf.Clamp(level, 1, VesselSkillCatalog.StandardMaxLevel))
        {
            case 1: return GameBalance.GetInt("skill.suture.damage_lv1", 20);
            case 2: return GameBalance.GetInt("skill.suture.damage_lv2", 20);
            case 3: return GameBalance.GetInt("skill.suture.damage_lv3", 25);
            case 4: return GameBalance.GetInt("skill.suture.damage_lv4", 25);
            default: return GameBalance.GetInt("skill.suture.damage_lv5", 25);
        }
    }

    public void ApplySpringCoil(Enemy enemy)
    {
        int level = GetVesselSkillLevel(UpgradeType.SpringCoil);
        if (level <= 0 || enemy == null) return;
        enemy.ApplySlow(SpringCoilSlowMultiplier(level),
            GameBalance.Get("skill.coil.duration", 1.2f));
        CharacterAttackEffect.HitBlock(enemy.transform.position, level >= 3 ? 0.3f : 0.2f);
    }

    public static float SpringCoilSlowMultiplier(int level)
    {
        switch (Mathf.Clamp(level, 1, VesselSkillCatalog.StandardMaxLevel))
        {
            case 1: return GameBalance.Get("skill.coil.slow_lv1", 0.90f);
            case 2: return GameBalance.Get("skill.coil.slow_lv2", 0.80f);
            case 3: return GameBalance.Get("skill.coil.slow_lv3", 0.70f);
            case 4: return GameBalance.Get("skill.coil.slow_lv4", 0.60f);
            default: return GameBalance.Get("skill.coil.slow_lv5", 0.50f);
        }
    }

    public void HandleEnemyDied(Enemy enemy)
    {
        if (enemy == null) return;
        int level = GetVesselSkillLevel(UpgradeType.MicroscopicHemostat);
        if (level <= 0) return;
        hemostatPools.RemoveAll(pool => pool == null || pool.retired);
        while (hemostatPools.Count >= level) { hemostatPools[0].Retire(); hemostatPools.RemoveAt(0); }
        hemostatPools.Add(CharacterAttackEffect.Pool(this, enemy.transform.position,
            GameBalance.Get("skill.clamp.radius_lv" + level,
                GameBalance.Get("skill.clamp.radius_base", 1.5f) +
                (level - 1) * GameBalance.Get("skill.clamp.radius_per_level", 0.25f)),
            GameBalance.Get("skill.clamp.duration", 1f),
            Mathf.Max(1, Mathf.FloorToInt(weapon.bulletDamage / GameBalance.Get("skill.clamp.damage_div", 2f))),
            GameBalance.Get("skill.clamp.tick", 0.25f)));
    }

    public void ThrowAblationBombs(int level)
    {
        RefreshTargets();
        // Workbook 1008: 2 / 3 / 3 / 3 / 3 bombs, damage 15 / 15 / 15 / 20 / 20.
        int bombCount = level <= 1 ? GameBalance.GetInt("skill.ablation.count_lv1", 2)
            : (level == 2 ? GameBalance.GetInt("skill.ablation.count_lv2", 3)
            : (level == 3 ? GameBalance.GetInt("skill.ablation.count_lv3", 3)
            : (level == 4 ? GameBalance.GetInt("skill.ablation.count_lv4", 3)
            : GameBalance.GetInt("skill.ablation.count_lv5", 3))));
        float delay = level >= 3 ? GameBalance.Get("skill.ablation.delay_lv3", 0.5f) : GameBalance.Get("skill.ablation.delay_lv12", 1f);
        int damage = AblationDamage(level);
        List<Enemy> candidates = new List<Enemy>();
        Camera camera = Camera.main;
        for (int i = 0; i < targets.Count; i++)
        {
            Enemy enemy = targets[i];
            if (enemy == null || enemy.currentHP <= 0) continue;
            Vector3 viewport = camera == null ? Vector3.one * 0.5f : camera.WorldToViewportPoint(enemy.transform.position);
            if (viewport.z > 0f && viewport.x >= 0f && viewport.x <= 1f && viewport.y >= 0f && viewport.y <= 1f) candidates.Add(enemy);
        }
        // No legal target: skip this throw entirely rather than spending the volley.
        if (candidates.Count == 0) return;
        // Always throw the full tier count. With too few targets the extras re-aim at
        // random existing candidates; each bomb resolves its own blast independently.
        for (int i = 0; i < bombCount; i++)
        {
            Enemy target = candidates[Random.Range(0, candidates.Count)];
            if (target == null) continue;
            StartCoroutine(ThrowDelayedBomb(target.transform.position, delay, damage));
        }
    }

    public System.Collections.IEnumerator ThrowDelayedBomb(Vector3 landing, float delay, int damage)
    {
        CharacterAttackEffect.Throw(this, landing, ProjectilePayload.AblationBomb);
        // Flight and fuse are counted separately and belong to the bomb: flight is
        // skill.ablation.extra_delay (0.35s), then the tier fuse below.
        yield return new WaitForSeconds(GameBalance.Get("skill.ablation.extra_delay", 0.35f) + delay);
        if (GameManager.Instance != null && GameManager.Instance.gameEnded) yield break;
        CharacterAttackEffect.Burst(this, landing, GameBalance.Get("skill.ablation.blast_radius", 2.2f),
            Mathf.Max(1, damage), true);
    }

    public static int AblationDamage(int level)
    {
        switch (Mathf.Clamp(level, 1, VesselSkillCatalog.StandardMaxLevel))
        {
            case 1: return GameBalance.GetInt("skill.ablation.damage_lv1", 15);
            case 2: return GameBalance.GetInt("skill.ablation.damage_lv2", 15);
            case 3: return GameBalance.GetInt("skill.ablation.damage_lv3", 15);
            case 4: return GameBalance.GetInt("skill.ablation.damage_lv4", 20);
            default: return GameBalance.GetInt("skill.ablation.damage_lv5", 20);
        }
    }

    public IEnumerator SecondSlash(Vector2 aim)
    {
        yield return new WaitForSeconds(GameBalance.Get("doctor.double_slash_delay", 0.16f));
        if (stats != null && stats.currentHP > 0) Slash(aim);
    }

    public void Slash(Vector2 aim)
    {
        float radius = rank >= 3 ? GameBalance.Get("doctor.radius_r3", 4.1f) :
            (rank >= 1 ? GameBalance.Get("doctor.radius_r1", 3.5f) : GameBalance.Get("doctor.radius_r0", 3f));
        float angle = rank >= 5 ? GameBalance.Get("doctor.angle_r5", 170f) :
            (rank >= 3 ? GameBalance.Get("doctor.angle_r3", 115f) :
            (rank >= 1 ? GameBalance.Get("doctor.angle_r1", 95f) : GameBalance.Get("doctor.angle_r0", 80f)));
        CharacterAttackEffect.Sector(this, transform.position, aim, radius, angle,
            Mathf.Max(1, weapon.bulletDamage + GameBalance.GetInt("doctor.bonus_damage", 10)), rank >= 5);
    }

    public void FireNeedles(Vector2 aim)
    {
        int count = rank >= 2 ? 2 : 1;
        for (int i = 0; i < count; i++)
        {
            float spreadAngle = GameBalance.Get("nurse.spread_angle", 5f);
            float spread = count == 1 ? 0f : (i == 0 ? -spreadAngle : spreadAngle);
            Vector2 direction = Quaternion.Euler(0f, 0f, spread) * aim;
            weapon.SpawnBullet(direction, rank >= 3 ? 2 : 1, new Vector3(0.16f, 0.65f, 1f));
        }
    }

    public void DrugStream(Vector2 aim)
    {
        float length = Mathf.Clamp(weapon.bulletSpeed, GameBalance.Get("nurse.beam_length_min", 7f),
            GameBalance.Get("nurse.beam_length_max", 16f));
        CharacterAttackEffect.Beam(this, transform.position, aim, length, GameBalance.Get("nurse.beam_width", 0.85f),
            Mathf.Max(1, weapon.bulletDamage), GameBalance.Get("nurse.beam_duration", 0.22f));
    }

    public void ThrowAmpoule(Vector2 aim)
    {
        Vector3 landing = transform.position + (Vector3)(aim * GameBalance.Get("pharmacist.fallback_distance", 4f));
        float targetRange = GameBalance.Get("pharmacist.target_range", 8f);
        float best = targetRange * targetRange;
        for (int i = 0; i < targets.Count; i++)
        {
            Enemy enemy = targets[i];
            if (enemy == null || enemy.currentHP <= 0) continue;
            float distance = (enemy.transform.position - transform.position).sqrMagnitude;
            if (distance < best) { best = distance; landing = enemy.transform.position; }
        }
        CharacterAttackEffect.Throw(this, landing, ProjectilePayload.Ampoule);
    }

    public void AmpouleImpact(Vector3 position)
    {
        CharacterAttackEffect.Burst(this, position, GameBalance.Get("pharmacist.blast_radius", 2.5f),
            Mathf.Max(1, weapon.bulletDamage + GameBalance.GetInt("pharmacist.blast_damage_add", 10)), rank >= 5);
        if (rank < 3) return;
        medicinePools.RemoveAll(pool => pool == null || pool.retired);
        while (medicinePools.Count >= Mathf.Max(1, GameBalance.GetInt("pharmacist.pool_max", 2)))
        {
            medicinePools[0].Retire();
            medicinePools.RemoveAt(0);
        }
        medicinePools.Add(CharacterAttackEffect.Pool(this, position, GameBalance.Get("pharmacist.pool_radius", 2.2f),
            GameBalance.Get("pharmacist.pool_duration", 4f), Mathf.Max(1, weapon.bulletDamage),
            GameBalance.Get("pharmacist.pool_tick", 0.6f)));
    }

    public void PerformSpecial()
    {
        if (role == CharacterRole.Nurse)
        {
            Vector2 aim = AimDirection();
            Vector3 destination = transform.position + (Vector3)(aim * GameBalance.Get("nurse.missile_fallback_distance", 6f));
            if (targets.Count > 0)
            {
                float best = float.MaxValue;
                for (int i = 0; i < targets.Count; i++)
                {
                    Enemy enemy = targets[i];
                    if (enemy == null || enemy.currentHP <= 0) continue;
                    float distance = (enemy.transform.position - transform.position).sqrMagnitude;
                    if (distance < best) { best = distance; destination = enemy.transform.position; }
                }
            }
            CharacterAttackEffect.Throw(this, destination, ProjectilePayload.Missile);
            return;
        }
        Camera camera = Camera.main;
        RefreshTargets();
        for (int i = 0; i < targets.Count; i++)
        {
            Enemy enemy = targets[i];
            if (enemy == null || enemy.currentHP <= 0) continue;
            Vector3 viewport = camera == null ? Vector3.one * 0.5f : camera.WorldToViewportPoint(enemy.transform.position);
            if (viewport.z < 0f || viewport.x < 0f || viewport.x > 1f || viewport.y < 0f || viewport.y > 1f) continue;
            if (role == CharacterRole.Doctor) enemy.ApplySlow(GameBalance.Get("doctor.special_slow_mult", 0.4f),
                GameBalance.Get("doctor.special_slow_duration", 2.5f));
            else enemy.TakeDamage(Mathf.Max(1, weapon.bulletDamage));
        }
        CharacterAttackEffect.Flash(this, transform.position,
            camera == null ? 14f : camera.orthographicSize * camera.aspect, role == CharacterRole.Doctor);
    }

    public void OnDestroy()
    {
        if (stats != null) stats.OnLevelUp -= HandleLevelUp;
        foreach (Enemy enemy in hookedEnemies) if (enemy != null) enemy.OnDied -= HandleEnemyDied;
        for (int i = 0; i < flushNeedles.Count; i++) if (flushNeedles[i] != null) Destroy(flushNeedles[i]);
        for (int i = 0; i < catheters.Count; i++) if (catheters[i] != null) Destroy(catheters[i]);
        if (membraneShield != null) Destroy(membraneShield);
        if (sidewallAura != null) Destroy(sidewallAura);
    }
}

