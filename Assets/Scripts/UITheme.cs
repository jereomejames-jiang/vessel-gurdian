using UnityEngine;
using UnityEngine.UI;

// VesselGuard 共享设计令牌：暖炭主导 + 琥珀/杏温暖 accent + 临床青理性 accent + muted 语义色。
// 取代各界面散落的 new Color(...) 与旧 Ark* 单青常量。纯静态、无 MonoBehaviour、无依赖。
public static class UITheme
{
    // ── 主导深色族（暖炭）──
    public static readonly Color Deep          = Hex("#161210");        // 页底 / 进度条轨道
    public static readonly Color Surface       = HexA("#221B16", 0.97f);// 卡体 / 面板体 / 未选中 tile
    public static readonly Color SurfaceRaised = Hex("#2C241D");        // 标题带 / hover / 选中体
    public static readonly Color Overlay       = HexA("#322A22", 0.98f);// 模态卡体
    public static readonly Color Scrim         = HexA("#0A0806", 0.80f);// 全屏暗场

    // ── 暖 accent ──
    public static readonly Color WarmAccent = Hex("#D9A05B"); // 琥珀：主 CTA / 选中框 / 编号牌 / 顶带
    public static readonly Color WarmSoft   = Hex("#E7C795"); // 杏色：暖文字强调 / 副标题 / 鼓励语
    public static readonly Color WarmHover  = Hex("#E4B273");
    public static readonly Color WarmPress  = Hex("#B87F3F");

    // ── 临床青 accent（muted）──
    public static readonly Color Clinical = Hex("#7FB5AD");

    // ── 语义（muted）──
    public static readonly Color Success = Hex("#86A96B");
    public static readonly Color Warning = Hex("#C9873E");
    public static readonly Color Danger  = Hex("#C4574E");
    public static readonly Color Info    = Clinical;

    // ── 中性 ──
    public static readonly Color TextPrimary = Hex("#F1E8DB");
    public static readonly Color TextMuted   = Hex("#BFB2A0");
    public static readonly Color LineCool    = HexA("#7A8894", 0.55f);
    public static readonly Color InkOnWarm   = Hex("#241B10");

    // ── 角色 muted 色（医生/护士/药剂师）──
    public static readonly Color[] Role = { Clinical, Hex("#C88A94"), Success };

    // 图鉴高饱和 cardColor 的统一降饱和公式（禁止直接铺原色）。
    // R2-01：压向暖炭深色再整体降亮，保证 tile 上 TextPrimary 对比 ≥7:1（旧 0.5 lerp 仍为中明度）。
    public static Color Mute(Color c)
    {
        Color m = Color.Lerp(c, Hex("#241C15"), 0.70f);
        m = new Color(m.r * 0.9f, m.g * 0.9f, m.b * 0.9f, 1f);
        return m;
    }

    // ── 字号层级（参考 px）──
    public const int Display = 62, Title = 30, CardTitle = 26, Subtitle = 22,
                     Body = 18, Value = 20, Label = 15, Tag = 14;

    // rich text 用的不带 alpha hex（<color=#RRGGBB>）。
    public static string HexStr(Color c) { return "#" + ColorUtility.ToHtmlStringRGB(c); }
    public static string Wrap(string text, Color c) { return "<color=" + HexStr(c) + ">" + text + "</color>"; }
    public static string Wrap(string text, Color c, int size) { return "<size=" + size + "><color=" + HexStr(c) + ">" + text + "</color></size>"; }

    public static Color Hex(string h)
    {
        Color c;
        ColorUtility.TryParseHtmlString(h, out c);
        return c;
    }
    public static Color HexA(string h, float a) { Color c = Hex(h); c.a = a; return c; }

    // 统一 Button 悬停/按压过渡时长（T_Hover / T_Press）。
    public static void ApplyFade(Button b)
    {
        if (b == null) return;
        ColorBlock cb = b.colors;
        cb.fadeDuration = 0.12f;
        b.colors = cb;
    }
}