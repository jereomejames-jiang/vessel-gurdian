using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

// 修复 WebGL Publisher（com.unity.connect.share）的一个缺陷：
// 它用分号 ';' 拼接历史构建路径，若构建文件夹名本身含分号（例如 "buil;df"），
// 拆分后的条目数会超过上限 10，导致打开发布窗口时抛 ArgumentOutOfRangeException。
// 这里在编辑器加载时检测并清空损坏的历史记录，使发布窗口能正常打开。
[InitializeOnLoad]
public static class FixWebGLPublisher
{
    const string Key = "buildOutputDirList";
    const int MaxDisplayedBuilds = 10;

    static FixWebGLPublisher()
    {
        EditorApplication.delayCall += Sanitize;
    }

    static void Sanitize()
    {
        try
        {
            Type utils = null;
            foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                utils = asm.GetType("Unity.Play.Publisher.Editor.PublisherUtils");
                if (utils != null) break;
            }
            if (utils == null) return;

            MethodInfo get = utils.GetMethod("GetEditorPreference", BindingFlags.Public | BindingFlags.Static);
            MethodInfo set = utils.GetMethod("SetEditorPreference", BindingFlags.Public | BindingFlags.Static);
            if (get == null || set == null) return;

            string value = get.Invoke(null, new object[] { Key }) as string;
            if (string.IsNullOrEmpty(value)) return;

            string[] parts = value.Split(';');
            if (parts.Length <= MaxDisplayedBuilds) return;

            set.Invoke(null, new object[] { Key, string.Empty });
            Debug.Log("[FixWebGLPublisher] 检测到损坏的发布构建历史（含分号的路径），已清空。请改用不含分号的构建文件夹后重新发布。");
        }
        catch (Exception e)
        {
            Debug.LogWarning("[FixWebGLPublisher] 自动修复失败：" + e.Message);
        }
    }
}