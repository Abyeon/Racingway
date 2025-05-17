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
    // Optimized version of DataQueue to minimize main thread FPS impact
    public class DataQueue
    {
        // Thread pool for database operations
        private readonly ThreadPool _threadPool = new ThreadPool(2); // Use 2 worker threads for database operations

        // Coordinates all data sequence-sensitive operations
        internal int Count => DataTaskQueue.Count;
        internal bool Active => DataLock.CurrentCount == 0;
        private ConcurrentQueue<(Task, DateTime)> DataTaskQueue { get; init; } = new();
        private SemaphoreSlim DataLock { get; init; } = new SemaphoreSlim(1, 1);
        internal DateTime LastTaskTime { get; set; }

        internal void Dispose()
        {
            DataTaskQueue.Clear();
            _threadPool.Dispose();
        }

        internal Task<T> QueueDataOperation<T>(Func<T> action)
        {
#if DEBUG
            var x = new StackFrame(1, true).GetMethod();
            Plugin.Log.Verbose(
                $"adding data operation from: {x?.Name} {x?.DeclaringType} tasks queued: {DataTaskQueue.Count + 1}"
            );
#endif
            // Create task completion source that will be completed from worker thread
            var tcs = new TaskCompletionSource<T>();

            // Queue operation to run on worker thread, not on main thread
            _threadPool.QueueWorkItem(() =>
            {
                try
                {
                    var result = action();
                    tcs.SetResult(result);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });

            return tcs.Task;
        }

        internal Task QueueDataOperation(Action action)
        {
#if DEBUG
            var x = new StackFrame(1, true).GetMethod();
            Plugin.Log.Verbose(
                $"adding data operation from: {x?.Name} {x?.DeclaringType} tasks queued: {DataTaskQueue.Count + 1}"
            );
#endif
            // Create task completion source that will be completed from worker thread
            var tcs = new TaskCompletionSource<bool>();

            // Queue operation to run on worker thread, not on main thread
            _threadPool.QueueWorkItem(() =>
            {
                try
                {
                    action();
                    tcs.SetResult(true);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });

            return tcs.Task;
        }

        private Task AddToTaskQueue(Task task)
        {
            DataTaskQueue.Enqueue((task, DateTime.Now));
            RunNextTask();
            return task;
        }

        private Task RunNextTask()
        {
            return Task.Run(async () =>
            {
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

    // Simple ThreadPool implementation for dedicated database operations
    internal class ThreadPool : IDisposable
    {
        private readonly Thread[] _workers;
        private readonly ConcurrentQueue<Action> _workItems = new ConcurrentQueue<Action>();
        private readonly ManualResetEvent _workAvailable = new ManualResetEvent(false);
        private readonly ManualResetEvent _stopEvent = new ManualResetEvent(false);
        private bool _disposing;

        public ThreadPool(int workerCount)
        {
            _workers = new Thread[workerCount];

            for (int i = 0; i < workerCount; i++)
            {
                _workers[i] = new Thread(WorkerThread)
                {
                    IsBackground = true,
                    Name = $"RacingwayDbWorker-{i}",
                };
                _workers[i].Start();
            }
        }

        public void QueueWorkItem(Action workItem)
        {
            if (_disposing)
                return;

            _workItems.Enqueue(workItem);
            _workAvailable.Set();
        }

        private void WorkerThread()
        {
            WaitHandle[] waitHandles = new WaitHandle[] { _workAvailable, _stopEvent };

            while (true)
            {
                // Wait for work or stop signal
                int index = WaitHandle.WaitAny(waitHandles);

                // If stop was signaled
                if (index == 1)
                    break;

                // Process all available work items
                while (_workItems.TryDequeue(out Action? workItem))
                {
                    try
                    {
                        workItem();
                    }
                    catch (Exception ex)
                    {
                        // Log exception but continue processing
                        Plugin.Log.Error(ex, "Error executing database operation");
                    }
                }

                // Reset the work available event if queue is empty
                if (_workItems.IsEmpty)
                {
                    _workAvailable.Reset();
                }
            }
        }

        public void Dispose()
        {
            _disposing = true;
            _stopEvent.Set();

            foreach (var worker in _workers)
            {
                if (worker.IsAlive && !worker.Join(200))
                {
                    // If thread doesn't exit within timeout, continue
                    // We don't want to block the main thread for too long during disposal
                }
            }

            _workAvailable.Dispose();
            _stopEvent.Dispose();
        }
    }
}
