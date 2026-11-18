using UnityEngine;

// 设定集(3) §9 特殊拾取物：血包（回复20%血量）/迅捷（敌人减速30% 10s）/磁铁（拾取半径+80% 10s）。
// 敌人死亡概率掉落（概率未给数值→pickup.drop_chance=3% 标旗）；医生画风像素图标+上下悬浮，尺寸走 pickup.size_*。
public enum PickupKind { BloodPack, Swift, Magnet }

public class PickupItem : MonoBehaviour
{
    public PickupKind kind;
    float lifetime;
    float bobSeed;
    Transform player;
    PlayerStats stats;
    CharacterCombat combat;
    Vector3 basePosition;

    public static void SpawnRandom(Vector3 position)
    {
        Spawn((PickupKind)Random.Range(0, 3), position);
    }

    public static void Spawn(PickupKind kind, Vector3 position)
    {
        GameObject obj = new GameObject("Pickup_" + kind);
        obj.transform.position = position;
        PickupItem item = obj.AddComponent<PickupItem>();
        item.kind = kind;
        item.Build();
    }

    void Build()
    {
        lifetime = GameBalance.Get("pickup.lifetime", 20f);
        bobSeed = Random.value * Mathf.PI * 2f;
        basePosition = transform.position;
        SpriteRenderer sr = gameObject.AddComponent<SpriteRenderer>();
        sr.sortingOrder = 7;
        string key = kind == PickupKind.BloodPack ? "blood" : kind == PickupKind.Swift ? "swift" : "magnet";
        float size = GameBalance.Get("pickup.size_" + key, kind == PickupKind.Swift ? 1.1f : 0.55f);
        sr.sprite = MonsterSurvivorArtLibrary.LoadVfx("vfx_pickup_" + key, 32f);
        // 16px ppu32 原生 0.5 世界 → 按配置表尺寸统一缩放
        transform.localScale = Vector3.one * (size / 0.5f);
        CircleCollider2D col = gameObject.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = 0.6f;
    }

    void Update()
    {
        if (Time.timeScale <= 0f) return;
        lifetime -= Time.deltaTime;
        if (lifetime <= 0f) { Destroy(gameObject); return; }
        if (player == null)
        {
            PlayerController controller = FindAnyObjectByType<PlayerController>();
            if (controller != null) { player = controller.transform; stats = controller.GetComponent<PlayerStats>(); }
        }
        if (player == null) return;
        if (combat == null) combat = FindAnyObjectByType<CharacterCombat>();

        float distance = Vector2.Distance(transform.position, player.position);
        float magnetRange = GameBalance.Get("xp.magnet_range", 4.6f);
        float mult = 1f;
        if (combat != null) mult += GameBalance.Get("passive.tube.magnet", 0.2f) * combat.GetVesselSkillLevel(UpgradeType.SuctionTube);
        if (stats != null && stats.MagnetBoostActive) mult *= GameBalance.Get("pickup.magnet_mult", 1.8f);
        if (distance <= magnetRange * mult)
        {
            basePosition = Vector3.MoveTowards(basePosition, player.position,
                GameBalance.Get("xp.magnet_speed", 8f) * Time.deltaTime);
        }
        transform.position = basePosition + Vector3.up * (Mathf.Sin(Time.time * GameBalance.Get("pickup.bob_freq", 2.5f) + bobSeed) * GameBalance.Get("pickup.bob_amp", 0.12f));
        if (distance <= GameBalance.Get("xp.pickup_distance", 0.35f) + 0.25f) Collect();
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        PlayerStats otherStats = other.GetComponent<PlayerStats>();
        if (otherStats == null) otherStats = other.GetComponentInParent<PlayerStats>();
        if (otherStats != null)
        {
            stats = otherStats;
            Collect();
        }
    }

    public void Collect()
    {
        if (stats == null && player != null) stats = player.GetComponent<PlayerStats>();
        if (stats == null)
        {
            PlayerController controller = FindAnyObjectByType<PlayerController>();
            if (controller != null) stats = controller.GetComponent<PlayerStats>();
        }
        if (stats == null) return;
        switch (kind)
        {
            case PickupKind.BloodPack:
                stats.Heal(Mathf.Max(1, Mathf.CeilToInt(stats.maxHP * GameBalance.Get("pickup.blood_percent", 0.2f))));
                break;
            case PickupKind.Swift:
                Enemy.globalSlowMultiplier = GameBalance.Get("pickup.swift_slow", 0.7f);
                Enemy.globalSlowUntil = Time.time + GameBalance.Get("pickup.swift_duration", 10f);
                break;
            case PickupKind.Magnet:
                stats.magnetBoostUntil = Time.time + GameBalance.Get("pickup.magnet_duration", 10f);
                break;
        }
        Destroy(gameObject);
    }
}