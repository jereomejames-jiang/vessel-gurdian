using UnityEngine;

[DisallowMultipleComponent]
public class MonsterSurvivorSpriteAnimator : MonoBehaviour
{
    public SpriteRenderer target;
    public Sprite[] frames;
    public float framesPerSecond = 8f;
    public float frameTimer;
    public int frameIndex;

    public void Awake()
    {
        if (target == null) target = GetComponent<SpriteRenderer>();
    }

    public void SetFrames(Sprite[] value, float fps)
    {
        frames = value;
        framesPerSecond = Mathf.Max(1f, fps);
        frameIndex = 0;
        frameTimer = 0f;
        if (target == null) target = GetComponent<SpriteRenderer>();
        if (target != null && frames != null && frames.Length > 0) target.sprite = frames[0];
    }

    public void Update()
    {
        if (target == null || frames == null || frames.Length < 2 || Time.timeScale <= 0f) return;
        frameTimer += Time.deltaTime * framesPerSecond;
        if (frameTimer < 1f) return;
        int step = Mathf.FloorToInt(frameTimer);
        frameTimer -= step;
        frameIndex = (frameIndex + step) % frames.Length;
        target.sprite = frames[frameIndex];
    }
}

