using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace DiceCraft.Editor
{
    /// <summary>
    /// Unverified Windows build for looking at work in progress (screenshots, art review).
    /// It writes only to the git-ignored artifacts cache and is never a delivery: releases go through
    /// <see cref="PrototypeBuild.BuildActiveProfile"/> and its test gate.
    /// </summary>
    public static class V072DevBuild
    {
        public const string Output="../artifacts/cache/v072/dev-build/DicePrototype.exe";
        public static void Build()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Output));
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes=new[]{"Assets/Game/Scenes/Bootstrap.unity"},locationPathName=Output,
                target=BuildTarget.StandaloneWindows64,options=BuildOptions.None
            });
            Debug.Log($"V072_DEV_BUILD {report.summary.result} errors={report.summary.totalErrors} warnings={report.summary.totalWarnings} seconds={report.summary.totalTime.TotalSeconds:0}");
            if(report.summary.result!=BuildResult.Succeeded)throw new InvalidOperationException("Dev build failed: "+report.summary.result);
        }
    }
}
