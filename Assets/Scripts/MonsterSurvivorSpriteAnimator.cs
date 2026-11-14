using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class MonsterSurvivorSpriteAnimator : MonoBehaviour
{
    public SpriteRenderer target;
    public Sprite[] frames;
    public float framesPerSecond = 8f;
    public float frameTimer;
    public int frameIndex;

    // 素材画的是朝左，facing 为 +1 时镜像。默认 -1，敌人和程序化角色都不会被翻。
    public int facing = -1;

    class Clip
    {
        public Sprite[] frames;
        public float fps;
        public bool loop;
    }

    readonly Dictionary<string, Clip> clips = new Dictionary<string, Clip>();
    Clip baseClip;
    Clip active;

    public void Awake()
    {
        if (target == null) target = GetComponent<SpriteRenderer>();
    }

    public void SetFrames(Sprite[] value, float fps)
    {
        SetClip("base", value, fps, true);
    }

    public void SetClip(string name, Sprite[] value, float fps, bool loop)
    {
        if (value == null || value.Length == 0) return;
        Clip clip = new Clip();
        clip.frames = value;
        clip.fps = Mathf.Max(1f, fps);
        clip.loop = loop;
        clips[name] = clip;
        // 只有循环片段才是"常态"，一次性片段等攻击那种事件来点。
        if (loop) Play(name);
    }

    public void Play(string name)
    {
        Clip clip;
        if (!clips.TryGetValue(name, out clip) || clip == active) return;
        active = clip;
        if (clip.loop) baseClip = clip;
        frames = clip.frames;
        framesPerSecond = clip.fps;
        frameIndex = 0;
        frameTimer = 0f;
        if (target == null) target = GetComponent<SpriteRenderer>();
        if (target != null) target.sprite = frames[0];
    }

    // 一次性片段播放期间（攻击）朝向要锁在瞄准方向，不能被走路方向覆盖。
    public bool InOneShot { get { return active != null && !active.loop; } }

    public void Update()
    {
        if (target == null || frames == null || frames.Length < 2 || Time.timeScale <= 0f) return;
        bool flipped = facing > 0;
        if (target.flipX != flipped) target.flipX = flipped;
        frameTimer += Time.deltaTime * framesPerSecond;
        if (frameTimer < 1f) return;
        int step = Mathf.FloorToInt(frameTimer);
        frameTimer -= step;
        if (active != null && !active.loop && frameIndex + step >= frames.Length)
        {
            if (baseClip != null) Play("base");
            else frameIndex = frames.Length - 1;
            target.sprite = frames[frameIndex];
            return;
        }
        frameIndex = (frameIndex + step) % frames.Length;
        target.sprite = frames[frameIndex];
    }
}
