using System;
using UnityEditor;
using UnityEngine;
using MCPForUnity.Editor.Services;

[InitializeOnLoad]
public static class UnityMcpReconnectOnce
{
    public static double nextAttemptAt;
    public static bool connecting;

    static UnityMcpReconnectOnce()
    {
        EditorApplication.update += Tick;
        EditorApplication.delayCall += TryConnect;
    }

    public static void Tick()
    {
        if (connecting || EditorApplication.timeSinceStartup < nextAttemptAt) return;
        TryConnect();
    }

    public static async void TryConnect()
    {
        if (connecting) return;
        connecting = true;
        nextAttemptAt = EditorApplication.timeSinceStartup + 10.0;
        try
        {
            EditorPrefs.SetBool("MCPForUnity.UseHttpTransport", true);
            EditorPrefs.SetString("MCPForUnity.HttpTransportScope", "local");
            EditorPrefs.SetString("MCPForUnity.HttpUrl", "http://127.0.0.1:8080");
            bool connected = await MCPServiceLocator.Bridge.StartAsync();
            nextAttemptAt = EditorApplication.timeSinceStartup + (connected ? 30.0 : 5.0);
            Debug.Log("Unity MCP reconnect: " + connected);
        }
        catch (Exception error)
        {
            nextAttemptAt = EditorApplication.timeSinceStartup + 5.0;
            Debug.LogWarning("Unity MCP reconnect failed: " + error.Message);
        }
        finally
        {
            connecting = false;
        }
    }
}

// retry
