#if UNITY_EDITOR
using UnityEditor;
using MCPForUnity.Editor.Services;
using MCPForUnity.Editor.Services.Transport;
[InitializeOnLoad]
public static class McpStartup
{
    static McpStartup() { EditorApplication.delayCall += Start; }
    static async void Start()
    {
        if (EditorApplication.isCompiling) { EditorApplication.delayCall += Start; return; }
        try { await MCPServiceLocator.TransportManager.StartAsync(TransportMode.Http); }
        catch (System.Exception e) { UnityEngine.Debug.LogWarning("MCP connection: " + e.Message); }
    }
}
#endif
