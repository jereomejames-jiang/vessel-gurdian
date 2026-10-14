using UnityEditor;
using UnityEngine;
using MCPForUnity.Editor.Services;

[InitializeOnLoad]
public static class UnityMcpReconnectOnce
{
    static UnityMcpReconnectOnce()
    {
        EditorApplication.delayCall += Connect;
    }

    public static async void Connect()
    {
        if (SessionState.GetBool("VesselGuard.McpReconnect20261014", false)) return;
        EditorPrefs.SetBool("MCPForUnity.UseHttpTransport", true);
        EditorPrefs.SetString("MCPForUnity.HttpTransportScope", "local");
        EditorPrefs.SetString("MCPForUnity.HttpUrl", "http://127.0.0.1:8080");
        bool connected = await MCPServiceLocator.Bridge.StartAsync();
        SessionState.SetBool("VesselGuard.McpReconnect20261014", connected);
        Debug.Log("Unity MCP reconnect: " + connected);
    }
}

// connection retry
