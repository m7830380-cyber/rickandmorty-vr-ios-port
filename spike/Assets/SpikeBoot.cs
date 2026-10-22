using System;
using System.IO;
using UnityEngine;

// Runtime half of the Unity 5.4 spike: proves the IL2CPP player starts on the phone, renders with Metal
// and can write to Documents. Shows a spinning cube plus device info and logs to Documents/RMVR-spike.log.
public class SpikeBoot : MonoBehaviour
{
    StreamWriter log;
    Transform cube;
    float fps, nextLog;

    void Awake()
    {
        try
        {
            string path = Path.Combine(Application.persistentDataPath, "RMVR-spike.log");
            log = new StreamWriter(path, false) { AutoFlush = true };
        }
        catch (Exception e) { Debug.LogError("[RMVR] log open failed: " + e); }
        Application.logMessageReceived += (msg, stack, type) => Write(type + ": " + msg);
        Application.targetFrameRate = 60;
        Write("Unity " + Application.unityVersion + " | " + SystemInfo.deviceModel + " | " + SystemInfo.operatingSystem);
        Write("GPU " + SystemInfo.graphicsDeviceName + " | API " + SystemInfo.graphicsDeviceType + " | RAM " + SystemInfo.systemMemorySize + " MB");
        Write("persistentDataPath " + Application.persistentDataPath);

        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube = go.transform;
        cube.position = new Vector3(0, 0, 4);
        var cam = Camera.main;
        if (cam != null) { cam.transform.position = Vector3.zero; cam.transform.rotation = Quaternion.identity; cam.backgroundColor = new Color(0.1f, 0.3f, 0.2f); cam.clearFlags = CameraClearFlags.SolidColor; }
    }

    void Write(string s)
    {
        if (log != null) log.WriteLine(DateTime.Now.ToString("HH:mm:ss.fff") + " " + s);
    }

    void Update()
    {
        cube.Rotate(30 * Time.deltaTime, 45 * Time.deltaTime, 0);
        fps = Mathf.Lerp(fps, 1f / Mathf.Max(Time.unscaledDeltaTime, 0.0001f), 0.05f);
        if (Time.time > nextLog) { nextLog = Time.time + 5; Write("alive fps=" + fps.ToString("F1")); }
    }

    void OnGUI()
    {
        GUI.matrix = Matrix4x4.Scale(Vector3.one * (Screen.height / 400f));
        GUI.Label(new Rect(10, 10, 600, 200), "RMVR spike: Unity " + Application.unityVersion + "\n" + SystemInfo.graphicsDeviceType + "  " + Screen.width + "x" + Screen.height + "\nFPS " + fps.ToString("F0"));
    }
}
