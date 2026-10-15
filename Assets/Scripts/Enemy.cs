using System;
using UnityEngine;

public class Enemy : MonoBehaviour
{
    public int maxHP = 3;
    public float moveSpeed = 2f;
    public int contactDamage = 1;
    public int xpValue = 1;
    public GameObject xpOrbPrefab;
    public bool externalMovement;
    public event Action<Enemy> OnDied;

    public int currentHP;
    public Transform player;
    public Rigidbody2D rb;

    void Awake()
    {
        currentHP = maxHP;
        rb = GetComponent<Rigidbody2D>();
    }

    void Start()
    {
        PlayerController controller = FindAnyObjectByType<PlayerController>();
        if (controller != null) player = controller.transform;
    }

    void FixedUpdate()
    {
        if (externalMovement) return;
        if (player == null)
        {
            PlayerController controller = FindAnyObjectByType<PlayerController>();
            if (controller != null) player = controller.transform;
        }

        if (player == null) return;

        Vector3 direction = (player.position - transform.position).normalized;
        if (rb != null) rb.MovePosition(rb.position + (Vector2)direction * moveSpeed * Time.fixedDeltaTime);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        PlayerStats stats = other.GetComponent<PlayerStats>();
        if (stats == null) stats = other.GetComponentInParent<PlayerStats>();
        if (stats != null && currentHP > 0) stats.TakeDamage(contactDamage);
    }

    public void TakeDamage(int amount)
    {
        if (amount <= 0 || currentHP <= 0) return;

        currentHP -= amount;
        if (currentHP <= 0)
        {
            OnDied?.Invoke(this);
            if (xpOrbPrefab != null)
            {
                GameObject orbObject = Instantiate(xpOrbPrefab, transform.position, Quaternion.identity);
                ExperienceOrb orb = orbObject.GetComponent<ExperienceOrb>();
                if (orb != null) orb.xpValue = Mathf.Max(1, xpValue);
            }
            Destroy(gameObject);
        }
    }

    public void IncreaseMaxHP(int amount)
    {
        if (amount <= 0) return;
        maxHP += amount;
        currentHP += amount;
    }
}
