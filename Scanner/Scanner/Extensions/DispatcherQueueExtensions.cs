using Microsoft.UI.Dispatching;
using System;
using System.Threading.Tasks;
using Windows.UI.Core;

namespace Scanner.Extensions;

public static class DispatcherQueueExtensions
{
    public static void RunOnThread(this DispatcherQueue queue, DispatcherQueuePriority priority,
        DispatchedHandler agileCallback)
    {
        if (queue.HasThreadAccess)
        {
            agileCallback();
        }
        else
        {
            queue.TryEnqueue(priority, () => agileCallback());
        }
    }

    public static async Task RunOnThreadAndWaitAsync(this DispatcherQueue queue, DispatcherQueuePriority priority,
        DispatchedHandler agileCallback)
    {
        if (queue.HasThreadAccess)
        {
            agileCallback();
        }
        else
        {
            TaskCompletionSource<bool> complete = new TaskCompletionSource<bool>();
            queue.TryEnqueue(priority, () =>
            {
                agileCallback();
                complete.SetResult(true);
            });
            await complete.Task;
        }
    }

    public static async Task RunOnThreadAndWaitAsync(this DispatcherQueue queue, DispatcherQueuePriority priority,
        Func<Task> agileCallback)
    {
        if (queue.HasThreadAccess)
        {
            await agileCallback();
        }
        else
        {
            TaskCompletionSource<bool> complete = new TaskCompletionSource<bool>();
            queue.TryEnqueue(priority, async () =>
            {
                try
                {
                    await agileCallback();
                    complete.SetResult(true);
                }
                catch (Exception ex)
                {
                    complete.SetException(ex);
                }
            });
            await complete.Task;
        }
    }
}
