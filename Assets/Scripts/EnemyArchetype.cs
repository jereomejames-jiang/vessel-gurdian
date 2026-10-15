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

        switch (kind)
        {
            case EnemyKind.CellShadow:
                enemy.maxHP = 1;
                enemy.moveSpeed = 2.8f + stageIndex * 0.2f;
                enemy.contactDamage = 1;
                enemy.xpValue = 2;
                transform.localScale = Vector3.one * 0.62f;
                break;
            case EnemyKind.SpikeSoldier:
                enemy.maxHP = 3 + stageIndex;
                enemy.moveSpeed = 1.65f + stageIndex * 0.18f;
                enemy.contactDamage = 2;
                enemy.xpValue = 3;
                transform.localScale = Vector3.one * 0.9f;
                break;
            case EnemyKind.RupturedCell:
                enemy.maxHP = 4 + stageIndex * 2;
                enemy.moveSpeed = 1.1f + stageIndex * 0.1f;
                enemy.contactDamage = 2;
                enemy.xpValue = 5;
                transform.localScale = Vector3.one * 1.05f;
                break;
            case EnemyKind.MicroThrombus:
                enemy.maxHP = 8 + stageIndex * 3;
                enemy.moveSpeed = 0.65f + stageIndex * 0.08f;
                enemy.contactDamage = 2;
                enemy.xpValue = 6;
                transform.localScale = Vector3.one * 1.35f;
                break;
            case EnemyKind.BleedingGap:
                enemy.maxHP = 12 + stageIndex * 3;
                enemy.moveSpeed = 0f;
                enemy.contactDamage = 1;
                enemy.xpValue = 5;
                transform.localScale = Vector3.one * 1.25f;
                break;
            case EnemyKind.NodeBoss:
                enemy.maxHP = 34 + stageIndex * 12;
                enemy.moveSpeed = 0.7f + stageIndex * 0.08f;
                enemy.contactDamage = 3;
                enemy.xpValue = 20;
                transform.localScale = Vector3.one * 2.2f;
                break;
        }

        // Higher stages reward the extra pressure with faster experience gain.
        float stageXpMultiplier = 1f + (stageIndex - 1) * 0.25f;
        enemy.xpValue = Mathf.Max(1, Mathf.RoundToInt(enemy.xpValue * stageXpMultiplier));
        enemy.currentHP = enemy.maxHP;
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
        spriteRenderer.sortingOrder = kind == EnemyKind.NodeBoss ? 4 : 2;
    }

    public void HandleEnemyDied(Enemy deadEnemy)
    {
        if (kind == EnemyKind.RupturedCell)
        {
            EnemySpawner spawner = FindAnyObjectByType<EnemySpawner>();
            if (spawner != null)
            {
                for (int i = 0; i < 2; i++)
                {
                    Vector2 offset = Random.insideUnitCircle * 0.55f;
                    spawner.SpawnSpecific(EnemyKind.CellShadow, transform.position + (Vector3)offset);
                }
            }
        }
    }

    public void OnDestroy()
    {
        if (subscribed && enemy != null) enemy.OnDied -= HandleEnemyDied;
    }
}
