using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// UGUI 手写插值动效令牌（零依赖）。全部 unscaled 时间：暂停/选卡时 timeScale=0 也要能播。
// 规则：每个挂载点持有自己的 Coroutine 句柄，重入先 StopCoroutine 再启动；
// 模态关闭直接 SetActive(false) 并复位 scale/alpha，不做退出动画。
public static class UIMotion
{
    public const float ScrimIn = 0.16f;
    public const float ModalInDur = 0.22f;
    public const float ModalInVictory = 0.25f;
    public const float StaggerStep = 0.06f;
    public const float CardIn = 0.24f;
    public const float SelectDur = 0.15f;

    public static float EaseOutCubic(float t) { t = Mathf.Clamp01(t); return 1f - Mathf.Pow(1f - t, 3f); }
    public static float EaseOutQuad(float t) { t = Mathf.Clamp01(t); return 1f - Mathf.Pow(1f - t, 2f); }
    public static float EaseInQuad(float t) { t = Mathf.Clamp01(t); return t * t; }

    public static IEnumerator Fade(CanvasGroup g, float from, float to, float dur, System.Func<float, float> ease)
    {
        if (g == null) yield break;
        if (ease == null) ease = EaseOutCubic;
        g.alpha = from;
        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            g.alpha = Mathf.Lerp(from, to, ease(Mathf.Clamp01(t / dur)));
            yield return null;
        }
        g.alpha = to;
    }

    // 暗场 Image 的 alpha 渐入（scrim 本身不带 CanvasGroup 时用）。
    public static IEnumerator FadeImage(Image img, float from, float to, float dur)
    {
        if (img == null) yield break;
        Color c = img.color;
        c.a = from;
        img.color = c;
        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            c.a = Mathf.Lerp(from, to, EaseOutCubic(Mathf.Clamp01(t / dur)));
            img.color = c;
            yield return null;
        }
        c.a = to;
        img.color = c;
    }

    public static IEnumerator ModalIn(RectTransform rt, CanvasGroup g, float dur, float rise)
    {
        return ModalIn(rt, g, dur, rise, 0.96f);
    }

    public static IEnumerator ModalIn(RectTransform rt, CanvasGroup g, float dur, float rise, float fromScale)
    {
        if (rt == null) yield break;
        float t = 0f;
        Vector2 end = rt.anchoredPosition;
        Vector2 start = end + new Vector2(0f, rise);
        rt.anchoredPosition = start;
        rt.localScale = Vector3.one * fromScale;
        if (g != null) g.alpha = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float k = EaseOutCubic(Mathf.Clamp01(t / dur));
            rt.anchoredPosition = Vector2.LerpUnclamped(start, end, k);
            rt.localScale = Vector3.one * Mathf.LerpUnclamped(fromScale, 1f, k);
            if (g != null) g.alpha = k;
            yield return null;
        }
        rt.anchoredPosition = end;
        rt.localScale = Vector3.one;
        if (g != null) g.alpha = 1f;
    }

    // T_Select：选中框色插值（只改 color，不重生成贴图）。
    public static IEnumerator Tint(Image img, Color from, Color to, float dur)
    {
        if (img == null) yield break;
        float t = 0f;
        img.color = from;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            img.color = Color.Lerp(from, to, EaseOutQuad(Mathf.Clamp01(t / dur)));
            yield return null;
        }
        img.color = to;
    }

    public static void Reset(RectTransform rt, CanvasGroup g)
    {
        if (rt != null) rt.localScale = Vector3.one;
        if (g != null) g.alpha = 1f;
    }

    public static CanvasGroup EnsureGroup(GameObject go)
    {
        if (go == null) return null;
        CanvasGroup g = go.GetComponent<CanvasGroup>();
        if (g == null) g = go.AddComponent<CanvasGroup>();
        return g;
    }
}