using System;
using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace DiceCraft.Editor
{
    /// <summary>
    /// Command-line entry points for the evidence runs: complete discovery, then a complete EditMode
    /// or PlayMode suite through <see cref="TestEvidence"/> (which writes the NUnit report and its
    /// source/assembly binding). The Editor exits with 0 only when the run finished with no failures.
    /// Optional <c>-v072TestFilter name-fragment</c> narrows a diagnostic run; such a run is never a gate.
    /// </summary>
    [InitializeOnLoad]
    public static class V072BatchTests
    {
        private const string ActiveKey="DiceCraft.V072BatchTests.Active";
        private static readonly Exit Recorder=new Exit();
        static V072BatchTests()
        {
            // PlayMode reloads the domain: re-attach so the finished run can still end the process.
            if(!SessionState.GetBool(ActiveKey,false))return;
            var api=ScriptableObject.CreateInstance<TestRunnerApi>();api.RegisterCallbacks(Recorder);
        }
        public static void Discover()
        {
            string dir=Path.GetFullPath("../evidence/v072/unity");
            DateTime started=DateTime.UtcNow;double deadline=EditorApplication.timeSinceStartup+300;
            TestEvidence.DiscoverAll();
            EditorApplication.update+=Poll;
            void Poll()
            {
                bool done=true;
                foreach(string mode in new[]{"EditMode","PlayMode"})
                {string path=Path.Combine(dir,"discovery-"+mode+".json");done&=File.Exists(path)&&File.GetLastWriteTimeUtc(path)>=started.AddSeconds(-2);}
                if(!done&&EditorApplication.timeSinceStartup<deadline)return;
                EditorApplication.update-=Poll;Debug.Log(done?"V072_DISCOVERY_WRITTEN":"V072_DISCOVERY_TIMEOUT");EditorApplication.Exit(done?0:4);
            }
        }
        public static void RunEditMode()=>Run(TestMode.EditMode);
        public static void RunPlayMode()=>Run(TestMode.PlayMode);
        private static void Run(TestMode mode)
        {
            SessionState.SetBool(ActiveKey,true);
            ApplyGameView();
            var api=ScriptableObject.CreateInstance<TestRunnerApi>();api.RegisterCallbacks(Recorder);
            string[] args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"-v072TestFilter");
            if(at>=0&&at+1<args.Length)TestEvidence.RunFilters(mode,args[at+1].Split(','));else TestEvidence.RunFilters(mode);
        }
        // The layout suites require a real fixed GameView (1920x1080 or 1280x720), never a simulated one.
        // "-v072GameView 1920x1080" selects that size for the run and RestoreGameView puts the window back.
        private const string ViewIndexKey="DiceCraft.V072BatchTests.ViewIndex",ViewAddedKey="DiceCraft.V072BatchTests.ViewAdded";
        private const System.Reflection.BindingFlags Any=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic;
        private static object SizeGroup(out Type groupType)
        {
            var editor=typeof(UnityEditor.Editor).Assembly;var sizesType=editor.GetType("UnityEditor.GameViewSizes");
            object instance=typeof(ScriptableSingleton<>).MakeGenericType(sizesType).GetProperty("instance").GetValue(null);
            object group=sizesType.GetProperty("currentGroup").GetValue(instance);groupType=group.GetType();return group;
        }
        private static EditorWindow GameView(out System.Reflection.PropertyInfo selected)
        {
            var view=EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView"));
            selected=view.GetType().GetProperty("selectedSizeIndex",Any);return view;
        }
        private static void ApplyGameView()
        {
            string[] args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"-v072GameView");
            if(at<0||at+1>=args.Length)return;
            string[] parts=args[at+1].Split('x');int w=int.Parse(parts[0]),h=int.Parse(parts[1]);
            object group=SizeGroup(out Type groupType);int count=(int)groupType.GetMethod("GetTotalCount").Invoke(group,null),index=-1;
            for(int i=0;i<count&&index<0;i++)
            {
                object size=groupType.GetMethod("GetGameViewSize").Invoke(group,new object[]{i});var type=size.GetType();
                if((int)type.GetProperty("width").GetValue(size)==w&&(int)type.GetProperty("height").GetValue(size)==h&&type.GetProperty("sizeType").GetValue(size).ToString()=="FixedResolution")index=i;
            }
            bool added=index<0;
            if(added)
            {
                var editor=typeof(UnityEditor.Editor).Assembly;
                object size=Activator.CreateInstance(editor.GetType("UnityEditor.GameViewSize"),Enum.Parse(editor.GetType("UnityEditor.GameViewSizeType"),"FixedResolution"),w,h,"v072 "+w+"x"+h);
                groupType.GetMethod("AddCustomSize").Invoke(group,new[]{size});index=count;
            }
            var view=GameView(out var selected);
            SessionState.SetInt(ViewIndexKey,(int)selected.GetValue(view));SessionState.SetInt(ViewAddedKey,added?index:-1);
            selected.SetValue(view,index);view.Repaint();
            Debug.Log($"V072_GAMEVIEW {w}x{h} index={index} added={added}");
        }
        private static void RestoreGameView()
        {
            int previous=SessionState.GetInt(ViewIndexKey,-1);if(previous<0)return;
            var view=GameView(out var selected);selected.SetValue(view,previous);
            int added=SessionState.GetInt(ViewAddedKey,-1);
            if(added>=0){object group=SizeGroup(out Type groupType);groupType.GetMethod("RemoveCustomSize").Invoke(group,new object[]{added});}
            SessionState.EraseInt(ViewIndexKey);SessionState.EraseInt(ViewAddedKey);
        }
        private sealed class Exit:ICallbacks
        {
            public void RunStarted(ITestAdaptor tests){}
            public void TestStarted(ITestAdaptor test){}
            public void TestFinished(ITestResultAdaptor result){}
            public void RunFinished(ITestResultAdaptor result)
            {
                SessionState.SetBool(ActiveKey,false);
                RestoreGameView();
                int code=result.FailCount==0&&result.PassCount>0?0:2;
                Debug.Log($"V072_TESTS_FINISHED passed={result.PassCount} failed={result.FailCount} skipped={result.SkipCount} inconclusive={result.InconclusiveCount}");
                // Let TestEvidence finish writing its report and binding before the process ends.
                int frames=0;EditorApplication.update+=()=>{if(++frames>=10)EditorApplication.Exit(code);};
            }
        }
    }
}
