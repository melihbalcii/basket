using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// WebGL derlemesi: Unity menüsünden (Build > WebGL) veya komut satırından
/// (-batchmode -executeMethod BuildWebGL.Build) çalışır. Çıktı: Builds/WebGL.
/// Gzip + çözme yedeği (decompression fallback) sayesinde HERHANGİ bir statik
/// sunucuda (Netlify, itch.io, GitHub Pages) özel ayar gerektirmeden çalışır.
/// </summary>
public static class BuildWebGL
{
    [MenuItem("Build/WebGL Build")]
    public static void Build()
    {
        PlayerSettings.companyName = "Vinyl League";
        PlayerSettings.productName = "Vinyl League";
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
        PlayerSettings.WebGL.decompressionFallback = true; // sunucu başlığı gerektirmez
        PlayerSettings.runInBackground = true;

        var opts = new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Scenes/SampleScene.unity" },
            locationPathName = "Builds/WebGL",
            target = BuildTarget.WebGL,
            options = BuildOptions.None,
        };

        var report = BuildPipeline.BuildPlayer(opts);
        Debug.Log($"WebGL derleme sonucu: {report.summary.result}  boyut: {report.summary.totalSize / (1024 * 1024)} MB  süre: {report.summary.totalTime.TotalMinutes:F1} dk");
        if (report.summary.result != BuildResult.Succeeded)
            EditorApplication.Exit(1);
    }
}
