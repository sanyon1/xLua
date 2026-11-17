using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class Build
{
    public static void Gen()
    {
        CSObjectWrapEditor.Generator.GenAll();
        AssetDatabase.Refresh();
        Debug.Log("GEN_FLAG_IN_EDITOR=" + XLua.DelegateBridge.Gen_Flag);
    }

    public static void CheckEditor()
    {
        Debug.Log("GEN_FLAG_IN_EDITOR=" + XLua.DelegateBridge.Gen_Flag + " asm=" + XLua.GenRegistry.GenAssemblies.Length);
    }

    internal static string[] Scene()
    {
        const string path = "Assets/Main.unity";
        if (!File.Exists(path))
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, path);
        }
        return new[] { path };
    }

    public static void Win()
    {
        PlayerSettings.SetScriptingBackend(BuildTargetGroup.Standalone, ScriptingImplementation.Mono2x);
        PlayerSettings.SetManagedStrippingLevel(BuildTargetGroup.Standalone, ManagedStrippingLevel.High);
        var report = BuildPipeline.BuildPlayer(Scene(), "Build/Win/smoke.exe", BuildTarget.StandaloneWindows64, BuildOptions.None);
        Debug.Log("BUILD_RESULT=" + report.summary.result + " errors=" + report.summary.totalErrors);
        if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded) EditorApplication.Exit(1);
    }

    public static void Android()
    {
        PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.SetManagedStrippingLevel(BuildTargetGroup.Android, ManagedStrippingLevel.High);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "com.sanyoni.xluasmoke");
        var report = BuildPipeline.BuildPlayer(Scene(), "Build/Android/smoke.apk", BuildTarget.Android, BuildOptions.None);
        Debug.Log("BUILD_RESULT=" + report.summary.result + " errors=" + report.summary.totalErrors);
        if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded) EditorApplication.Exit(1);
    }
}

public static class BuildIl2cpp
{
    public static void Win()
    {
        PlayerSettings.SetScriptingBackend(BuildTargetGroup.Standalone, ScriptingImplementation.IL2CPP);
        PlayerSettings.SetManagedStrippingLevel(BuildTargetGroup.Standalone, ManagedStrippingLevel.High);
        var report = BuildPipeline.BuildPlayer(Build.Scene(), "Build/WinIl2cpp/smoke.exe", BuildTarget.StandaloneWindows64, BuildOptions.None);
        Debug.Log("BUILD_RESULT=" + report.summary.result + " errors=" + report.summary.totalErrors);
        if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded) EditorApplication.Exit(1);
    }
}
