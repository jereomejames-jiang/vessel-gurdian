using UnityEngine;

public class ExperienceOrb : MonoBehaviour
{
    public int xpValue = 1;
    public float magnetRange = 3.5f;
    public float magnetSpeed = 8f;

    public Transform player;
    public PlayerStats playerStats;
    public bool collected;
    CharacterCombat combat;

    void Start()
    {
        magnetRange = GameBalance.Get("xp.magnet_range", magnetRange);
        magnetSpeed = GameBalance.Get("xp.magnet_speed", magnetSpeed);
        combat = FindAnyObjectByType<CharacterCombat>();
        SpriteRenderer renderer = GetComponent<SpriteRenderer>();
        if (renderer == null) return;
        string gem = xpValue >= 15 ? "xp_purple" : (xpValue >= 5 ? "xp_orange" : (xpValue >= 3 ? "xp_green" : "xp_blue"));
        Sprite gemSprite = MonsterSurvivorArtLibrary.LoadSingle(gem);
        if (gemSprite != null) renderer.sprite = gemSprite;
        renderer.color = Color.white;
        renderer.color = xpValue >= 15 ? new Color(1f, 0.72f, 1f) :
            (xpValue >= 5 ? new Color(0.72f, 0.42f, 1f) :
            (xpValue >= 3 ? new Color(1f, 0.76f, 0.24f) : new Color(0.26f, 0.88f, 1f)));
        float scale = xpValue >= 15 ? 3.2f : (xpValue >= 5 ? 2.4f : 1.8f);
        transform.localScale *= scale;
    }

    void Update()
    {
        if (collected || Time.timeScale <= 0f) return;
        if (player == null)
        {
            PlayerController controller = FindAnyObjectByType<PlayerController>();
            if (controller != null) player = controller.transform;
        }

        if (player == null) return;
        if (playerStats == null) playerStats = player.GetComponent<PlayerStats>();

        float distance = Vector2.Distance(transform.position, player.position);
        if (distance <= EffectiveMagnetRange())
        {
            transform.position = Vector3.MoveTowards(transform.position, player.position, magnetSpeed * Time.deltaTime);
        }

        if (distance <= GameBalance.Get("xp.pickup_distance", 0.35f))
        {
            Collect();
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        PlayerStats stats = other.GetComponent<PlayerStats>();
        if (stats == null) stats = other.GetComponentInParent<PlayerStats>();
        if (stats != null)
        {
            playerStats = stats;
            Collect();
        }
    }

    public void Collect()
    {
        if (collected || Time.timeScale <= 0f) return;
        if (playerStats == null && player != null)
        {
            playerStats = player.GetComponent<PlayerStats>();
        }

        if (playerStats != null)
        {
            collected = true;
            playerStats.GainXP(xpValue);
            if (GameManager.Instance != null) GameManager.Instance.RegisterExperienceCollected(xpValue);
            Destroy(gameObject);
        }
    }

    // Boss 出场时停用：无视 timeScale 强制吸入全场经验球。
    public void CollectImmediate()
    {
        if (collected) return;
        if (playerStats == null)
        {
            if (player == null)
            {
                PlayerController controller = FindAnyObjectByType<PlayerController>();
                if (controller != null) player = controller.transform;
            }
            if (player != null) playerStats = player.GetComponent<PlayerStats>();
        }
        collected = true;
        if (playerStats != null)
        {
            playerStats.GainXP(xpValue);
            if (GameManager.Instance != null) GameManager.Instance.RegisterExperienceCollected(xpValue);
        }
        Destroy(gameObject);
    }

    // 吸液管（v3 §7 被动）：拾取半径 +20%/级，随等级实时生效；
    // 磁铁拾取物（v3 §9）：拾取半径 +80%，10s。
    public float EffectiveMagnetRange()
    {
        if (combat == null) combat = FindAnyObjectByType<CharacterCombat>();
        float mult = 1f;
        if (combat != null)
            mult += GameBalance.Get("passive.tube.magnet", 0.2f) * combat.GetVesselSkillLevel(UpgradeType.SuctionTube);
        if (playerStats != null && playerStats.MagnetBoostActive)
            mult *= GameBalance.Get("pickup.magnet_mult", 1.8f);
        return magnetRange * mult;
    }
}
