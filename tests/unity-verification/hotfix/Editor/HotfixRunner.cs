using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class HotfixRunner
{
    const string Key = "HotfixRunner.active";
    static double start;

    static HotfixRunner()
    {
        if (!SessionState.GetBool(Key, false)) return;
        start = EditorApplication.timeSinceStartup;
        EditorApplication.update += Tick;
    }

    public static void SetDefine()
    {
        PlayerSettings.SetScriptingDefineSymbolsForGroup(BuildTargetGroup.Standalone, "HOTFIX_ENABLE");
        AssetDatabase.SaveAssets();
    }

    public static void Gen()
    {
        CSObjectWrapEditor.Generator.GenAll();
    }

    public static void Run()
    {
#if HOTFIX_ENABLE
        XLua.Hotfix.HotfixInject();
#else
        Debug.Log("HOTFIXTEST HOTFIX_ENABLE not defined");
#endif
        SessionState.SetBool(Key, true);
        start = EditorApplication.timeSinceStartup;
        EditorApplication.update += Tick;
        EditorApplication.isPlaying = true;
    }

    static void Tick()
    {
        double waited = EditorApplication.timeSinceStartup - start;
        if (EditorApplication.isPlaying && waited > 20) EditorApplication.Exit(0);
        else if (!EditorApplication.isPlaying && waited > 180) EditorApplication.Exit(3);
    }
}
