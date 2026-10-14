using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;
    public float gameDuration = 300f;
    public float elapsedTime;
    public bool gameEnded;
    public bool victory;
    public PlayerStats playerStats;
    public SkillCodexManager skillCodexManager;
    public KnowledgeTicker knowledgeTicker;
    public LevelUpManager levelUpManager;
    public PlayerSkillCollection playerSkillCollection;
    public int selectedLevel = 1;
    public string selectedLevelName = "血管起点";
    public event Action<bool> OnGameEnded;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        if (skillCodexManager == null) skillCodexManager = GetComponent<SkillCodexManager>();
        if (skillCodexManager == null) skillCodexManager = gameObject.AddComponent<SkillCodexManager>();
        skillCodexManager.isGameplay = true;
        if (knowledgeTicker == null) knowledgeTicker = GetComponent<KnowledgeTicker>();
        if (knowledgeTicker == null) knowledgeTicker = gameObject.AddComponent<KnowledgeTicker>();
        Time.timeScale = 1f;
        selectedLevel = Mathf.Clamp(PlayerPrefs.GetInt("VesselGuard.SelectedLevel", 1), 1, 3);
        selectedLevelName = selectedLevel == 1 ? "血管起点" : (selectedLevel == 2 ? "斑块挑战" : "健康冲刺");
    }

    void Start()
    {
        playerStats = FindAnyObjectByType<PlayerStats>();
        if (playerStats != null) playerStats.OnDeath += HandleDeath;
        levelUpManager = FindAnyObjectByType<LevelUpManager>();
        EnemySpawner spawner = FindAnyObjectByType<EnemySpawner>();
        if (spawner != null) spawner.spawnInterval = Mathf.Max(0.8f, 2.4f - (selectedLevel - 1) * 0.45f);
        if (levelUpManager != null) levelUpManager.OnUpgradeApplied += HandleUpgradeApplied;
        if (playerStats != null)
        {
            playerSkillCollection = playerStats.GetComponent<PlayerSkillCollection>();
            if (playerSkillCollection == null) playerSkillCollection = playerStats.gameObject.AddComponent<PlayerSkillCollection>();
            if (levelUpManager != null) playerSkillCollection.Initialize(levelUpManager.allUpgrades);
        }
        StartGame();
    }

    void Update()
    {
        if (gameEnded || Time.timeScale <= 0f) return;
        elapsedTime = Mathf.Min(gameDuration, elapsedTime + Time.deltaTime);
        if (elapsedTime >= gameDuration) EndGame();
    }

    public void StartGame()
    {
        elapsedTime = 0f;
        gameEnded = false;
        victory = false;
        Time.timeScale = 1f;
    }

    public void HandleUpgradeApplied(UpgradeOption option)
    {
        if (skillCodexManager != null) skillCodexManager.UnlockForUpgrade(option);
        if (knowledgeTicker != null) knowledgeTicker.ShowForUpgrade(option);
        if (playerSkillCollection != null) playerSkillCollection.RecordSelection(option);
    }

    public float GetRemaining() { return Mathf.Max(0f, gameDuration - elapsedTime); }
    public void EndGame() { FinishGame(true); }
    public void HandleDeath() { FinishGame(false); }

    public void FinishGame(bool won)
    {
        if (gameEnded) return;
        gameEnded = true;
        victory = won;
        if (won) elapsedTime = gameDuration;
        Time.timeScale = 0f;
        OnGameEnded?.Invoke(won);
    }

    public void RestartGame()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    void OnDestroy()
    {
        if (playerStats != null) playerStats.OnDeath -= HandleDeath;
        if (levelUpManager != null) levelUpManager.OnUpgradeApplied -= HandleUpgradeApplied;
        if (Instance == this) { Instance = null; Time.timeScale = 1f; }
    }
}
// skill collection wired
