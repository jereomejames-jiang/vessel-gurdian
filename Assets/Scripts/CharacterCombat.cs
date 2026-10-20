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

    public void Update()
    {
        if (!initialized || Time.timeScale <= 0f || stats.currentHP <= 0 ||
            (GameManager.Instance != null && GameManager.Instance.gameEnded)) return;
        evolutionNoticeTimer = Mathf.Max(0f, evolutionNoticeTimer - Time.deltaTime);
        VesselSkillUpdate();
        if (rank < 4) return;
        specialTimer -= Time.deltaTime;
        if (specialTimer > 0f) return;
        specialTimer = role == CharacterRole.Nurse ? 8f : 12f;
        PerformSpecial();
    }

    public void HandleLevelUp(int playerLevel)
    {
        int newRank = CharacterCatalog.RankForLevel(playerLevel);
        if (newRank <= rank) return;
        // Apply crossed thresholds once, including XP gains spanning several levels.
        if (role == CharacterRole.Pharmacist && rank < 1 && newRank >= 1)
        {
            if (controller != null) controller.moveSpeed *= 1.1f;
            stats.IncreaseMaxHP(1);
        }
        if (rank < 4 && newRank >= 4) specialTimer = 3f;
        rank = newRank;
        evolutionNotice = CharacterCatalog.Name(role) + "进化 " + rank + "：" + CharacterCatalog.Route(role, rank);
        evolutionNoticeTimer = 5f;
    }

    public float IntervalMultiplier()
    {
        if (role == CharacterRole.Doctor) return 1f;
        if (role == CharacterRole.Nurse) return rank >= 5 ? 0.32f : (rank >= 1 ? 0.72f : 1f);
        return rank >= 5 ? 0.5f : (rank >= 2 ? 0.75f : 1f);
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
        level = Mathf.Clamp(level, 1, 3);
        if (type == UpgradeType.MembraneStent && stats != null) stats.ConfigureMembraneStent(level);
        if (type == UpgradeType.MembraneStent) EnsureMembraneShield(level);
        if (type == UpgradeType.FlushNeedle) EnsureFlushNeedles(level + 1);
        if (type == UpgradeType.ThrombusCatheter) EnsureCatheters(level);
        evolutionNotice = VesselSkillCatalog.GlyphFor(type) + "  " + CharacterCatalog.Name(role) + "获得 " + VesselSkillCatalog.EducationFor(type).Split('。')[0];
        evolutionNoticeTimer = 3.5f;
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

    public void HandleAttackSkills()
    {
        int sidewall = GetVesselSkillLevel(UpgradeType.SidewallForceps);
        int suture = GetVesselSkillLevel(UpgradeType.Suture);
        if (suture > 0) SutureStrike(suture);
        if (sidewall > 0) { /* healing is triggered by each hit in DamageArea/Bullet */ }
    }

    public void VesselSkillUpdate()
    {
        int scissors = GetVesselSkillLevel(UpgradeType.DissectionScissors);
        if (scissors > 0)
        {
            vesselSkillTimer -= Time.deltaTime;
            if (vesselSkillTimer <= 0f)
            {
                vesselSkillTimer = Mathf.Max(1.2f, 2.8f - scissors * 0.35f);
                Vector2 aim = AimDirection();
                float radius = scissors >= 2 ? 4.1f : 3.3f;
                CharacterAttackEffect.Sector(this, transform.position, aim, radius, scissors >= 2 ? 100f : 82f,
                    Mathf.Max(1, weapon.bulletDamage), true);
                if (scissors >= 3)
                    CharacterAttackEffect.Sector(this, transform.position, Quaternion.Euler(0f, 0f, 90f) * aim, radius, 42f,
                        Mathf.Max(1, weapon.bulletDamage), true);
            }
        }

        int flush = GetVesselSkillLevel(UpgradeType.FlushNeedle);
        if (flush > 0)
        {
            EnsureFlushNeedles(flush + 1);
            float radius = 1.25f + flush * 0.18f;
            for (int i = 0; i < flushNeedles.Count; i++)
            {
                float angle = Time.time * (95f + flush * 12f) + i * 360f / flushNeedles.Count;
                Vector3 pos = transform.position + Quaternion.Euler(0f, 0f, angle) * Vector3.right * radius;
                flushNeedles[i].transform.position = pos;
                RefreshTargets();
                for (int j = 0; j < targets.Count; j++)
                {
                    Enemy enemy = targets[j];
                    if (enemy != null && (enemy.transform.position - pos).sqrMagnitude < 0.55f * 0.55f)
                        enemy.ApplyKnockback((enemy.transform.position - transform.position).normalized, 0.18f);
                }
            }
        }

        int catheter = GetVesselSkillLevel(UpgradeType.ThrombusCatheter);
        if (catheter > 0)
        {
            EnsureCatheters(catheter);
            catheterTimer -= Time.deltaTime;
            for (int i = 0; i < catheters.Count; i++)
            {
                float angle = Time.time * 55f + i * 360f / catheters.Count;
                catheters[i].transform.position = transform.position + Quaternion.Euler(0f, 0f, angle) * Vector3.up * 1.55f;
            }
            if (catheterTimer <= 0f)
            {
                catheterTimer = 0.9f;
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
                        CharacterAttackEffect.Beam(this, catheters[i].transform.position, direction, 4.2f, 0.15f,
                            Mathf.Max(1, weapon.bulletDamage / 2), 0.18f);
                    }
            }
        }

        int bomb = GetVesselSkillLevel(UpgradeType.AblationBomb);
        if (bomb > 0)
        {
            ablationBombTimer -= Time.deltaTime;
            if (ablationBombTimer <= 0f)
            {
                ablationBombTimer = 2.5f;
                ThrowAblationBombs(bomb);
            }
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
        int count = Mathf.Min(visible.Count, level >= 3 ? 4 : (level == 2 ? 3 : 2));
        for (int i = 0; i < count; i++)
        {
            int swap = Random.Range(i, visible.Count); Enemy temp = visible[i]; visible[i] = visible[swap]; visible[swap] = temp;
            visible[i].TakeDamage(Mathf.Max(1, weapon.bulletDamage));
            if (GetVesselSkillLevel(UpgradeType.SidewallForceps) > 0) TrySidewallHeal();
            CharacterAttackEffect.SutureLightning(this, visible[i].transform.position, level >= 3);
        }
    }

    public void TrySidewallHeal()
    {
        int level = GetVesselSkillLevel(UpgradeType.SidewallForceps);
        if (level <= 0) return;
        float chance = level >= 3 ? 0.30f : (level == 2 ? 0.20f : 0.15f);
        if (Random.value <= chance && stats != null)
        {
            int amount = Mathf.Max(1, Mathf.CeilToInt(stats.maxHP * 0.05f));
            stats.Heal(amount);
        }
    }

    public void ApplySpringCoil(Enemy enemy)
    {
        int level = GetVesselSkillLevel(UpgradeType.SpringCoil);
        if (level <= 0 || enemy == null) return;
        enemy.ApplySlow(level >= 3 ? 0.70f : (level == 2 ? 0.80f : 0.90f), 1.2f);
        CharacterAttackEffect.HitBlock(enemy.transform.position, level >= 3 ? 0.3f : 0.2f);
    }

    public void HandleEnemyDied(Enemy enemy)
    {
        if (enemy == null) return;
        int level = GetVesselSkillLevel(UpgradeType.MicroscopicHemostat);
        if (level <= 0) return;
        hemostatPools.RemoveAll(pool => pool == null || pool.retired);
        while (hemostatPools.Count >= level) { hemostatPools[0].Retire(); hemostatPools.RemoveAt(0); }
        hemostatPools.Add(CharacterAttackEffect.Pool(this, enemy.transform.position, 1.25f + level * 0.25f, 1f,
            Mathf.Max(1, weapon.bulletDamage / 2), 0.25f));
    }

    public void ThrowAblationBombs(int level)
    {
        RefreshTargets();
        int bombCount = level >= 3 ? 3 : 2;
        float delay = level >= 3 ? 0.5f : 1f;
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
            StartCoroutine(ThrowDelayedBomb(candidates[i].transform.position, delay));
        }
    }

    public System.Collections.IEnumerator ThrowDelayedBomb(Vector3 landing, float delay)
    {
        CharacterAttackEffect.Throw(this, landing, false);
        yield return new WaitForSeconds(0.35f + delay);
        CharacterAttackEffect.Burst(this, landing, 2.2f, Mathf.Max(2, weapon.bulletDamage + 2), true);
    }

    public IEnumerator SecondSlash(Vector2 aim)
    {
        yield return new WaitForSeconds(0.16f);
        if (stats != null && stats.currentHP > 0) Slash(aim);
    }

    public void Slash(Vector2 aim)
    {
        float radius = rank >= 3 ? 4.1f : (rank >= 1 ? 3.5f : 3f);
        float angle = rank >= 5 ? 170f : (rank >= 3 ? 115f : (rank >= 1 ? 95f : 80f));
        CharacterAttackEffect.Sector(this, transform.position, aim, radius, angle,
            Mathf.Max(1, weapon.bulletDamage + 1), rank >= 5);
    }

    public void FireNeedles(Vector2 aim)
    {
        int count = rank >= 2 ? 2 : 1;
        for (int i = 0; i < count; i++)
        {
            float spread = count == 1 ? 0f : (i == 0 ? -5f : 5f);
            Vector2 direction = Quaternion.Euler(0f, 0f, spread) * aim;
            weapon.SpawnBullet(direction, rank >= 3 ? 2 : 1, new Vector3(0.16f, 0.65f, 1f));
        }
    }

    public void DrugStream(Vector2 aim)
    {
        float length = Mathf.Clamp(weapon.bulletSpeed, 7f, 16f);
        CharacterAttackEffect.Beam(this, transform.position, aim, length, 0.85f,
            Mathf.Max(1, weapon.bulletDamage), 0.22f);
    }

    public void ThrowAmpoule(Vector2 aim)
    {
        Vector3 landing = transform.position + (Vector3)(aim * 4f);
        float best = 8f * 8f;
        for (int i = 0; i < targets.Count; i++)
        {
            Enemy enemy = targets[i];
            if (enemy == null || enemy.currentHP <= 0) continue;
            float distance = (enemy.transform.position - transform.position).sqrMagnitude;
            if (distance < best) { best = distance; landing = enemy.transform.position; }
        }
        CharacterAttackEffect.Throw(this, landing, false);
    }

    public void AmpouleImpact(Vector3 position)
    {
        CharacterAttackEffect.Burst(this, position, 2.5f, Mathf.Max(1, weapon.bulletDamage + 1), rank >= 5);
        if (rank < 3) return;
        medicinePools.RemoveAll(pool => pool == null || pool.retired);
        while (medicinePools.Count >= 2)
        {
            medicinePools[0].Retire();
            medicinePools.RemoveAt(0);
        }
        medicinePools.Add(CharacterAttackEffect.Pool(this, position, 2.2f, 4f,
            Mathf.Max(1, weapon.bulletDamage), 0.6f));
    }

    public void PerformSpecial()
    {
        if (role == CharacterRole.Nurse)
        {
            Vector2 aim = AimDirection();
            Vector3 destination = transform.position + (Vector3)(aim * 6f);
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
            if (role == CharacterRole.Doctor) enemy.ApplySlow(0.4f, 2.5f);
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
    }
}

