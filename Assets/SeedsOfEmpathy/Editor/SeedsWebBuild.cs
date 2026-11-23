#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace SeedsOfEmpathy.Editor
{
    /// <summary>
    /// Compila la versión web (WebGL) para las pruebas con jugadores.
    /// Menú: Seeds of Empathy > Build WebGL. Deja index.html en Builds/WebGL, listo para comprimir
    /// en un zip y subir a itch.io (Kind of project: HTML).
    /// </summary>
    public static class SeedsWebBuild
    {
        [MenuItem("Seeds of Empathy/Build WebGL")]
        public static void Build()
        {
            var scenes = new System.Collections.Generic.List<string>();
            foreach (var s in EditorBuildSettings.scenes)
                if (s.enabled) scenes.Add(s.path);

            string outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Builds", "WebGL"));
            if (Directory.Exists(outDir)) Directory.Delete(outDir, true);

            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled; // itch.io sirve los ficheros tal cual
            PlayerSettings.WebGL.decompressionFallback = false;
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
            PlayerSettings.WebGL.template = "APPLICATION:Default";

            var opts = new BuildPlayerOptions
            {
                scenes = scenes.ToArray(),
                locationPathName = outDir,
                target = BuildTarget.WebGL,
                options = BuildOptions.None
            };

            var report = BuildPipeline.BuildPlayer(opts);
            if (report.summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"[Seeds] Build WebGL correcto: {outDir} ({report.summary.totalSize / (1024 * 1024)} MB). " +
                          "Comprime el CONTENIDO de la carpeta en un zip (index.html en la raíz) y súbelo a itch.io.");
                EditorUtility.RevealInFinder(outDir);
            }
            else
            {
                Debug.LogError($"[Seeds] Build WebGL fallido: {report.summary.result}");
            }
        }
    }
}
#endif
