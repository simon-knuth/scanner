using Windows.UI.Xaml;

namespace Scanner.Services
{
    /// <summary>
    ///     Simplifies meeting accessibility requirements.
    /// </summary>
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
