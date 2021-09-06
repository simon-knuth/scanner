using Windows.Storage;
using Windows.UI.Xaml;

namespace Scanner.Services
{
    public interface IAccessibilityService
    {
        FlowDirection DefaultFlowDirection
        {
            get;
        }

        FlowDirection InvertedFlowDirection
        {
            get;
        }
    }
}
