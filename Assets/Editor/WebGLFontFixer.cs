using UnityEditor;
using UnityEditor.Build.Reporting;

public class WebGLFontFixer
{
    [MenuItem("VesselGuard/修复 WebGL 字体并构建")]
    public static void FixAndBuild()
    {
        // 切换到 WebGL 平台
        EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WebGL, BuildTarget.WebGL);

        // 打开构建对话框让用户选择位置
        string path = EditorUtility.SaveFolderPanel("选择 WebGL 构建输出位置", "", "WebGLBuild");
        if (string.IsNullOrEmpty(path)) return;

        // 构建 WebGL
        BuildPlayerOptions options = new BuildPlayerOptions();
        options.scenes = GetEnabledScenes();
        options.locationPathName = path;
        options.target = BuildTarget.WebGL;
        options.options = BuildOptions.None;

        BuildReport report = BuildPipeline.BuildPlayer(options);

        if (report.summary.result == BuildResult.Succeeded)
        {
            EditorUtility.DisplayDialog("成功", $"构建成功！\n输出位置：{path}", "确定");
        }
        else
        {
            EditorUtility.DisplayDialog("失败", $"构建失败：{report.summary.result}", "确定");
        }
    }

    private static string[] GetEnabledScenes()
    {
        var scenes = new System.Collections.Generic.List<string>();
        foreach (var scene in EditorBuildSettings.scenes)
        {
            if (scene.enabled)
            {
                scenes.Add(scene.path);
            }
        }
        return scenes.ToArray();
    }
}
