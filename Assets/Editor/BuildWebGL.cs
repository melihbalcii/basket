using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// WebGL derlemesi: Unity menüsünden (Build > WebGL) veya komut satırından
/// (-batchmode -executeMethod BuildWebGL.Build) çalışır. Çıktı: Builds/WebGL.
/// Gzip + çözme yedeği (decompression fallback) sayesinde HERHANGİ bir statik
/// sunucuda (Netlify, itch.io, GitHub Pages) özel ayar gerektirmeden çalışır.
/// Build sonrası PWA katmanı eklenir (WebGLExtras/): manifest + service worker +
/// ikonlar kopyalanır ve index.html'e etiketler enjekte edilir - oyun telefona
/// "Ana Ekrana Ekle" ile gerçek uygulama gibi kurulabilir.
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
        {
            EditorApplication.Exit(1);
            return;
        }

        try
        {
            AddPwaLayer("Builds/WebGL");
            Debug.Log("PWA katmanı eklendi (manifest + sw + ikonlar + index enjeksiyonu).");
        }
        catch (Exception ex)
        {
            // PWA katmanı kritik değil: eklenemezse build yine geçerli, sadece uyar.
            Debug.LogWarning("PWA katmanı eklenemedi: " + ex.Message);
        }
    }

    /// <summary>WebGLExtras içeriğini build çıktısına kopyalar ve index.html'i yamalar.</summary>
    static void AddPwaLayer(string outDir)
    {
        const string extras = "WebGLExtras";
        if (!Directory.Exists(extras)) { Debug.LogWarning("WebGLExtras yok; PWA atlandı."); return; }

        // 1) manifest + ikonlar birebir kopya
        File.Copy(Path.Combine(extras, "manifest.webmanifest"), Path.Combine(outDir, "manifest.webmanifest"), true);
        string iconsSrc = Path.Combine(extras, "icons");
        string iconsDst = Path.Combine(outDir, "icons");
        Directory.CreateDirectory(iconsDst);
        foreach (var f in Directory.GetFiles(iconsSrc, "*.png"))
            File.Copy(f, Path.Combine(iconsDst, Path.GetFileName(f)), true);

        // 2) service worker: sürüm damgası bas (yeni build -> yeni önbellek -> bayat oyun yok)
        string ver = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        string sw = File.ReadAllText(Path.Combine(extras, "sw.js")).Replace("__BUILDVER__", ver);
        File.WriteAllText(Path.Combine(outDir, "sw.js"), sw);

        // 3) index.html enjeksiyonu (tekrar çalıştırılabilir: işaret varsa dokunma)
        string indexPath = Path.Combine(outDir, "index.html");
        string html = File.ReadAllText(indexPath);
        const string marker = "<!-- vinyl-pwa -->";
        if (!html.Contains(marker))
        {
            string inject = marker + "\n"
                + "    <link rel=\"manifest\" href=\"manifest.webmanifest\">\n"
                + "    <meta name=\"theme-color\" content=\"#33435C\">\n"
                + "    <meta name=\"apple-mobile-web-app-capable\" content=\"yes\">\n"
                + "    <meta name=\"mobile-web-app-capable\" content=\"yes\">\n"
                + "    <meta name=\"apple-mobile-web-app-status-bar-style\" content=\"black-translucent\">\n"
                + "    <meta name=\"apple-mobile-web-app-title\" content=\"Vinyl League\">\n"
                + "    <link rel=\"apple-touch-icon\" href=\"icons/icon-180.png\">\n"
                + "    <script>\n"
                + "    // SW kaydı + OTOMATİK GÜNCELLEME: build dosya adları sabit olduğundan, yeni\n"
                + "    // sürüm SW'yi devraldığı an sayfa BİR KEZ yenilenir (oyuncu eski önbellekte kalmaz).\n"
                + "    // İlk ziyarette (önceki denetleyici yokken) yenileme YAPILMAZ - çifte indirme olmasın.\n"
                + "    if ('serviceWorker' in navigator) {\n"
                + "      addEventListener('load', () => navigator.serviceWorker.register('sw.js'));\n"
                + "      let vlHadSw = !!navigator.serviceWorker.controller, vlReloaded = false;\n"
                + "      navigator.serviceWorker.addEventListener('controllerchange', () => {\n"
                + "        if (vlHadSw && !vlReloaded) { vlReloaded = true; location.reload(); }\n"
                + "        vlHadSw = true;\n"
                + "      });\n"
                + "    }\n"
                + "    </script>\n";
            html = html.Replace("</head>", inject + "  </head>");
            File.WriteAllText(indexPath, html);
        }
    }
}
