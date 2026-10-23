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

            LicenseReport();
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

    // Logs what the editor believes about its license: every parameterless bool/string/int licence query on
    // InternalEditorUtility, plus whether iOS counts as supported. Only results, never license contents.
    static void LicenseReport()
    {
        var t = typeof(UnityEditorInternal.InternalEditorUtility);
        foreach (var m in t.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static))
        {
            string n = m.Name;
            bool relevant = n.IndexOf("Licen", StringComparison.OrdinalIgnoreCase) >= 0 || n.StartsWith("HasPro") ||
                            n.StartsWith("HasAdvanced") || n.StartsWith("HasTeam") || n.StartsWith("HasFree") || n.StartsWith("IsUnityBeta");
            if (!relevant) continue;
            var ps = m.GetParameters();
            try
            {
                if (ps.Length == 0 && (m.ReturnType == typeof(bool) || m.ReturnType == typeof(int) || m.ReturnType.IsEnum))
                    Debug.Log("[RMVR] license " + n + "() = " + m.Invoke(null, null));
                else if (ps.Length == 1 && ps[0].ParameterType == typeof(BuildTarget) && m.ReturnType == typeof(bool))
                    Debug.Log("[RMVR] license " + n + "(iOS) = " + m.Invoke(null, new object[] { BuildTarget.iOS }));
                else
                    Debug.Log("[RMVR] license method " + m.ReturnType.Name + " " + n + "(" + ps.Length + " args)");
            }
            catch (Exception e)
            {
                Debug.Log("[RMVR] license " + n + " threw " + e.GetBaseException().Message);
            }
        }
        foreach (var m in typeof(BuildPipeline).GetMethods(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static))
        {
            var ps = m.GetParameters();
            if (m.Name != "IsBuildTargetSupported" || ps.Length != 1 || ps[0].ParameterType != typeof(BuildTarget)) continue;
            try { Debug.Log("[RMVR] IsBuildTargetSupported(iOS) = " + m.Invoke(null, new object[] { BuildTarget.iOS })); }
            catch (Exception e) { Debug.Log("[RMVR] IsBuildTargetSupported threw " + e.GetBaseException().Message); }
        }
    }
}
