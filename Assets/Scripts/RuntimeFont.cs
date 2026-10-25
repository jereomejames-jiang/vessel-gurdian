using UnityEngine;

public static class RuntimeFont
{
    static Font cached;

    public static Font Get()
    {
        if (cached == null) cached = Resources.Load<Font>("simhei");
        if (cached == null) Debug.LogError("RuntimeFont: Resources/simhei.ttf 未找到");
        return cached;
    }
}
