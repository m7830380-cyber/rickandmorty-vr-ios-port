using System;
using System.IO;
using UnityEngine;

// Player options for the flat build, stored as JSON in Documents (Application.persistentDataPath on iOS).
[Serializable]
public class FlatSettings
{
    public float lookSensitivity = 1f;
    public float fieldOfView = 75f;
    public bool invertLook;
    public float buttonScale = 1f;
    public float buttonOpacity = 0.45f;
    public bool hideButtons;
    public int quality = 1;          // 0 low, 1 medium, 2 high
    public float playerHeight = 1.45f;
    public bool showFps = true;

    static FlatSettings current;

    public static string FilePath
    {
        get { return Path.Combine(Application.persistentDataPath, "flat-settings.json"); }
    }

    public static FlatSettings Current
    {
        get
        {
            if (current == null) current = Load();
            return current;
        }
    }

    static FlatSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonUtility.FromJson<FlatSettings>(File.ReadAllText(FilePath)) ?? new FlatSettings();
        }
        catch (Exception e)
        {
            Debug.LogWarning("[RMVR] settings unreadable, using defaults: " + e.Message);
        }
        return new FlatSettings();
    }

    public void Save()
    {
        try
        {
            File.WriteAllText(FilePath, JsonUtility.ToJson(this, true));
        }
        catch (Exception e)
        {
            Debug.LogWarning("[RMVR] could not save settings: " + e.Message);
        }
    }

    static int nativeWidth, nativeHeight;

    // Low: per-vertex lighting, no MSAA, 60% resolution. Medium: per-pixel, 2x MSAA, 80%. High: 4x MSAA, full.
    public void Apply()
    {
        quality = Mathf.Clamp(quality, 0, 2);
        QualitySettings.shadows = ShadowQuality.Disable;
        QualitySettings.antiAliasing = new[] { 0, 2, 4 }[quality];
        QualitySettings.pixelLightCount = 1;
        QualitySettings.vSyncCount = 0;
        if (quality == 0) Shader.EnableKeyword("FLAT_VERTEX_LIGHTING");
        else Shader.DisableKeyword("FLAT_VERTEX_LIGHTING");
        if (nativeWidth == 0)
        {
            nativeWidth = Mathf.Max(Screen.width, Screen.height);
            nativeHeight = Mathf.Min(Screen.width, Screen.height);
        }
        float scale = new[] { 0.6f, 0.8f, 1f }[quality];
        Screen.SetResolution(Mathf.RoundToInt(nativeWidth * scale), Mathf.RoundToInt(nativeHeight * scale), true);
        Application.targetFrameRate = 60;
    }
}
