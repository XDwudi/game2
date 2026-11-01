using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DiceCraft.Core;
using UnityEngine;

namespace DiceCraft
{
    /// <summary>One private store per load, serialized by absolute path across scene lifetimes.</summary>
    public sealed class Art07StartupLoad
    {
        public sealed class Result
        {
            public AtomicSaveStore Store;
            public RunState State;
            public string Error;
            public int WorkerThreadId;
        }
        private static readonly object gate=new object();
        private static readonly Dictionary<string,Task> tails=new Dictionary<string,Task>(Path.DirectorySeparatorChar=='\\'?StringComparer.OrdinalIgnoreCase:StringComparer.Ordinal);
        private long started,ended;
        private int abandoned;
        public Task<Result> Completion { get; private set; }
        public bool IsCompleted=>Completion.IsCompleted;
        public bool IsAbandoned=>Volatile.Read(ref abandoned)!=0;
        public double ElapsedMilliseconds
        {
            get
            {
                long start=Interlocked.Read(ref started);if(start==0)return 0;
                long end=Interlocked.Read(ref ended);
                return ((end==0?Stopwatch.GetTimestamp():end)-start)*1000d/Stopwatch.Frequency;
            }
        }
        public void Abandon()=>Interlocked.Exchange(ref abandoned,1);
        public static Art07StartupLoad Begin(string absolutePath,string detachedConfigJson,Action<string> beforeRead=null)
        {
            if(!Path.IsPathRooted(absolutePath))throw new ArgumentException("Startup load requires an absolute path.",nameof(absolutePath));
            // Include a real wait for a previous owner of this path; the new scene is
            // already waiting for storage even though its own worker has not entered Load.
            string path=Path.GetFullPath(absolutePath);var operation=new Art07StartupLoad{started=Stopwatch.GetTimestamp()};
            lock(gate)
            {
                tails.TryGetValue(path,out Task previous);
                operation.Completion=Task.Run(async ()=>
                {
                    // A destroyed owner's synchronous file read cannot be cancelled safely.
                    // Its successor waits for it; no worker ever refers to a scene object.
                    if(previous!=null)try{await previous.ConfigureAwait(false);}catch(Exception){/* A failed predecessor does not suppress the next real read. */}
                    var result=new Result{WorkerThreadId=Thread.CurrentThread.ManagedThreadId};
                    if(operation.IsAbandoned){Interlocked.Exchange(ref operation.ended,Stopwatch.GetTimestamp());return result;}
                    try
                    {
                        var config=JsonUtility.FromJson<GameConfig>(detachedConfigJson);config.Validate();
                        // Canonicalize only the lease key; preserve the existing public save-path spelling.
                        result.Store=new AtomicSaveStore(absolutePath,config,SaveSlotPolicy.Normal);
                        beforeRead?.Invoke(path);
                        if(!operation.IsAbandoned)result.State=result.Store.Load();
                    }
                    catch(Exception error){result.Error=error.ToString();}
                    finally{Interlocked.Exchange(ref operation.ended,Stopwatch.GetTimestamp());}
                    return result;
                });
                tails[path]=operation.Completion;
                operation.Completion.ContinueWith(completed=>
                {
                    lock(gate)if(tails.TryGetValue(path,out Task current)&&ReferenceEquals(current,completed))tails.Remove(path);
                },CancellationToken.None,TaskContinuationOptions.ExecuteSynchronously,TaskScheduler.Default);
            }
            return operation;
        }
    }
}
