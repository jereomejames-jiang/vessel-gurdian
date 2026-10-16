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
        stats.OnLevelUp += HandleLevelUp;
        SpriteRenderer renderer = GetComponent<SpriteRenderer>();
        if (renderer != null) renderer.color = CharacterCatalog.Colors[(int)role];
        HandleLevelUp(stats.level);
    }

    public void Update()
    {
        if (!initialized || Time.timeScale <= 0f || stats.currentHP <= 0 ||
            (GameManager.Instance != null && GameManager.Instance.gameEnded)) return;
        evolutionNoticeTimer = Mathf.Max(0f, evolutionNoticeTimer - Time.deltaTime);
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
    }
}

