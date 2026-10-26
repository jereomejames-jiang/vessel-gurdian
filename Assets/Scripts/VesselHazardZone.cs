using System.Collections.Generic;
using UnityEngine;

public class VesselHazardZone : MonoBehaviour
{
    public Transform owner;
    public float remaining = 5f;
    public float damageInterval = 1f;
    public int damage = 0;
    public float slowMultiplier = 0.5f;
    public bool destroyWithOwner;
    public CircleCollider2D trigger;
    public SpriteRenderer spriteRenderer;
    public float damageTimer;
    [System.NonSerialized] public HashSet<PlayerController> affectedPlayers = new HashSet<PlayerController>();
    public static Sprite hazardSprite;

    public static VesselHazardZone Create(Vector3 position, float radius, int damageValue,
        float slow, GameObject ownerObject, float duration = 9999f, float damageIntervalValue = 1f)
    {
        GameObject zoneObject = new GameObject("VesselHazardZone");
        zoneObject.transform.position = position;
        VesselHazardZone zone = zoneObject.AddComponent<VesselHazardZone>();
        zone.remaining = duration;
        zone.damageInterval = damageIntervalValue;
        zone.damage = Mathf.Max(0, damageValue);
        zone.slowMultiplier = Mathf.Clamp(slow, GameBalance.Get("player.slow_zone_min", 0.1f), 1f);
        zone.owner = ownerObject == null ? null : ownerObject.transform;
        zone.destroyWithOwner = ownerObject != null;
        zone.transform.localScale = Vector3.one * Mathf.Max(0.2f, radius * 2f);
        return zone;
    }

    public void Awake()
    {
        trigger = gameObject.AddComponent<CircleCollider2D>();
        trigger.isTrigger = true;
        trigger.radius = 0.5f;

        spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = GetHazardSprite();
        spriteRenderer.color = new Color(0.95f, 0.18f, 0.62f, 0.26f);
        spriteRenderer.sortingOrder = 1;
    }

    public void Update()
    {
        if (destroyWithOwner && owner == null)
        {
            Destroy(gameObject);
            return;
        }

        remaining -= Time.deltaTime;
        damageTimer -= Time.deltaTime;
        if (remaining <= 0f) Destroy(gameObject);
    }

    public void OnTriggerEnter2D(Collider2D other)
    {
        PlayerController controller = other.GetComponent<PlayerController>();
        if (controller == null) controller = other.GetComponentInParent<PlayerController>();
        if (controller == null) return;
        if (affectedPlayers.Add(controller)) controller.AddSlowZone(slowMultiplier);
        ApplyDamage(controller);
    }

    public void OnTriggerStay2D(Collider2D other)
    {
        PlayerController controller = other.GetComponent<PlayerController>();
        if (controller == null) controller = other.GetComponentInParent<PlayerController>();
        if (controller == null) return;
        if (affectedPlayers.Add(controller)) controller.AddSlowZone(slowMultiplier);
        ApplyDamage(controller);
    }

    public void OnTriggerExit2D(Collider2D other)
    {
        PlayerController controller = other.GetComponent<PlayerController>();
        if (controller == null) controller = other.GetComponentInParent<PlayerController>();
        if (controller == null) return;
        if (affectedPlayers.Remove(controller)) controller.RemoveSlowZone(slowMultiplier);
    }

    public void ApplyDamage(PlayerController controller)
    {
        if (damage <= 0 || damageTimer > 0f || controller == null) return;
        PlayerStats stats = controller.GetComponent<PlayerStats>();
        if (stats == null) stats = controller.GetComponentInParent<PlayerStats>();
        if (stats == null) return;
        damageTimer = damageInterval;
        stats.TakeDamage(damage);
    }

    public Sprite GetHazardSprite()
    {
        if (hazardSprite != null) return hazardSprite;
        Texture2D texture = new Texture2D(64, 64, TextureFormat.RGBA32, false);
        texture.name = "RuntimeHazardZoneTexture";
        texture.filterMode = FilterMode.Bilinear;
        Color[] pixels = new Color[64 * 64];
        Vector2 center = new Vector2(31.5f, 31.5f);
        for (int y = 0; y < 64; y++)
        {
            for (int x = 0; x < 64; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center) / 31.5f;
                // Soft interior fill plus a bright ring AT the damage boundary, so the
                // edge the player must avoid is clearly visible instead of fading out.
                float fill = Mathf.Clamp01(1f - distance) * 0.26f;
                float ring = Mathf.Clamp01(1f - Mathf.Abs(distance - 0.94f) / 0.06f);
                float alpha = Mathf.Clamp01(Mathf.Max(fill, ring));
                pixels[y * 64 + x] = new Color(1f, 0.06f, 0.55f, alpha);
            }
        }
        texture.SetPixels(pixels);
        texture.Apply(false, true);
        hazardSprite = Sprite.Create(texture, new Rect(0f, 0f, 64f, 64f),
            new Vector2(0.5f, 0.5f), 64f);
        hazardSprite.name = "RuntimeHazardZone";
        return hazardSprite;
    }

    // A thin white ring used to telegraph an incoming blast. Tinted at the caller.
    static Sprite ringSprite;
    public static Sprite GetRingSprite()
    {
        if (ringSprite != null) return ringSprite;
        Texture2D texture = new Texture2D(64, 64, TextureFormat.RGBA32, false);
        texture.name = "RuntimeRingTexture";
        texture.filterMode = FilterMode.Bilinear;
        Color[] pixels = new Color[64 * 64];
        Vector2 center = new Vector2(31.5f, 31.5f);
        for (int y = 0; y < 64; y++)
        {
            for (int x = 0; x < 64; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center) / 31.5f;
                float ring = Mathf.Clamp01(1f - Mathf.Abs(distance - 0.96f) / 0.04f);
                pixels[y * 64 + x] = new Color(1f, 1f, 1f, ring);
            }
        }
        texture.SetPixels(pixels);
        texture.Apply(false, true);
        ringSprite = Sprite.Create(texture, new Rect(0f, 0f, 64f, 64f),
            new Vector2(0.5f, 0.5f), 64f);
        ringSprite.name = "RuntimeRing";
        return ringSprite;
    }

    public void OnDestroy()
    {
        foreach (PlayerController controller in affectedPlayers)
            if (controller != null) controller.RemoveSlowZone(slowMultiplier);
        affectedPlayers.Clear();
    }
}
