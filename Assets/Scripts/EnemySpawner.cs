using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    public GameObject enemyPrefab;
    public float spawnInterval = 2f;
    public float spawnRadius = 12f;
    public Transform player;

    public float spawnTimer;

    void Start()
    {
        if (player == null)
        {
            PlayerController controller = FindAnyObjectByType<PlayerController>();
            if (controller != null) player = controller.transform;
        }
        spawnTimer = spawnInterval;
    }

    void Update()
    {
        if (Time.timeScale <= 0f || enemyPrefab == null || player == null) return;

        spawnTimer -= Time.deltaTime;
        if (spawnTimer <= 0f)
        {
            spawnTimer = spawnInterval;
            SpawnEnemy();
        }
    }

    public void SpawnEnemy()
    {
        if (Time.timeScale <= 0f || enemyPrefab == null || player == null) return;

        float angle = Random.Range(0f, Mathf.PI * 2f);
        Vector3 offset = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * spawnRadius;
        Instantiate(enemyPrefab, player.position + offset, Quaternion.identity);
    }
}
