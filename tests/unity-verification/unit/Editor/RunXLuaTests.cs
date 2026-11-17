using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class RunXLuaTests
{
    const string Key = "RunXLuaTests.active";
    static double start;

    static RunXLuaTests()
    {
        if (!SessionState.GetBool(Key, false)) return;
        start = EditorApplication.timeSinceStartup;
        EditorApplication.update += Tick;
    }

    public static void Gen()
    {
        CSObjectWrapEditor.Generator.GenAll();
    }

    public static void Run()
    {
        SessionState.SetBool(Key, true);
        EditorSceneManager.OpenScene("Assets/xLuaTest/main.unity");
        Debug.Log("XLUATEST gen_flag=" + XLua.DelegateBridge.Gen_Flag);
        start = EditorApplication.timeSinceStartup;
        EditorApplication.update += Tick;
        EditorApplication.isPlaying = true;
    }

    static void Tick()
    {
        double waited = EditorApplication.timeSinceStartup - start;
        if (EditorApplication.isPlaying && waited > 45)
        {
            Debug.Log("XLUATEST done");
            EditorApplication.Exit(0);
        }
        else if (!EditorApplication.isPlaying && waited > 180)
        {
            Debug.Log("XLUATEST never entered play mode");
            EditorApplication.Exit(3);
        }
    }
}
