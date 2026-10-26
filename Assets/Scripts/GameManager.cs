using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// Distinct gameplay states. Once a run leaves Running it can never return, so the
// first legal terminal condition (player death or boss defeat) permanently locks
// the outcome and later events cannot flip it.
public enum GameState { Running, VictoryPending, Won, Lost }

// Every popup that freezes play claims its own reason bit. Time only resumes when
// all reasons are cleared, so an auto-closing reveal can never release a menu pause.
[Flags]
public enum PauseReason { None = 0, Menu = 1, CardChoice = 2, CardReveal = 4, Codex = 8 }

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;
    public float gameDuration = 300f;
    public float elapsedTime;
    public bool gameEnded;
    public bool victory;
    public GameState state = GameState.Running;
    // Centralized pause/speed control. baseTimeScale is 1 (normal) or the test
    // multiplier (x6); any active pause reason forces the scale to 0.
    PauseReason activePauses = PauseReason.None;
    public float baseTimeScale = 1f;
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
        state = GameState.Running;
        activePauses = PauseReason.None;
        baseTimeScale = 1f;
        RecomputeTimeScale();
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
        EnemySpawner spawner = FindAnyObjectByType<EnemySpawner>();
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
        if (Input.GetKeyDown(KeyCode.F8)) GameBalance.Reload();
        if (gameEnded || Time.timeScale <= 0f) return;
        elapsedTime = Mathf.Min(gameDuration, elapsedTime + Time.deltaTime);
        if (elapsedTime >= gameDuration) EndGame();
    }

    public void StartGame()
    {
        elapsedTime = 0f;
        gameEnded = false;
        victory = false;
        state = GameState.Running;
        bossDefeated = false;
        enemiesDefeated = 0;
        experienceCollected = 0;
        activePauses = PauseReason.None;
        baseTimeScale = 1f;
        RecomputeTimeScale();
    }

    // Single entry point for both pausing and the normal/x6 running speed.
    public void SetPause(PauseReason reason, bool enabled)
    {
        if (enabled) activePauses |= reason;
        else activePauses &= ~reason;
        RecomputeTimeScale();
    }

    public bool IsPaused(PauseReason reason) { return (activePauses & reason) != 0; }
    public bool AnyPauseActive { get { return activePauses != PauseReason.None; } }

    public void SetBaseSpeed(float scale)
    {
        baseTimeScale = Mathf.Max(0.0001f, scale);
        RecomputeTimeScale();
    }

    // Scene switch or restart: drop every held reason so nothing leaks into the
    // next run. The run being torn down may already be over, so the clock is
    // normalised explicitly rather than through RecomputeTimeScale.
    public void ClearAllPauses()
    {
        activePauses = PauseReason.None;
        Time.timeScale = baseTimeScale;
    }

    // A run that has left Running stays frozen; otherwise paused freezes and the
    // running speed applies only when no popup holds a reason.
    void RecomputeTimeScale()
    {
        if (gameEnded) { Time.timeScale = 0f; return; }
        Time.timeScale = activePauses != PauseReason.None ? 0f : baseTimeScale;
    }

    public int CurrentWave => Mathf.Clamp(Mathf.FloorToInt(elapsedTime / 30f) + 1, 1, Mathf.Max(1, Mathf.CeilToInt(gameDuration / 30f)));
    public string GetWaveLabel() { return "波次 " + CurrentWave.ToString("00"); }
    public void RegisterEnemyDefeated(Enemy enemy)
    {
        enemiesDefeated++;
        OnRunStatsChanged?.Invoke();
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
        if (!requireBossDefeat) return;
        // Only a living-player boss kill from the Running state locks victory. The
        // first terminal condition wins, so this is ignored if the run already ended.
        if (state != GameState.Running) return;
        bossDefeated = true;
        state = GameState.VictoryPending;
        gameEnded = true; // freeze timer, damage, attacks, spawning, XP and level-ups at once
        RecomputeTimeScale();
        StartCoroutine(BossVictoryRoutine());
    }

    public IEnumerator BossVictoryRoutine()
    {
        // Brief realtime delay so the boss death reads before the result panel. It
        // runs on unscaled time, so it still elapses while gameplay is frozen.
        yield return new WaitForSecondsRealtime(GameBalance.Get("game.victory_delay", 1.5f));
        if (state == GameState.VictoryPending) FinishGame(true);
    }

    public void EndGame()
    {
        if (state != GameState.Running) return;
        EnemySpawner spawner = FindAnyObjectByType<EnemySpawner>();
        // Timeout only counts as a win when the boss actually spawned and was beaten;
        // a missing spawner or an unspawned boss can never grant an automatic victory.
        bool bossConfirmed = spawner != null && spawner.bossSpawned && spawner.bossDefeated;
        bool canAdvance = !requireBossDefeat || bossConfirmed;
        bossDefeated = bossConfirmed;
        FinishGame(canAdvance);
    }
    public void HandleDeath()
    {
        // Player death only ends the run while it is still live. If the boss was
        // already defeated (VictoryPending) the outcome is locked to a win.
        if (state != GameState.Running) return;
        FinishGame(false);
    }

    public void FinishGame(bool won)
    {
        // Result fires exactly once; a settled Won/Lost can never be re-entered.
        if (state == GameState.Won || state == GameState.Lost) return;
        state = won ? GameState.Won : GameState.Lost;
        gameEnded = true;
        victory = won;
        RecomputeTimeScale();
        OnGameEnded?.Invoke(won);
    }

    public void RestartGame()
    {
        // Reset the speed baseline too, then clear through the shared entry so the
        // reloaded scene starts unpaused at normal speed with no stale reasons.
        baseTimeScale = 1f;
        ClearAllPauses();
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void ApplyStageTheme(EnemySpawner spawner)
    {
        InfiniteBrickBackground background = FindAnyObjectByType<InfiniteBrickBackground>();
        if (background == null) return;

        if (selectedLevel == 1)
        {
            background.brickColor = new Color(0.16f, 0.075f, 0.12f, 1f);
            background.mortarColor = new Color(0.035f, 0.018f, 0.035f, 1f);
        }
        else if (selectedLevel == 2)
        {
            background.brickColor = new Color(0.035f, 0.11f, 0.2f, 1f);
            background.mortarColor = new Color(0.008f, 0.025f, 0.055f, 1f);
        }
        else
        {
            background.brickColor = new Color(0.2f, 0.045f, 0.055f, 1f);
            background.mortarColor = new Color(0.045f, 0.008f, 0.012f, 1f);
        }
        background.BuildField();
    }

    void OnDestroy()
    {
        if (playerStats != null) playerStats.OnDeath -= HandleDeath;
        if (levelUpManager != null) levelUpManager.OnUpgradeApplied -= HandleUpgradeApplied;
        // Clear the static handle BEFORE releasing, so anything still alive sees a
        // null Instance instead of a half-destroyed one, and any component that
        // releases a pause afterwards cannot reach a dead object.
        if (Instance == this)
        {
            activePauses = PauseReason.None;
            Instance = null;
            Time.timeScale = 1f;
        }
    }
}
// skill collection wired
