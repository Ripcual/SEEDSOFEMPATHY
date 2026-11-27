#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace SeedsOfEmpathy.Editor
{
    /// <summary>
    /// Compila la versión alfa para Windows 64 bits y la comprime en un zip listo para subir a GitHub.
    /// Menú: Seeds of Empathy > Build Windows. Resultado: Builds/SeedsOfEmpathy_alpha_win64.zip
    /// </summary>
    public static class SeedsWindowsBuild
    {
        [MenuItem("Seeds of Empathy/Build Windows")]
        public static void Build()
        {
            var scenes = new List<string>();
            foreach (var s in EditorBuildSettings.scenes)
                if (s.enabled) scenes.Add(s.path);

            string root = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Builds"));
            string outDir = Path.Combine(root, "Windows");
            string zipPath = Path.Combine(root, "SeedsOfEmpathy_alpha_win64.zip");
            if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
            if (File.Exists(zipPath)) File.Delete(zipPath);
            Directory.CreateDirectory(outDir);

            var opts = new BuildPlayerOptions
            {
                scenes = scenes.ToArray(),
                locationPathName = Path.Combine(outDir, "SeedsOfEmpathy.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };

            var report = BuildPipeline.BuildPlayer(opts);
            if (report.summary.result != BuildResult.Succeeded)
            {
                Debug.LogError($"[Seeds] Build Windows fallido: {report.summary.result}");
                return;
            }

            ZipFile.CreateFromDirectory(outDir, zipPath, System.IO.Compression.CompressionLevel.Optimal, false);
            Debug.Log($"[Seeds] Build Windows correcto. Zip listo: {zipPath}");
            EditorUtility.RevealInFinder(zipPath);
        }
    }
}
#endif
