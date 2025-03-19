using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Racingway.Utils.Storage
{
    // Shamelessly yoinking from PvpStats... because why reinvent the wheel?
    //https://github.com/wrath16/PvpStats/blob/master/PvpStats/Utility/DataQueue.cs
    public class DataQueue
    {

        //coordinates all data sequence-sensitive operations
        internal int Count => DataTaskQueue.Count;
        internal bool Active => DataLock.CurrentCount == 0;
        private ConcurrentQueue<(Task, DateTime)> DataTaskQueue { get; init; } = new();
        private SemaphoreSlim DataLock { get; init; } = new SemaphoreSlim(1, 1);
        internal DateTime LastTaskTime { get; set; }

        internal void Dispose()
        {
            DataTaskQueue.Clear();
        }

        internal Task<T> QueueDataOperation<T>(Func<T> action)
        {
#if DEBUG
            var x = new StackFrame(1, true).GetMethod();
            Plugin.Log.Verbose($"adding data operation from: {x?.Name} {x?.DeclaringType} tasks queued: {DataTaskQueue.Count + 1}");
#endif
            Task<T> t = new(action);
            AddToTaskQueue(t);
            return t;
        }

        internal Task QueueDataOperation(Action action)
        {
#if DEBUG
            var x = new StackFrame(1, true).GetMethod();
            Plugin.Log.Verbose($"adding data operation from: {x?.Name} {x?.DeclaringType} tasks queued: {DataTaskQueue.Count + 1}");
#endif
            Task t = new(action);
            AddToTaskQueue(t);
            return t;
        }

        private Task AddToTaskQueue(Task task)
        {
            DataTaskQueue.Enqueue((task, DateTime.Now));
            RunNextTask();
            return task;
        }

        private Task RunNextTask()
        {
            return Task.Run(async () => {
                try
                {
                    await DataLock.WaitAsync();
                    if (DataTaskQueue.TryDequeue(out (Task task, DateTime timestamp) nextTask))
                    {
                        LastTaskTime = nextTask.timestamp;
                        nextTask.task.Start();
                        await nextTask.task;
                        if (nextTask.task.GetType().IsAssignableTo(typeof(Task<Task>)))
                        {
                            var nestedTask = nextTask.task as Task<Task>;
                            await nestedTask!.Result;
                        }
                    }
                    else
                    {
                        throw new InvalidOperationException("Unable to dequeue task!");
                    }
                }
                catch (Exception e)
                {
                    Plugin.Log.Error(e, $"Exception in data task.");
                }
                finally
                {
                    DataLock.Release();
                }
            });
        }
    }
}
