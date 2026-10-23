using System;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

// Builds the flat (non-VR) test scene to an iOS Xcode project with Unity 2020.3: IL2CPP, arm64, Metal, no XR.
public static class FlatBuild
{
    public static void Build()
    {
        try
        {
            string outDir = Environment.GetEnvironmentVariable("SHELL_OUT");
            if (string.IsNullOrEmpty(outDir)) outDir = System.IO.Path.GetFullPath("../xcode");
            System.IO.Directory.CreateDirectory("Assets/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            new GameObject("Boot").AddComponent<FlatBoot>();
            EditorSceneManager.SaveScene(scene, "Assets/Scenes/Main.unity");

            PlayerSettings.companyName = "rmvr";
            PlayerSettings.productName = "Virtual Rick-ality Flat";
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.iOS, "com.rmvr.flat");
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.iOS, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetArchitecture(BuildTargetGroup.iOS, 1);
            PlayerSettings.iOS.sdkVersion = iOSSdkVersion.DeviceSDK;
            PlayerSettings.iOS.targetOSVersionString = "12.0";
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.iOS, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.iOS, new[] { UnityEngine.Rendering.GraphicsDeviceType.Metal });
            PlayerSettings.defaultInterfaceOrientation = UIDefaultOrientation.LandscapeLeft;
            PlayerSettings.statusBarHidden = true;

            Debug.Log("[RMVR] building iOS project to " + outDir);
            var r = BuildPipeline.BuildPlayer(new[] { "Assets/Scenes/Main.unity" }, outDir, BuildTarget.iOS, BuildOptions.None);
            Debug.Log("[RMVR] result " + r.summary.result + " errors=" + r.summary.totalErrors);
            EditorApplication.Exit(r.summary.result == BuildResult.Succeeded ? 0 : 1);
        }
        catch (Exception e)
        {
            Debug.LogError("[RMVR] BUILD EXCEPTION: " + e);
            EditorApplication.Exit(1);
        }
    }
}
