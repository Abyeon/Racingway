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
    public class DataQueue : IDisposable
    {
        internal int Count => _taskQueue.Count;
        internal bool Active => _isProcessing;
        internal DateTime LastTaskTime { get; private set; }

        private readonly ConcurrentQueue<(Func<Task>, TaskCompletionSource<object>)> _taskQueue =
            new();
        private readonly SemaphoreSlim _queueLock = new SemaphoreSlim(1, 1);
        private bool _isProcessing = false;
        private CancellationTokenSource _cts = new CancellationTokenSource();
        private Task _processingTask;

        public DataQueue()
        {
            // Start the background processing task
            _processingTask = Task.Run(ProcessQueueAsync);
        }

        public void Dispose()
        {
            _cts.Cancel();
            _taskQueue.Clear();
            _queueLock.Dispose();
            _cts.Dispose();
        }

        public Task<T> QueueDataOperation<T>(Func<T> action)
        {
#if DEBUG
            var x = new StackFrame(1, true).GetMethod();
            Plugin.Log.Verbose(
                $"adding data operation from: {x?.Name} {x?.DeclaringType} tasks queued: {_taskQueue.Count + 1}"
            );
#endif
            var tcs = new TaskCompletionSource<T>();
            var wrapperTcs = new TaskCompletionSource<object>();

            // Wrap the synchronous function in an async one
            async Task ExecuteAsync()
            {
                try
                {
                    var result = action();
                    tcs.TrySetResult(result);
                    wrapperTcs.TrySetResult(null!);
                    await Task.CompletedTask;
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                    wrapperTcs.TrySetException(ex);
                }
            }

            _taskQueue.Enqueue((ExecuteAsync, wrapperTcs));
            return tcs.Task;
        }

        public Task QueueDataOperation(Action action)
        {
#if DEBUG
            var x = new StackFrame(1, true).GetMethod();
            Plugin.Log.Verbose(
                $"adding data operation from: {x?.Name} {x?.DeclaringType} tasks queued: {_taskQueue.Count + 1}"
            );
#endif
            var tcs = new TaskCompletionSource<object>();

            // Wrap the synchronous action in an async one
            async Task ExecuteAsync()
            {
                try
                {
                    action();
                    tcs.TrySetResult(null!);
                    await Task.CompletedTask;
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
            }

            _taskQueue.Enqueue((ExecuteAsync, tcs));
            return tcs.Task;
        }

        public Task QueueDataOperation(Func<Task> asyncAction)
        {
#if DEBUG
            var x = new StackFrame(1, true).GetMethod();
            Plugin.Log.Verbose(
                $"adding async data operation from: {x?.Name} {x?.DeclaringType} tasks queued: {_taskQueue.Count + 1}"
            );
#endif
            var tcs = new TaskCompletionSource<object>();

            // Wrap the async function in our standardized format
            async Task ExecuteAsync()
            {
                try
                {
                    await asyncAction();
                    tcs.TrySetResult(null);
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
            }

            _taskQueue.Enqueue((ExecuteAsync, tcs));
            return tcs.Task;
        }

        private async Task ProcessQueueAsync()
        {
            while (!_cts.IsCancellationRequested)
            {
                if (_taskQueue.IsEmpty)
                {
                    // Sleep briefly before checking the queue again
                    await Task.Delay(50, _cts.Token);
                    continue;
                }

                try
                {
                    await _queueLock.WaitAsync(_cts.Token);
                    _isProcessing = true;

                    // Process up to 5 items at a time in a batch
                    for (int i = 0; i < 5 && _taskQueue.TryDequeue(out var item); i++)
                    {
                        var (action, tcs) = item;
                        LastTaskTime = DateTime.Now;

                        Exception? capturedEx = null;

                        try
                        {
                            // Execute the action but don't complete the task yet
                            await action();
                        }
                        catch (Exception ex)
                        {
                            // Capture the exception but don't throw or set the task result yet
                            capturedEx = ex;
                            Plugin.Log.Error(ex, $"Exception in data task.");
                        }

                        // Complete the task outside the try-catch to prevent exceptions during task completion
                        try
                        {
                            // Use Task.Run to avoid deadlocks if task completion causes re-entry
                            await Task.Run(() =>
                            {
                                try
                                {
                                    if (capturedEx != null)
                                    {
                                        try
                                        {
                                            // Only try to set exception if task is not already in a final state
                                            if (
                                                !tcs.Task.IsCompleted
                                                && !tcs.Task.IsCanceled
                                                && !tcs.Task.IsFaulted
                                            )
                                                tcs.TrySetException(capturedEx);
                                        }
                                        catch
                                        { /* Ignore any exceptions from task completion */
                                        }
                                    }
                                    else
                                    {
                                        try
                                        {
                                            // Only try to set result if task is not already in a final state
                                            if (
                                                !tcs.Task.IsCompleted
                                                && !tcs.Task.IsCanceled
                                                && !tcs.Task.IsFaulted
                                            )
                                                tcs.TrySetResult(null);
                                        }
                                        catch
                                        { /* Ignore any exceptions from task completion */
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    // Absolutely ensure no exceptions escape from this Task.Run
                                    Plugin.Log.Error(ex, "Critical error in task completion");
                                }
                            });
                        }
                        catch (Exception ex)
                        {
                            // Ignore any exceptions from task completion to ensure we continue processing
                            Plugin.Log.Error(ex, $"Error completing task in data queue.");
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    // Normal cancellation, just exit
                    break;
                }
                catch (Exception ex)
                {
                    Plugin.Log.Error(ex, $"Exception in data queue processing.");
                }
                finally
                {
                    _isProcessing = false;
                    _queueLock.Release();
                }
            }
        }
    }
}
