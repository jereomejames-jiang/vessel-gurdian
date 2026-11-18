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
    public List<CharacterAttackEffect> hemostatPools = new List<CharacterAttackEffect>(3);
    public HashSet<Enemy> hookedEnemies = new HashSet<Enemy>();
    public float ablationBombTimer;
    public float hemostatNetTimer;
    public float spasmRingTimer;
    public float ablationRainTimer;
    // v3 新增计时器：冲洗针 10s 周期/6s 在场、缝线独立 5s、快乐水 5s、吻合钉枪快节奏。
    public float flushCycleTimer;
    public float flushActiveTimer;
    public float sutureTimer;
    public float happyWaterTimer;
    readonly Dictionary<Enemy, float> flushHitAt = new Dictionary<Enemy, float>();

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
        MonsterSurvivorArtLibrary.ApplyCharacter(gameObject, role);
        if (GetComponent<PlayerHitFeedback>() == null) gameObject.AddComponent<PlayerHitFeedback>();
        HandleLevelUp(stats.level);
    }

    public void Update()
    {
        if (!initialized || Time.timeScale <= 0f || stats.currentHP <= 0 ||
            (GameManager.Instance != null && GameManager.Instance.gameEnded)) return;
        evolutionNoticeTimer = Mathf.Max(0f, evolutionNoticeTimer - Time.deltaTime);
        UpdateWalkFacing();
        if (membraneShield != null)
        {
            SpriteRenderer sr = membraneShield.GetComponent<SpriteRenderer>();
            if (sr != null) sr.sprite = MonsterSurvivorArtLibrary.LoadVfx(
                Mathf.FloorToInt(Time.time / 0.5f) % 2 == 0 ? "vfx_stent_ring_f0" : "vfx_stent_ring_f1", 32f);
        }
        VesselSkillUpdate();
        // 旧版 rank4+ 特殊技：v3 基础攻击封顶 lv3（character.rank_max=3），此分支不再可达，代码保留。
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
        // 药剂师 lv1（附录C3：列位=等级序）：小幅强化自身属性，增加移速血量。
        if (role == CharacterRole.Pharmacist && rank < 1 && newRank >= 1)
        {
            if (controller != null) controller.moveSpeed *= GameBalance.Get("pharmacist.r1_move_mult", 1.1f);
            stats.IncreaseMaxHP(GameBalance.GetInt("pharmacist.r1_hp_bonus", 1));
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

    // ===== v3 §7 被动倍率（单一百分比按级加法/乘法叠加，叠加口径未指定→标旗） =====
    public float DamageMult() { return 1f + GameBalance.Get("passive.training.damage", 0.1f) * GetVesselSkillLevel(UpgradeType.AdvancedTraining); }
    public int D(int raw) { return Mathf.Max(1, Mathf.RoundToInt(raw * DamageMult())); }
    public float CooldownMult() { return Mathf.Pow(GameBalance.Get("passive.surgery.cooldown", 0.8f), GetVesselSkillLevel(UpgradeType.MajorSurgery)); }
    public float DurationMult() { return Mathf.Pow(GameBalance.Get("passive.equipment.duration", 1.25f), GetVesselSkillLevel(UpgradeType.EquipmentUpdate)); }
    public float SizeMult() { return 1f + GameBalance.Get("passive.clinic.size", 0.15f) * GetVesselSkillLevel(UpgradeType.FreeClinic); }
    public float BulletSpeedMult() { return 1f + GameBalance.Get("passive.research.speed", 0.5f) * GetVesselSkillLevel(UpgradeType.DeepResearch); }
    public float BasicAttackSpeedMult() { return 1f + GameBalance.Get("passive.rapid.attack_speed", 0.2f) * GetVesselSkillLevel(UpgradeType.RapidDisposal); }

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
        PlayAttackVisual(aim);
        if (role == CharacterRole.Doctor)
        {
            // R1-08 替换语义（§8 表头）：高频电刀进化后基础攻击改为电弧刀波(伤害5)，不再挥砍。
            if (GetVesselSkillLevel(UpgradeType.HighFrequencyElectroknife) > 0) ElectroknifeArc(aim);
            else
            {
                Slash(aim);
                // 医生 lv2：一次连续挥砍 2 次。
                if (rank >= 2) StartCoroutine(SecondSlash(aim));
            }
        }
        else if (role == CharacterRole.Nurse)
        {
            FireNeedles(aim);
        }
        else ThrowAmpoule(aim);
        HandleAttackSkills();
    }

    MonsterSurvivorSpriteAnimator spriteAnimator;

    MonsterSurvivorSpriteAnimator Animator()
    {
        if (spriteAnimator == null) spriteAnimator = GetComponentInChildren<MonsterSurvivorSpriteAnimator>();
        return spriteAnimator;
    }

    // 侧视素材跟着 aim 翻面，攻击条带放一次就自己回到走路循环。
    void PlayAttackVisual(Vector2 aim)
    {
        MonsterSurvivorSpriteAnimator animator = Animator();
        if (animator == null) return;
        ApplyFacing(animator, aim.x);
        animator.Play("attack");
    }

    // 走路时朝行进方向翻面，否则角色会一直侧对前进方向。
    void UpdateWalkFacing()
    {
        MonsterSurvivorSpriteAnimator animator = Animator();
        if (animator == null || animator.InOneShot || controller == null) return;
        ApplyFacing(animator, controller.Heading.x);
    }

    // 正上/正下方向的 x 只是浮点噪声，保持上一面的朝向，不然角色会原地抖着翻面。
    static void ApplyFacing(MonsterSurvivorSpriteAnimator animator, float horizontal)
    {
        if (Mathf.Abs(horizontal) < 0.05f) return;
        animator.facing = horizontal > 0f ? 1 : -1;
    }

    public int GetVesselSkillLevel(UpgradeType type)
    {
        return skillCollection == null ? 0 : skillCollection.GetLevel(type);
    }

    public void ApplyVesselSkill(UpgradeType type, int level)
    {
        level = Mathf.Clamp(level, 1, VesselSkillCatalog.MaxLevel(type));
        if (type == UpgradeType.MembraneStent && stats != null) stats.ConfigureMembraneStent(level);
        if (type == UpgradeType.MembraneStent) EnsureMembraneShield(level);
        if (type == UpgradeType.HeartAmulet && stats != null) stats.ConfigureHeartAmulet(level);
        if (type == UpgradeType.PartnerSupport && stats != null)
            stats.xpMultiplier = 1f + GameBalance.Get("passive.support.xp", 0.2f) * level;
        if (type == UpgradeType.FlushNeedle) EnsureFlushNeedles(level + GameBalance.GetInt("skill.rinse.count_base", 1));
        if (type == UpgradeType.ThrombusCatheter) EnsureCatheters(level);
        if (type == UpgradeType.VascularSpasmRing)
            CharacterAttackEffect.SpasmRing(this, GameBalance.Get("evolution.coil_spasm.radius", 3f), 5f);
        evolutionNotice = VesselSkillCatalog.GlyphFor(type) + "  " + CharacterCatalog.Name(role) + "获得 " + VesselSkillCatalog.EducationFor(type).Split('。')[0];
        evolutionNoticeTimer = 3.5f;
    }

    public void EnsureMembraneShield(int level)
    {
        if (membraneShield == null)
        {
            membraneShield = new GameObject("覆膜支架护盾");
            membraneShield.transform.SetParent(transform, false);
            SpriteRenderer sr = membraneShield.AddComponent<SpriteRenderer>();
            sr.sprite = MonsterSurvivorArtLibrary.LoadVfx("vfx_stent_ring_f0", 32f);
            sr.sortingOrder = 6;
        }
        SpriteRenderer ring = membraneShield.GetComponent<SpriteRenderer>();
        ring.color = new Color(1f, 1f, 1f, 0.42f);
        // 96px@ppu32=3 世界；直径固定 2.7 世界（医生身高1.35倍）。根节点旧缩放只服务碰撞体/轨道，护盾按世界尺寸除回。
        float root = Mathf.Max(0.01f, transform.lossyScale.x);
        membraneShield.transform.localScale = Vector3.one * (0.9f / root);
    }

    public void HandleAttackSkills()
    {
        // 缝线改为独立 5s 计时（v3 §6），不再挂在每次普攻上；侧壁钳吸血由每次命中触发。
    }

    public void VesselSkillUpdate()
    {
        float cd = CooldownMult();
        float dur = DurationMult();
        float size = SizeMult();

        // 被动·快乐水：每 5s 恢复最大生命的 10%/级。
        int happy = GetVesselSkillLevel(UpgradeType.HappyWater);
        if (happy > 0 && stats != null)
        {
            happyWaterTimer -= Time.deltaTime;
            if (happyWaterTimer <= 0f)
            {
                happyWaterTimer = GameBalance.Get("passive.happywater.interval", 5f);
                stats.Heal(Mathf.Max(1, Mathf.CeilToInt(stats.maxHP * GameBalance.Get("passive.happywater.percent", 0.1f) * happy)));
            }
        }

        // 主动·冲洗针：0.25r/s（90°/s）环绕，每 10s 出现 6s；2/3/4 枚；轻微击退+少量伤害。
        // 进化·持续冲洗系统：针体持续环绕，不再周期消失（替换原主动效果）。
        int flush = GetVesselSkillLevel(UpgradeType.FlushNeedle);
        bool continuous = GetVesselSkillLevel(UpgradeType.ContinuousFlush) > 0;
        if (flush > 0 || continuous)
        {
            EnsureFlushNeedles(Mathf.Max(flush, 1) + GameBalance.GetInt("skill.rinse.count_base", 1));
            bool active = true;
            if (!continuous)
            {
                if (flushActiveTimer > 0f)
                {
                    flushActiveTimer -= Time.deltaTime;
                    // R1-01：周期=active+wait 共 10s（6s 在场 + 4s 等待），wait 不再重置为完整周期。
                    if (flushActiveTimer <= 0f)
                        flushCycleTimer = (GameBalance.Get("skill.rinse.cycle", 10f) - GameBalance.Get("skill.rinse.active", 6f)) * cd;
                }
                else
                {
                    flushCycleTimer -= Time.deltaTime;
                    if (flushCycleTimer <= 0f) flushActiveTimer = GameBalance.Get("skill.rinse.active", 6f) * dur;
                }
                active = flushActiveTimer > 0f;
            }
            float radius = GameBalance.Get("skill.rinse.radius_base", 1.25f) + Mathf.Max(flush, 1) * GameBalance.Get("skill.rinse.radius_per_level", 0.18f);
            float hitRadius = GameBalance.Get("skill.rinse.hit_radius", 0.55f);
            float angular = GameBalance.Get("skill.rinse.angular_base", 90f) + Mathf.Max(flush, 1) * GameBalance.Get("skill.rinse.angular_per_level", 0f);
            int rinseDamage = D(Mathf.Max(1, Mathf.FloorToInt(weapon.bulletDamage / GameBalance.Get("skill.rinse.damage_div", 2f))));
            float hitCooldown = GameBalance.Get("skill.rinse.hit_cooldown", 0.5f);
            if (active) RefreshTargets();
            for (int i = 0; i < flushNeedles.Count; i++)
            {
                flushNeedles[i].SetActive(active);
                if (!active) continue;
                float angle = Time.time * angular + i * 360f / flushNeedles.Count;
                Vector3 pos = transform.position + Quaternion.Euler(0f, 0f, angle) * Vector3.right * radius;
                flushNeedles[i].transform.position = pos;
                for (int j = 0; j < targets.Count; j++)
                {
                    Enemy enemy = targets[j];
                    if (enemy == null || enemy.currentHP <= 0) continue;
                    if ((enemy.transform.position - pos).sqrMagnitude >= hitRadius * hitRadius) continue;
                    enemy.ApplyKnockback((enemy.transform.position - transform.position).normalized,
                        GameBalance.Get("skill.rinse.knockback", 0.18f));
                    float nextHit;
                    if (!flushHitAt.TryGetValue(enemy, out nextHit) || Time.time >= nextHit)
                    {
                        flushHitAt[enemy] = Time.time + hitCooldown;
                        enemy.TakeDamage(rinseDamage);
                    }
                }
            }
        }

        // 主动·解剖剪：每 5s 发射剪刃，路径碰撞伤害；lv2 体积+20%；lv3 数量+1。
        // 进化·超声解剖剪：剪刃飞射旋转滞留，十字二剪 42°（替换原主动效果）。
        int scissors = GetVesselSkillLevel(UpgradeType.DissectionScissors);
        bool ultrasonic = GetVesselSkillLevel(UpgradeType.UltrasonicScissors) > 0;
        if (scissors > 0 || ultrasonic)
        {
            vesselSkillTimer -= Time.deltaTime;
            if (vesselSkillTimer <= 0f)
            {
                vesselSkillTimer = GameBalance.Get("skill.scissors.period_base", 5f) * cd;
                Vector2 aim = AimDirection();
                float sizeMult = (scissors >= 2 ? GameBalance.Get("skill.scissors.size_lv2", 1.2f) : 1f) * size;
                int shots = ultrasonic ? 2 : (scissors >= 3 ? GameBalance.GetInt("skill.scissors.count_lv3", 2) : 1);
                float cross = ultrasonic ? GameBalance.Get("evolution.scissors_ultrasonic.angle", 42f) : GameBalance.Get("skill.scissors.cross_angle", 30f);
                int damage = D(Mathf.Max(1, weapon.bulletDamage));
                for (int i = 0; i < shots; i++)
                {
                    Vector2 dir = i == 0 ? aim : (Vector2)(Quaternion.Euler(0f, 0f, ultrasonic ? 90f : 12f) * aim);
                    CharacterAttackEffect.ScissorsShot(this, transform.position, dir,
                        GameBalance.Get("skill.scissors.shot_speed", 9f), GameBalance.Get("skill.scissors.shot_range", 6f),
                        GameBalance.Get("skill.scissors.shot_width", 0.5f), damage, sizeMult, ultrasonic, cross);
                }
            }
        }

        // 主动·缝线：每 5s 随机攻击视野内 2/3/4 个敌人。
        // 进化·吻合钉枪：高速穿透缝合钉，攻击频率进一步加快（替换原主动效果）。
        int suture = GetVesselSkillLevel(UpgradeType.Suture);
        bool staple = GetVesselSkillLevel(UpgradeType.StapleGun) > 0;
        if (suture > 0 || staple)
        {
            sutureTimer -= Time.deltaTime;
            if (sutureTimer <= 0f)
            {
                if (staple)
                {
                    sutureTimer = GameBalance.Get("evolution.staple.interval", 1.2f) * cd;
                    StapleVolley();
                }
                else
                {
                    sutureTimer = GameBalance.Get("skill.suture.interval", 5f) * cd;
                    SutureStrike(Mathf.Max(suture, 1));
                }
            }
        }

        // 主动·血栓抽吸导管：跟随物每 3s 单体激光；1/2/3 个跟随物。
        // 进化·血管痉挛环：一定距离内范围减速 50%+持续伤害（替换原主动效果）。
        int catheter = GetVesselSkillLevel(UpgradeType.ThrombusCatheter);
        bool spasm = GetVesselSkillLevel(UpgradeType.VascularSpasmRing) > 0;
        if (catheter > 0 || spasm)
        {
            EnsureCatheters(Mathf.Max(catheter, 1));
            for (int i = 0; i < catheters.Count; i++)
            {
                float angle = Time.time * GameBalance.Get("skill.suction.angular_speed", 55f) + i * 360f / catheters.Count;
                catheters[i].transform.position = transform.position + Quaternion.Euler(0f, 0f, angle) * Vector3.up *
                    GameBalance.Get("skill.suction.orbit_radius", 1.55f);
            }
            if (!spasm)
            {
                catheterTimer -= Time.deltaTime;
                if (catheterTimer <= 0f)
                {
                    catheterTimer = GameBalance.Get("skill.suction.laser_interval", 3f) * cd;
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
                            catheters[i].transform.rotation = Quaternion.Euler(0f, 0f,
                                Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f);
                            CharacterAttackEffect.CatheterLaser(this, catheters[i].transform.position + (Vector3)(direction * 0.81f), direction,
                                GameBalance.Get("skill.suction.laser_length", 4.2f), GameBalance.Get("skill.suction.laser_width", 0.15f),
                                D(Mathf.Max(1, Mathf.FloorToInt(weapon.bulletDamage / GameBalance.Get("skill.suction.damage_div", 2f)))),
                                GameBalance.Get("skill.suction.laser_duration", 0.18f));
                        }
                }
            }
        }
        if (spasm)
        {
            spasmRingTimer -= Time.deltaTime;
            if (spasmRingTimer <= 0f)
            {
                spasmRingTimer = GameBalance.Get("evolution.coil_spasm.interval", 2f) * cd;
                float slow = GameBalance.Get("evolution.coil_spasm.slow", 0.5f);
                float dotMult = GameBalance.Get("evolution.coil_spasm.dot_mult", 2f);
                float radius = GameBalance.Get("evolution.coil_spasm.radius", 3f);
                RefreshTargets();
                for (int i = 0; i < targets.Count; i++)
                {
                    Enemy enemy = targets[i];
                    if (enemy != null && (enemy.transform.position - transform.position).sqrMagnitude < radius * radius)
                    {
                        enemy.ApplySlow(slow, 2f);
                        enemy.TakeDamage(D(Mathf.Max(1, Mathf.CeilToInt(weapon.bulletDamage * dotMult))));
                    }
                }
            }
        }

        // 主动·消融弹：每 2s 投 2 枚（lv2 3 枚），延迟 1s（lv3 0.5s）。
        // 进化·定向消融弹雨：从上方投下多枚消融弹（替换原主动效果）。
        int bomb = GetVesselSkillLevel(UpgradeType.AblationBomb);
        bool rain = GetVesselSkillLevel(UpgradeType.DirectedAblationRain) > 0;
        if (bomb > 0 && !rain)
        {
            ablationBombTimer -= Time.deltaTime;
            if (ablationBombTimer <= 0f)
            {
                ablationBombTimer = GameBalance.Get("skill.ablation.interval", 2f) * cd;
                ThrowAblationBombs(bomb);
            }
        }
        if (rain)
        {
            ablationRainTimer -= Time.deltaTime;
            if (ablationRainTimer <= 0f)
            {
                ablationRainTimer = GameBalance.Get("evolution.ablation_rain.interval", 3f) * cd;
                int count = GameBalance.GetInt("evolution.ablation_rain.count", 5);
                float radius = GameBalance.Get("evolution.ablation_rain.radius", 1.8f) * size;
                float delay = GameBalance.Get("evolution.ablation_rain.delay", 0.3f);
                RefreshTargets();
                for (int i = 0; i < count && i < targets.Count; i++)
                {
                    Enemy target = targets[i];
                    if (target != null)
                    {
                        StartCoroutine(AblationRainStrike(target.transform.position, radius, delay * i));
                    }
                }
            }
        }

        // 进化·栓塞止血网：周期性推远周围敌人（替换显微止血夹的击杀留区）。
        int hemostatNet = GetVesselSkillLevel(UpgradeType.EmbolismHemostatNet);
        if (hemostatNet > 0)
        {
            hemostatNetTimer -= Time.deltaTime;
            if (hemostatNetTimer <= 0f)
            {
                hemostatNetTimer = GameBalance.Get("evolution.clamp_magnet.interval", 3f) * cd;
                float radius = GameBalance.Get("evolution.clamp_magnet.radius", 2.5f);
                float pushForce = GameBalance.Get("evolution.clamp_magnet.push_force", 3f);
                RefreshTargets();
                for (int i = 0; i < targets.Count; i++)
                {
                    Enemy enemy = targets[i];
                    if (enemy != null && (enemy.transform.position - transform.position).sqrMagnitude < radius * radius)
                    {
                        Vector2 direction = (enemy.transform.position - transform.position).normalized;
                        enemy.ApplyKnockback(direction, pushForce);
                    }
                }
                CharacterAttackEffect.PushWave(this, radius);
            }
        }

        // 进化·高频电刀 / 微量输液泵（R1-08 替换语义，§8 表头「去除原主动技能效果，更换为下述技能」）：
        // 基础攻击系进化不再与普攻共存——医生普攻改为电弧刀波（Attack→ElectroknifeArc），
        // 护士普攻改为极短冷却持续发射（AutoAttack 间隔覆写为 evolution.needle_burst.interval）。
        // 药剂师雾化给药机仅改药剂滩上限（AmpouleImpact），基础攻击链不动。

        // 进化·雾化给药机：药剂池最多 3 滩（池上限在 AmpouleImpact 中接入）。
    }

    // 高频电刀进化后的医生基础攻击：电弧刀波(伤害5)，随普攻频率发射（替换语义 F58）。
    public void ElectroknifeArc(Vector2 aim)
    {
        int damage = D(GameBalance.GetInt("evolution.slash_arc.damage", 5));
        float range = GameBalance.Get("evolution.slash_arc.range", 3.5f);
        CharacterAttackEffect.Sector(this, transform.position, aim, range, 120f, damage, true);
    }

    // 微量输液泵进化后的护士基础攻击间隔：极短冷却持续发射（替换语义 F58）。
    public float EffectiveAttackInterval(float baseInterval)
    {
        if (role == CharacterRole.Nurse && GetVesselSkillLevel(UpgradeType.MicroInfusionPump) > 0)
            return GameBalance.Get("evolution.needle_burst.interval", 0.15f);
        return baseInterval;
    }

    System.Collections.IEnumerator AblationRainStrike(Vector3 position, float radius, float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        CharacterAttackEffect.RainBomb(this, position, GameBalance.Get("evolution.ablation_rain.fall", 0.25f),
            0.25f, radius, D(Mathf.Max(1, weapon.bulletDamage)));
    }

    public void EnsureFlushNeedles(int count)
    {
        while (flushNeedles.Count < count)
        {
            GameObject obj = new GameObject("冲洗针");
            obj.transform.SetParent(transform, true);
            SpriteRenderer sr = obj.AddComponent<SpriteRenderer>();
            sr.sprite = MonsterSurvivorArtLibrary.LoadVfx("vfx_flush_needle");
            if (sr.sprite == null)
            {
                sr.sprite = CharacterAttackEffect.GetWhiteSprite();
                sr.color = new Color(0.45f, 0.9f, 1f, 0.95f);
                obj.transform.localScale = new Vector3(0.14f, 0.48f, 1f);
            }
            else
            {
                // 角色根节点被美术库按碰撞高度放大，针按目标世界高度反推 local 缩放（约医生 82% 身高）。
                float parent = Mathf.Max(0.01f, transform.lossyScale.x);
                obj.transform.localScale = Vector3.one * (1.64f / (sr.sprite.bounds.size.y * parent));
            }
            sr.sortingOrder = 9;
            flushNeedles.Add(obj);
        }
    }

    public void EnsureCatheters(int count)
    {
        while (catheters.Count < count)
        {
            GameObject obj = new GameObject("血栓抽吸导管");
            obj.transform.SetParent(transform, true);
            SpriteRenderer sr = obj.AddComponent<SpriteRenderer>();
            Sprite pod = MonsterSurvivorArtLibrary.LoadVfx("vfx_catheter_pod");
            sr.sprite = pod; sr.color = Color.white; sr.sortingOrder = 9;
            // 确认图比例：pod 长 = 医生身高×52/64 = 1.625 世界；父级 lossyScale 需除回。
            obj.transform.localScale = Vector3.one * (1.625f / (pod.bounds.size.y * transform.lossyScale.y));
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
        int count = Mathf.Min(visible.Count, level + GameBalance.GetInt("skill.suture.count_base", 1));
        for (int i = 0; i < count; i++)
        {
            int swap = Random.Range(i, visible.Count); Enemy temp = visible[i]; visible[i] = visible[swap]; visible[swap] = temp;
            visible[i].TakeDamage(D(Mathf.Max(1, weapon.bulletDamage)));
            // R1-03：侧壁钳吸血仅限普攻子弹命中（Bullet.cs），缝线技能路径不触发。
            CharacterAttackEffect.SutureLightning(this, visible[i].transform.position, level >= 3);
        }
    }

    // 吻合钉枪：向视野内随机敌人发射高速穿透缝合钉。
    public void StapleVolley()
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
        int count = Mathf.Min(visible.Count, GameBalance.GetInt("evolution.staple.count", 3));
        for (int i = 0; i < count; i++)
        {
            int swap = Random.Range(i, visible.Count); Enemy temp = visible[i]; visible[i] = visible[swap]; visible[swap] = temp;
            Vector2 dir = ((Vector2)(visible[i].transform.position - transform.position)).normalized;
            GameObject shot = weapon.SpawnBullet(dir, GameBalance.GetInt("evolution.staple.pierce", 3),
                Vector3.one, GameBalance.Get("evolution.staple.speed_mult", 1.6f));
            if (shot == null) continue;
            SpriteRenderer sr = shot.GetComponent<SpriteRenderer>();
            Sprite stapleSprite = MonsterSurvivorArtLibrary.LoadVfx("vfx_staple");
            if (sr != null && stapleSprite != null)
            {
                sr.sprite = stapleSprite;
                sr.sortingOrder = 9;
                // 24px@ppu96=0.25 世界；弹体高 0.45 世界（约医生身高 1/5）。
                shot.transform.localScale = Vector3.one * (0.45f / stapleSprite.bounds.size.y);
                CircleCollider2D col = shot.GetComponent<CircleCollider2D>();
                if (col != null) col.radius = 0.12f;
            }
        }
    }

    // 侧壁钳（v3 §7）：普攻 15% 概率回复 5% 血量，各级同值。
    public void TrySidewallHeal()
    {
        int level = GetVesselSkillLevel(UpgradeType.SidewallForceps);
        if (level <= 0) return;
        float chance = GameBalance.Get("skill.forceps.chance", 0.15f);
        if (Random.value <= chance && stats != null)
        {
            int amount = Mathf.Max(1, Mathf.CeilToInt(stats.maxHP * GameBalance.Get("skill.forceps.heal_percent", 0.05f)));
            stats.Heal(amount);
        }
    }

    // 旧版弹簧圈：已移出掉落池（等级恒 0），代码保留不产出。
    public void ApplySpringCoil(Enemy enemy)
    {
        int level = GetVesselSkillLevel(UpgradeType.SpringCoil);
        if (level <= 0 || enemy == null) return;
        enemy.ApplySlow(level >= 3 ? GameBalance.Get("skill.coil.slow_lv3", 0.70f) :
            (level == 2 ? GameBalance.Get("skill.coil.slow_lv2", 0.80f) : GameBalance.Get("skill.coil.slow_lv1", 0.90f)),
            GameBalance.Get("skill.coil.duration", 1.2f));
        CharacterAttackEffect.HitBlock(enemy.transform.position, level >= 3 ? 0.3f : 0.2f);
    }

    // 显微止血夹：击杀留区，持续 1/2/3s（×设备更新时长倍率）。进化栓塞止血网后不再生成。
    public void HandleEnemyDied(Enemy enemy)
    {
        if (enemy == null) return;
        if (GetVesselSkillLevel(UpgradeType.EmbolismHemostatNet) > 0) return;
        int level = GetVesselSkillLevel(UpgradeType.MicroscopicHemostat);
        if (level <= 0) return;
        hemostatPools.RemoveAll(pool => pool == null || pool.retired);
        // v3 未给区域数量上限；设安全上限 8 防性能问题（标旗）。
        int safetyCap = GameBalance.GetInt("skill.clamp.pool_cap", 8);
        while (hemostatPools.Count >= safetyCap) { hemostatPools[0].Retire(); hemostatPools.RemoveAt(0); }
        // R1-05：止血夹池为纯伤害区域，tick 不附加减速（§6 无减速字面）。
        CharacterAttackEffect clampPool = CharacterAttackEffect.Pool(this, enemy.transform.position,
            GameBalance.Get("skill.clamp.radius_base", 1.25f) + level * GameBalance.Get("skill.clamp.radius_per_level", 0.25f),
            level * GameBalance.Get("skill.clamp.duration", 1f) * DurationMult(),
            D(Mathf.Max(1, Mathf.FloorToInt(weapon.bulletDamage / GameBalance.Get("skill.clamp.damage_div", 2f)))),
            GameBalance.Get("skill.clamp.tick", 0.25f), false);
        clampPool.hemostatMark = true;
        hemostatPools.Add(clampPool);
    }

    public void ThrowAblationBombs(int level)
    {
        RefreshTargets();
        int bombCount = level >= 2 ? GameBalance.GetInt("skill.ablation.count_lv2", 3) : GameBalance.GetInt("skill.ablation.count_lv1", 2);
        float delay = level >= 3 ? GameBalance.Get("skill.ablation.delay_lv3", 0.5f) : GameBalance.Get("skill.ablation.delay_lv12", 1f);
        List<Enemy> candidates = new List<Enemy>();
        Camera camera = Camera.main;
        for (int i = 0; i < targets.Count; i++)
        {
            Enemy enemy = targets[i];
            if (enemy == null || enemy.currentHP <= 0) continue;
            Vector3 viewport = camera == null ? Vector3.one * 0.5f : camera.WorldToViewportPoint(enemy.transform.position);
            if (viewport.z > 0f && viewport.x >= 0f && viewport.x <= 1f && viewport.y >= 0f && viewport.y <= 1f) candidates.Add(enemy);
        }
        for (int i = 0; i < Mathf.Min(bombCount, candidates.Count); i++)
        {
            int swap = Random.Range(i, candidates.Count);
            Enemy temp = candidates[i]; candidates[i] = candidates[swap]; candidates[swap] = temp;
            CharacterAttackEffect.BombThrow(this, candidates[i].transform.position,
                delay + GameBalance.Get("skill.ablation.extra_delay", 0f),
                GameBalance.Get("skill.ablation.blast_radius", 2.2f) * SizeMult(),
                D(Mathf.Max(GameBalance.GetInt("skill.ablation.damage_min", 2),
                    weapon.bulletDamage + GameBalance.GetInt("skill.ablation.damage_add", 2))));
        }
    }

    public IEnumerator SecondSlash(Vector2 aim)
    {
        yield return new WaitForSeconds(GameBalance.Get("doctor.double_slash_delay", 0.16f));
        if (stats != null && stats.currentHP > 0) Slash(aim);
    }

    // 医生 lv0-3：扇形近战；lv1 半径角度小幅变大；lv2 双击；lv3 更改为电刀且半径角度再变大。
    public void Slash(Vector2 aim)
    {
        float radius = rank >= 3 ? GameBalance.Get("doctor.radius_r3", 4.1f) :
            (rank >= 1 ? GameBalance.Get("doctor.radius_r1", 3.5f) : GameBalance.Get("doctor.radius_r0", 3f));
        float angle = rank >= 3 ? GameBalance.Get("doctor.angle_r3", 115f) :
            (rank >= 1 ? GameBalance.Get("doctor.angle_r1", 95f) : GameBalance.Get("doctor.angle_r0", 80f));
        CharacterAttackEffect.Sector(this, transform.position, aim, radius, angle,
            D(Mathf.Max(1, weapon.bulletDamage + GameBalance.GetInt("doctor.bonus_damage", 1))), rank >= 3);
    }

    // 护士 lv0-3：针头子弹；lv1 攻速加快（IntervalMultiplier）；lv2 弹道+1；lv3 穿透 2。
    public void FireNeedles(Vector2 aim)
    {
        int count = rank >= 2 ? 2 : 1;
        float size = SizeMult();
        for (int i = 0; i < count; i++)
        {
            float spreadAngle = GameBalance.Get("nurse.spread_angle", 5f);
            float spread = count == 1 ? 0f : (i == 0 ? -spreadAngle : spreadAngle);
            Vector2 direction = Quaternion.Euler(0f, 0f, spread) * aim;
            GameObject projectile = weapon.SpawnBullet(direction, rank >= 3 ? 2 : 1, Vector3.one * 3f * size);
            if (projectile != null)
            {
                // 针 36px@ppu96×3=1.125 世界（px/32 约定）；prefab 碰撞半径 0.45 随缩放放大，收回到针尖判定。
                SpriteRenderer sr = projectile.GetComponent<SpriteRenderer>();
                if (sr != null) sr.sprite = MonsterSurvivorArtLibrary.LoadVfx("vfx_needle_f0");
                CircleCollider2D col = projectile.GetComponent<CircleCollider2D>();
                if (col != null) col.radius = 0.07f;
                Bullet needle = projectile.GetComponent<Bullet>();
                if (needle != null) needle.pixelNeedle = true;
            }
        }
    }

    // 旧版护士 rank5 药流：v3 封顶 lv3，不再可达，代码保留。
    public void DrugStream(Vector2 aim)
    {
        float length = Mathf.Clamp(weapon.bulletSpeed, GameBalance.Get("nurse.beam_length_min", 7f),
            GameBalance.Get("nurse.beam_length_max", 16f));
        CharacterAttackEffect.Beam(this, transform.position, aim, length, GameBalance.Get("nurse.beam_width", 0.85f),
            D(Mathf.Max(1, weapon.bulletDamage)), GameBalance.Get("nurse.beam_duration", 0.22f));
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
        CharacterAttackEffect.Throw(this, landing, false);
    }

    // 药剂师 lv0-3（附录C3 列位=等级序）：lv0 范围伤害环；lv1 属性强化；lv2 攻击频率；lv3 药剂滩最多 2 个。
    // 进化·雾化给药机：药剂池最多 3 滩。
    public void AmpouleImpact(Vector3 position)
    {
        CharacterAttackEffect.PixelSplash(this, position, GameBalance.Get("pharmacist.blast_radius", 2.5f) * SizeMult(),
            D(Mathf.Max(1, weapon.bulletDamage + GameBalance.GetInt("pharmacist.blast_damage_add", 1))));
        bool atomizer = GetVesselSkillLevel(UpgradeType.AtomizationDrugMachine) > 0;
        if (rank < 3 && !atomizer) return;
        int poolMax = atomizer ? GameBalance.GetInt("evolution.ampoule_pool.max", 3) : GameBalance.GetInt("pharmacist.pool_max", 2);
        medicinePools.RemoveAll(pool => pool == null || pool.retired);
        while (medicinePools.Count >= Mathf.Max(1, poolMax))
        {
            medicinePools[0].Retire();
            medicinePools.RemoveAt(0);
        }
        // R1-05：药剂师药剂滩减速有源依据（§5 lv3「可减速并…持续伤害」），tick 减速保留。
        medicinePools.Add(CharacterAttackEffect.Pool(this, position, GameBalance.Get("pharmacist.pool_radius", 2.2f) * SizeMult(),
            GameBalance.Get("pharmacist.pool_duration", 4f) * DurationMult(), D(Mathf.Max(1, weapon.bulletDamage)),
            GameBalance.Get("pharmacist.pool_tick", 0.6f), true, true));
        if (atomizer) CharacterAttackEffect.MistPuff(this, position);
    }

    // 旧版 rank4+ 特殊技：v3 封顶 lv3，不再可达，代码保留。
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
            CharacterAttackEffect.Throw(this, destination, true);
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
            else enemy.TakeDamage(D(Mathf.Max(1, weapon.bulletDamage)));
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
    }
}