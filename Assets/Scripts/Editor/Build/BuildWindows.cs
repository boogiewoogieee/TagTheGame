using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class BuildWindows
{
    private const string OutputPath = "Builds/Windows/TagTheGame.exe";

    [MenuItem("TagTheGame/Build Windows")]
    public static void BuildRelease()
    {
        Build(false);
    }

    [MenuItem("TagTheGame/Build Windows (Development)")]
    public static void BuildDevelopment()
    {
        Build(true);
    }

    public static void BuildFromCommandLine()
    {
        try
        {
            Build(Environment.GetCommandLineArgs().Contains("-development"));
        }
        catch (Exception exception)
        {
            Debug.LogError("Windows build failed: " + exception);
            // CI needs a failing process status as well as a readable Editor log.
            EditorApplication.Exit(1);
        }
    }

    private static void Build(bool development)
    {
        // Build Settings is the team's source of truth for scene order.
        string[] scenes = EditorBuildSettings.scenes
            .Where(scene => scene.enabled)
            .Select(scene => scene.path)
            .ToArray();

        if (scenes.Length == 0)
        {
            throw new InvalidOperationException("Windows build failed: no scenes are enabled in Build Settings.");
        }

        string projectRoot = Path.GetDirectoryName(Application.dataPath);
        string output = Path.Combine(projectRoot, OutputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(output));

        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = output,
            target = BuildTarget.StandaloneWindows64,
            options = development ? BuildOptions.Development : BuildOptions.None,
        };

        Debug.Log("Building Windows player: " + output + " (" +
            (development ? "development" : "release") + ")\nScenes: " + string.Join(", ", scenes));

        BuildReport report = BuildPipeline.BuildPlayer(options);
        if (report == null || report.summary.result != BuildResult.Succeeded)
        {
            // The summary can omit the cause; build steps retain compiler and player errors.
            string reason = report == null
                ? "BuildPipeline returned no report."
                : string.Join(" | ", report.steps
                    .SelectMany(step => step.messages)
                    .Where(message => message.type == LogType.Error || message.type == LogType.Exception)
                    .Select(message => message.content)
                    .Distinct());
            if (string.IsNullOrEmpty(reason))
            {
                reason = report.summary.result + "; " + report.summary.totalErrors + " error(s).";
            }

            throw new InvalidOperationException("Windows build failed: " + reason);
        }

        Debug.Log("Windows build succeeded: " + output + "; size " +
            report.summary.totalSize + " bytes; time " + report.summary.totalTime + ".");
    }
}
