using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Builds the spike scene to an iOS Xcode project: IL2CPP, arm64 only, Metal only.
// Written against the Unity 5.4 editor API (bundleIdentifier, SetPropertyInt for backend/architecture).
public static class SpikeBuild
{
    public static void Build()
    {
        try
        {
            string outDir = Environment.GetEnvironmentVariable("SHELL_OUT");
            if (string.IsNullOrEmpty(outDir)) outDir = Path.GetFullPath("../xcode");

            Directory.CreateDirectory("Assets/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            new GameObject("Boot").AddComponent<SpikeBoot>();
            EditorSceneManager.SaveScene(scene, "Assets/Scenes/Main.unity");

            PlayerSettings.companyName = "rmvr";
            PlayerSettings.productName = "RMVR Spike";
            PlayerSettings.bundleIdentifier = "com.rmvr.spike";
            PlayerSettings.SetPropertyInt("ScriptingBackend", 1, BuildTargetGroup.iOS); // 1 = IL2CPP
            PlayerSettings.SetPropertyInt("Architecture", 1, BuildTargetGroup.iOS);     // 1 = arm64
            PlayerSettings.iOS.sdkVersion = iOSSdkVersion.DeviceSDK;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.iOS, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.iOS, new[] { UnityEngine.Rendering.GraphicsDeviceType.Metal });
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.statusBarHidden = true;

            Debug.Log("[RMVR] building iOS project to " + outDir);
            string err = BuildPipeline.BuildPlayer(new[] { "Assets/Scenes/Main.unity" }, outDir, BuildTarget.iOS, BuildOptions.None);
            if (!string.IsNullOrEmpty(err))
            {
                Debug.LogError("[RMVR] BUILD FAILED: " + err);
                EditorApplication.Exit(1);
                return;
            }
            Debug.Log("[RMVR] BUILD OK");
            EditorApplication.Exit(0);
        }
        catch (Exception e)
        {
            Debug.LogError("[RMVR] BUILD EXCEPTION: " + e);
            EditorApplication.Exit(1);
        }
    }
}
