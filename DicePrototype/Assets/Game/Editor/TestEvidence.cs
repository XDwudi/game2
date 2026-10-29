using System;
using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace DiceCraft.Editor
{
    [InitializeOnLoad]
    public static class TestEvidence
    {
        static readonly TestRunnerApi Api;
        static TestEvidence(){Api=ScriptableObject.CreateInstance<TestRunnerApi>();Api.RegisterCallbacks(new Capture());}
        private sealed class Capture:ICallbacks
        {
            public void RunStarted(ITestAdaptor tests){}
            public void TestStarted(ITestAdaptor test){}
            public void TestFinished(ITestResultAdaptor result){}
            public void RunFinished(ITestResultAdaptor result)
            {
                string dir=Path.GetFullPath("../evidence/unity");Directory.CreateDirectory(dir);
                string stamp=DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
                TestRunnerApi.SaveResultToFile(result,Path.Combine(dir,"nunit-"+stamp+".xml"));
            }
        }
    }
}
