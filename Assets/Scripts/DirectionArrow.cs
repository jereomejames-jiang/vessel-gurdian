using UnityEngine;

[DefaultExecutionOrder(110)]
public class DirectionArrow : MonoBehaviour
{
    public Transform target;
    public Vector3 worldOffset = new Vector3(0f, -0.82f, -0.1f);
    // Retained for compatibility with existing rebuild scripts.
    public float followSmooth = 24f;
    public PlayerController playerController;

    SpriteRenderer sweep;
    SpriteRenderer shadow;
    float halfHeight;
    bool sized;
    static readonly Color Accent = new Color(0.35f, 0.9f, 1f, 1f);
    // 扫弧贴图按旧半身高 0.825 设计，随实际半身高缩放以保持"刚好不挡身体"。
    const float DesignHalfHeight = 0.825f;

    void Awake()
    {
        // 旧的实心黄楔形和侧视像素角色不搭，换成贴地阴影+绕身罗盘扫弧。
        SpriteRenderer legacy = GetComponent<SpriteRenderer>();
        if (legacy != null) legacy.enabled = false;

        shadow = NewChild("接触阴影", MonsterSurvivorArtLibrary.GroundShadow(), 1);
        shadow.color = new Color(0f, 0f, 0f, 0.35f);

        // 扫弧绕角色中心（脚底+半身高）旋转，半径大于半身高，任何朝向都不被身体挡住。
        sweep = NewChild("朝向弧", MonsterSurvivorArtLibrary.HeadingSweep(), 1);
        sweep.color = Accent;
    }

    SpriteRenderer NewChild(string name, Sprite sprite, int order)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(transform, false);
        SpriteRenderer renderer = obj.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = order;
        return renderer;
    }

    void LateUpdate() { UpdatePose(); }

    public void UpdatePose()
    {
        if (target == null)
        {
            playerController = FindAnyObjectByType<PlayerController>();
            if (playerController == null) return;
            target = playerController.transform;
        }
        if (!sized)
        {
            SpriteRenderer visual = target.GetComponentInChildren<SpriteRenderer>();
            if (visual != null && visual.sprite != null)
            {
                halfHeight = visual.bounds.size.y * 0.5f;
                sized = true;
            }
        }
        float hh = sized ? halfHeight : 0.82f;
        float k = hh / DesignHalfHeight;
        // No second smoothing pass: stay exactly beneath the rendered player.
        transform.position = target.position + new Vector3(worldOffset.x, -hh, worldOffset.z);
        if (shadow != null) shadow.transform.localScale = new Vector3(k, 0.4f * k, 1f);
        if (sweep == null) return;
        sweep.transform.localPosition = new Vector3(0f, hh, 0f);
        sweep.transform.localScale = Vector3.one * k;
        // 弧画的是指向 +X，heading 以 +X 为 0 度，直接转 heading 即可。
        float heading = playerController != null ? playerController.headingAngle : 90f;
        sweep.transform.localRotation = Quaternion.Euler(0f, 0f, heading);
        // 低幅度呼吸，让罗盘有一点活气但不闪。
        float pulse = 0.45f + 0.10f * Mathf.Sin(Time.time * 2f);
        sweep.color = new Color(Accent.r, Accent.g, Accent.b, pulse);
    }
}