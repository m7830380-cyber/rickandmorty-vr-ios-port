using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

// Writes everything Unity logs to Documents/RickAndMortyVR-iOS.log (the previous run is kept as -prev.log), so a
// problem on the phone can be read in Filza or the Files app. Also keeps the last lines for the in-game viewer
// (tap the FPS counter) and logs unhandled exceptions with their stack traces.
public static class FlatLog
{
    const int KeepLines = 400;

    static StreamWriter writer;
    static readonly object gate = new object();
    static readonly Queue<string> recent = new Queue<string>();
    public static int ErrorCount;

    public static string LogPath
    {
        get { return Path.Combine(Application.persistentDataPath, "RickAndMortyVR-iOS.log"); }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Init()
    {
        try
        {
            string prev = Path.Combine(Application.persistentDataPath, "RickAndMortyVR-iOS-prev.log");
            if (File.Exists(LogPath))
            {
                if (File.Exists(prev)) File.Delete(prev);
                File.Move(LogPath, prev);
            }
            writer = new StreamWriter(new FileStream(LogPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite), new UTF8Encoding(false));
            writer.AutoFlush = true;
        }
        catch (Exception e)
        {
            Debug.LogWarning("[RMVR] log file unavailable: " + e.Message);
        }
        Application.logMessageReceivedThreaded += OnLog;
        AppDomain.CurrentDomain.UnhandledException += (s, a) => Write("UNHANDLED " + a.ExceptionObject);
        Write("Rick and Morty: Virtual Rick-ality flat iOS build " + Application.version + " | Unity " + Application.unityVersion +
              " | " + SystemInfo.deviceModel + " | " + SystemInfo.operatingSystem + " | " + SystemInfo.graphicsDeviceName +
              " | RAM " + SystemInfo.systemMemorySize + " MB | screen " + Screen.width + "x" + Screen.height);
    }

    static void OnLog(string message, string stackTrace, LogType type)
    {
        if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert)
        {
            ErrorCount++;
            Write(type.ToString().ToUpperInvariant() + " " + message + (string.IsNullOrEmpty(stackTrace) ? "" : "\n" + stackTrace.TrimEnd()));
        }
        else
        {
            Write((type == LogType.Warning ? "WARN " : "") + message);
        }
    }

    public static void Write(string line)
    {
        line = DateTime.Now.ToString("HH:mm:ss.fff") + " " + line;
        lock (gate)
        {
            try
            {
                if (writer != null) writer.WriteLine(line);
            }
            catch
            {
                // a full disk must never take the game down
            }
            recent.Enqueue(line);
            while (recent.Count > KeepLines) recent.Dequeue();
        }
    }

    public static string[] Recent()
    {
        lock (gate) return recent.ToArray();
    }
}
