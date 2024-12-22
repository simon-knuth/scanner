using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using System.Threading.Tasks;
using Windows.UI.Core;

namespace Scanner.Extensions
{
    public static class DependencyObjectExtensions
    {
        public static void RunOnUIThread(this DependencyObject dependencyObject, DispatcherQueuePriority priority,
            DispatchedHandler agileCallback)
        {
            if (dependencyObject.DispatcherQueue.HasThreadAccess)
            {
                agileCallback();
            }
            else
            {
                dependencyObject.DispatcherQueue.TryEnqueue(priority, () => agileCallback());
            }
        }

        public static async Task RunOnUIThreadAndWaitAsync(this DependencyObject dependencyObject, DispatcherQueuePriority priority,
            DispatchedHandler agileCallback)
        {
            if (dependencyObject.DispatcherQueue.HasThreadAccess)
            {
                agileCallback();
            }
            else
            {
                TaskCompletionSource<bool> complete = new TaskCompletionSource<bool>();
                dependencyObject.DispatcherQueue.TryEnqueue(priority, () =>
                {
                    agileCallback();
                    complete.SetResult(true);
                });
                await complete.Task;
            }
        }
    }
}
