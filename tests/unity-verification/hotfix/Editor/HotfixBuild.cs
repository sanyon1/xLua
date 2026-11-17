using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class HotfixBuild
{
    public static void Il2cpp()
    {
        const string path = "Assets/HotfixMain.unity";
        if (!File.Exists(path))
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, path);
        }
        PlayerSettings.SetScriptingBackend(BuildTargetGroup.Standalone, ScriptingImplementation.IL2CPP);
        PlayerSettings.SetManagedStrippingLevel(BuildTargetGroup.Standalone, ManagedStrippingLevel.Low);
        var report = BuildPipeline.BuildPlayer(new[] { path }, "Build/hf/hf.exe", BuildTarget.StandaloneWindows64, BuildOptions.None);
        Debug.Log("BUILD_RESULT=" + report.summary.result + " errors=" + report.summary.totalErrors);
        if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded) EditorApplication.Exit(1);
    }
}
