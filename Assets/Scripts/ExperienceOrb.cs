using UnityEngine;

public class ExperienceOrb : MonoBehaviour
{
    public int xpValue = 1;
    public float magnetRange = 3f;
    public float magnetSpeed = 8f;

    public Transform player;
    public PlayerStats playerStats;
    public bool collected;

    void Update()
    {
        if (collected || Time.timeScale <= 0f) return;
        if (player == null)
        {
            PlayerController controller = FindAnyObjectByType<PlayerController>();
            if (controller != null) player = controller.transform;
        }

        if (player == null) return;

        float distance = Vector2.Distance(transform.position, player.position);
        if (distance <= magnetRange)
        {
            transform.position = Vector3.MoveTowards(transform.position, player.position, magnetSpeed * Time.deltaTime);
        }

        if (distance <= 0.35f)
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
            Destroy(gameObject);
        }
    }
}
