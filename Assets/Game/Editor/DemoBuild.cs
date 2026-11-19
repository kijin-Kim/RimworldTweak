using System;
using System.IO;
using RimworldTweak.Demo;
using RimworldTweak.Presentation;
using RimworldTweak.Runtime;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace RimworldTweak.Editor
{
    public static class DemoBuild
    {
        public const string ScenePath = "Assets/Game/Scenes/SimulationSandbox.unity";

        public static void PrepareScene()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = new GameObject("Camera").AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.035f, 0.05f, 0.07f);
            camera.orthographic = true;
            camera.cullingMask = ~(1 << WorldMapView.MapLayer);
            camera.transform.position = new Vector3(0, 0, -10);
            var runner = new GameObject("Simulation").AddComponent<SimulationRunner>();
            var view = new GameObject("Settlement").AddComponent<SettlementView>();
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            var bootstrap = new GameObject("Demo").AddComponent<DemoBootstrap>();
            bootstrap.Runner = runner;
            bootstrap.View = view;
            bootstrap.Visuals = AssetDatabase.LoadAssetAtPath<VisualCatalog>(VisualCatalog.DefaultAssetPath);
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            PlayerSettings.companyName = "RimworldTweak";
            PlayerSettings.productName = "RimworldTweak";
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 800;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.runInBackground = true;
            AssetDatabase.SaveAssets();
            Debug.Log("DEMO_SCENE_PREPARED " + ScenePath);
        }

        public static void BuildWindows()
        {
            BuildWindowsAt("Builds/Windows", BuildOptions.None);
        }

        // GC allocation counters require an instrumented Player on the Mono backend.
        public static void BuildWorkProfile()
        {
            BuildWindowsAt("TestResults/WorkProfilePlayer", BuildOptions.Development);
        }

        private static void BuildWindowsAt(string directory, BuildOptions options)
        {
            Directory.CreateDirectory(directory);
            var result = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { ScenePath },
                locationPathName = Path.Combine(directory, "RimworldTweak.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = options
            });
            File.WriteAllText(Path.Combine(directory,"build-summary.json"), JsonUtility.ToJson(new BuildSummary {
                result = result.summary.result.ToString(), errors = result.summary.totalErrors,
                warnings = result.summary.totalWarnings, size = result.summary.totalSize,
                seconds = result.summary.totalTime.TotalSeconds
            }, true));
            if (result.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("Windows build failed: " + result.summary.result);
        }

        [Serializable]
        private sealed class BuildSummary
        {
            public string result;
            public int errors, warnings;
            public ulong size;
            public double seconds;
        }
    }
}

