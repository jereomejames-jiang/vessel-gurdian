using System;
using System.Collections;
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
    public bool requireBossDefeat = true;
    public bool bossDefeated;
    public string stageSummary;
    public int enemiesDefeated;
    public int experienceCollected;
    public event Action OnRunStatsChanged;
    public event Action<bool> OnGameEnded;
    EnemySpawner spawner;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        gameDuration = GameBalance.Get("game.duration", gameDuration);
#if !UNITY_WEBGL
        Screen.SetResolution(1920, 1080, FullScreenMode.MaximizedWindow);
#endif
        if (skillCodexManager == null) skillCodexManager = GetComponent<SkillCodexManager>();
        if (skillCodexManager == null) skillCodexManager = gameObject.AddComponent<SkillCodexManager>();
        skillCodexManager.isGameplay = true;
        if (knowledgeTicker == null) knowledgeTicker = GetComponent<KnowledgeTicker>();
        if (knowledgeTicker == null) knowledgeTicker = gameObject.AddComponent<KnowledgeTicker>();
        Time.timeScale = 1f;
        selectedLevel = Mathf.Clamp(PlayerPrefs.GetInt("VesselGuard.SelectedLevel", 1), 1, 3);
        selectedLevelName = selectedLevel == 1 ? "血管起点" : (selectedLevel == 2 ? "斑块挑战" : "健康冲刺");
        stageSummary = selectedLevel == 1 ? "毛细血管 · 低压与微循环" :
            (selectedLevel == 2 ? "静脉 · 淤积与凝聚" : "动脉 · 高压与斑块");
    }

    void Start()
    {
        playerStats = FindAnyObjectByType<PlayerStats>();
        if (playerStats != null) playerStats.OnDeath += HandleDeath;
        levelUpManager = FindAnyObjectByType<LevelUpManager>();
        spawner = FindAnyObjectByType<EnemySpawner>();
        if (spawner != null) ApplyStageTheme(spawner);
        if (spawner != null) spawner.spawnInterval = selectedLevel == 1 ? GameBalance.Get("game.spawn_interval_s1", 1.15f) :
            (selectedLevel == 2 ? GameBalance.Get("game.spawn_interval_s2", 1f) : GameBalance.Get("game.spawn_interval_s3", 0.85f));
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
        if (Input.GetKeyDown(KeyCode.F8)) { GameBalance.Reload(); SkillText.Reload(); }
        if (gameEnded || Time.timeScale <= 0f) return;
        elapsedTime = Mathf.Min(gameDuration, elapsedTime + Time.deltaTime);
        if (elapsedTime >= gameDuration && !IsBossEngaged()) EndGame();
    }

    // Boss 已出场且尚未击杀时，让战斗自然打完，不因到达 game.duration 直接判负。
    bool IsBossEngaged()
    {
        if (!requireBossDefeat || spawner == null) return false;
        return spawner.bossSpawned && !spawner.bossDefeated;
    }

    public void StartGame()
    {
        elapsedTime = 0f;
        gameEnded = false;
        victory = false;
        bossDefeated = false;
        enemiesDefeated = 0;
        experienceCollected = 0;
        Time.timeScale = 1f;
    }

    public int CurrentWave => Mathf.Clamp(Mathf.FloorToInt(elapsedTime / 30f) + 1, 1, Mathf.Max(1, Mathf.CeilToInt(gameDuration / 30f)));
    public string GetWaveLabel() { return "波次 " + CurrentWave.ToString("00"); }
    public void RegisterEnemyDefeated(Enemy enemy)
    {
        enemiesDefeated++;
        OnRunStatsChanged?.Invoke();
        if (enemy != null && SkillCodexManager.Instance != null)
        {
            EnemyArchetype archetype = enemy.GetComponent<EnemyArchetype>();
            if (archetype != null) SkillCodexManager.Instance.UnlockEnemy(archetype.kind);
        }
    }
    public void RegisterExperienceCollected(int amount)
    {
        experienceCollected += Mathf.Max(0, amount);
        OnRunStatsChanged?.Invoke();
    }

    public void HandleUpgradeApplied(UpgradeOption option)
    {
        if (skillCodexManager != null) skillCodexManager.UnlockForUpgrade(option);
        if (knowledgeTicker != null) knowledgeTicker.ShowForUpgrade(option);
    }

    public float GetRemaining() { return Mathf.Max(0f, gameDuration - elapsedTime); }

    public void HandleBossDefeated()
    {
        if (gameEnded || !requireBossDefeat) return;
        bossDefeated = true;
        StartCoroutine(BossVictoryRoutine());
    }

    public IEnumerator BossVictoryRoutine()
    {
        // Brief realtime pause so the boss death reads before the result panel.
        yield return new WaitForSecondsRealtime(GameBalance.Get("game.victory_delay", 1.5f));
        // 设定集(3) 节点结算「免费升级次数」⸢页裁⸥→保留现值（无免费升级，默认 0，标旗）。
        int freeChoices = GameBalance.GetInt("game.boss_free_upgrades", 0);
        if (freeChoices > 0 && levelUpManager != null)
        {
            levelUpManager.GrantFreeChoices(freeChoices);
            float guard = 0f;
            while (levelUpManager.isChoosing && guard < 180f)
            {
                yield return new WaitForSecondsRealtime(0.1f);
                guard += 0.1f;
            }
        }
        if (!gameEnded) FinishGame(true);
    }

    public void EndGame()
    {
        EnemySpawner spawner = FindAnyObjectByType<EnemySpawner>();
        bool canAdvance = !requireBossDefeat || spawner == null || !spawner.bossSpawned || spawner.bossDefeated;
        bossDefeated = spawner != null && spawner.bossDefeated;
        FinishGame(canAdvance);
    }
    public void HandleDeath() { FinishGame(false); }

    public void FinishGame(bool won)
    {
        if (gameEnded) return;
        gameEnded = true;
        victory = won;
        // 设定集(3) 节点结算：击败节点 Boss 才解锁下一张地图；Boss 存活通关不解锁。
        if (won) PlayerPrefs.SetInt("VesselGuard.StageCleared_" + selectedLevel, 1);
        Time.timeScale = 0f;
        OnGameEnded?.Invoke(won);
    }

    public void RestartGame()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void ApplyStageTheme(EnemySpawner spawner)
    {
        InfiniteBrickBackground background = FindAnyObjectByType<InfiniteBrickBackground>();
        if (background == null) return;

        // 1/2 关优先用美术无缝瓦片（含装饰变体）；素材缺失时回退程序化砖色。
        if (selectedLevel == 1)
        {
            Sprite[] tiles = InfiniteBrickBackground.LoadTileSet("capillary", 3);
            if (tiles != null)
            {
                background.SetTileTheme(tiles, 92f / 32f, 92f / 32f);
                return;
            }
            background.SetTheme(new Color(0.16f, 0.075f, 0.12f, 1f), new Color(0.035f, 0.018f, 0.035f, 1f));
        }
        else if (selectedLevel == 2)
        {
            Sprite[] tiles = InfiniteBrickBackground.LoadTileSet("vein", 3);
            if (tiles != null)
            {
                background.SetTileTheme(tiles, 3f, 3f);
                return;
            }
            background.SetTheme(new Color(0.035f, 0.11f, 0.2f, 1f), new Color(0.008f, 0.025f, 0.055f, 1f));
        }
        else
        {
            background.SetTheme(new Color(0.2f, 0.045f, 0.055f, 1f), new Color(0.045f, 0.008f, 0.012f, 1f));
        }
    }

    void OnDestroy()
    {
        if (playerStats != null) playerStats.OnDeath -= HandleDeath;
        if (levelUpManager != null) levelUpManager.OnUpgradeApplied -= HandleUpgradeApplied;
        if (Instance == this) { Instance = null; Time.timeScale = 1f; }
    }
}
// skill collection wired
