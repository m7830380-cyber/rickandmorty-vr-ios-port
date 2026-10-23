using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using UnityEngine;

// Builds the game's own scenes to an iOS Xcode project with Unity 2020.3: IL2CPP, arm64, Metal, no VR,
// landscape. Run with -executeMethod FlatBuild.Build; SHELL_OUT is the output folder.
public static class FlatBuild
{
    public static void Build()
    {
        try
        {
            string outDir = Environment.GetEnvironmentVariable("SHELL_OUT");
            if (string.IsNullOrEmpty(outDir)) outDir = Path.GetFullPath("../xcode");

            string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            Debug.Log("[RMVR] scenes: " + string.Join(", ", scenes.Select(Path.GetFileNameWithoutExtension)));

            PlayerSettings.companyName = "rmvr";
            PlayerSettings.productName = "Virtual Rick-ality";
            PlayerSettings.bundleVersion = "0.1." + (Environment.GetEnvironmentVariable("GITHUB_RUN_NUMBER") ?? "0");
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.iOS, "com.rmvr.flat");
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.iOS, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetArchitecture(BuildTargetGroup.iOS, 1);
            PlayerSettings.SetManagedStrippingLevel(BuildTargetGroup.iOS, ManagedStrippingLevel.Low);
            PlayerSettings.SetApiCompatibilityLevel(BuildTargetGroup.iOS, ApiCompatibilityLevel.NET_4_6);
            PlayerSettings.stripEngineCode = false;
            PlayerSettings.iOS.sdkVersion = iOSSdkVersion.DeviceSDK;
            PlayerSettings.iOS.targetOSVersionString = "13.0";
            PlayerSettings.iOS.requiresFullScreen = true;
            PlayerSettings.iOS.hideHomeButton = true;
            PlayerSettings.iOS.deferSystemGesturesMode = UnityEngine.iOS.SystemGestureDeferMode.All;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.iOS, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.iOS, new[] { UnityEngine.Rendering.GraphicsDeviceType.Metal });
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.statusBarHidden = true;
            PlayerSettings.runInBackground = false;
#pragma warning disable 618
            PlayerSettings.virtualRealitySupported = false;
#pragma warning restore 618
            EditorUserBuildSettings.iOSBuildConfigType = iOSBuildType.Release;

            Debug.Log("[RMVR] building iOS project to " + outDir);
            BuildReport report = BuildPipeline.BuildPlayer(scenes, outDir, BuildTarget.iOS, BuildOptions.None);
            Debug.Log("[RMVR] result " + report.summary.result + " errors=" + report.summary.totalErrors +
                      " size=" + (report.summary.totalSize / (1024 * 1024)) + "MB time=" + report.summary.totalTime);
            EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
        }
        catch (Exception e)
        {
            Debug.LogError("[RMVR] BUILD EXCEPTION: " + e);
            EditorApplication.Exit(1);
        }
    }

    // Let the Files app (and Filza) see Documents, where the log, settings and saves live.
    [PostProcessBuild]
    public static void OnPostprocessBuild(BuildTarget target, string path)
    {
        if (target != BuildTarget.iOS) return;
        string plistPath = Path.Combine(path, "Info.plist");
        var plist = new PlistDocument();
        plist.ReadFromFile(plistPath);
        plist.root.SetBoolean("UIFileSharingEnabled", true);
        plist.root.SetBoolean("LSSupportsOpeningDocumentsInPlace", true);
        plist.root.SetBoolean("UIRequiresFullScreen", true);
        plist.root.SetBoolean("GCSupportsControllerUserInteraction", true);
        plist.WriteToFile(plistPath);
    }
}
