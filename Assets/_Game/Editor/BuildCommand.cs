using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IdleHeroDefense.Editor
{
    public static class BuildCommand
    {
        private const string BootstrapScene = "Assets/_Game/Scenes/Bootstrap.unity";

        [MenuItem("Idle Hero Defense/Build/Android")]
        public static void BuildAndroid() => Build(BuildTarget.Android,
            Environment.GetEnvironmentVariable("OUTPUT_PATH") ?? "Builds/Android/IdleHeroDefense.apk");

        [MenuItem("Idle Hero Defense/Build/Windows")]
        public static void BuildWindows() => Build(BuildTarget.StandaloneWindows64,
            Environment.GetEnvironmentVariable("OUTPUT_PATH") ?? "Builds/Windows/IdleHeroDefense.exe");

        public static void Build(BuildTarget target, string outputPath)
        {
            ProjectValidator.ValidateOrThrow();
            EnsureBootstrapScene();
            ApplyVersion();
            var fullOutput = Path.GetFullPath(outputPath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullOutput) ?? throw new InvalidOperationException("Invalid output path."));
            var options = new BuildPlayerOptions
            {
                scenes = new[] { BootstrapScene },
                locationPathName = fullOutput,
                target = target,
                options = BuildOptions.CleanBuildCache
            };
            var report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException($"Build failed: {report.summary.result}, {report.summary.totalErrors} error(s).");
            Debug.Log($"Build succeeded: {fullOutput} ({report.summary.totalSize} bytes)");
        }

        public static void EnsureBootstrapScene()
        {
            if (File.Exists(BootstrapScene)) return;
            Directory.CreateDirectory(Path.GetDirectoryName(BootstrapScene) ?? "Assets/_Game/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "Bootstrap";
            EditorSceneManager.SaveScene(scene, BootstrapScene);
            AssetDatabase.Refresh();
        }

        private static void ApplyVersion()
        {
            var version = Environment.GetEnvironmentVariable("GAME_VERSION");
            if (!string.IsNullOrWhiteSpace(version)) PlayerSettings.bundleVersion = version;
            var buildNumber = Environment.GetEnvironmentVariable("BUILD_NUMBER");
            if (int.TryParse(buildNumber, out var number) && number > 0)
            {
                PlayerSettings.Android.bundleVersionCode = number;
                PlayerSettings.iOS.buildNumber = number.ToString();
            }
            PlayerSettings.productName = "Idle Hero Defense";
            PlayerSettings.companyName = "Idle Hero Defense Studio";
        }
    }
}

