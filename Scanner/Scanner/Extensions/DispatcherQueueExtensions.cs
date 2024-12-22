using Microsoft.UI.Dispatching;
using System.Threading.Tasks;
using Windows.UI.Core;

namespace Scanner.Extensions
{
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
    }
}
