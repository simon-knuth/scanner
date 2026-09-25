using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

namespace Scanner.Extensions;

public static class UIElementExtensions
{
    /// <summary>
    ///     Checks whether keyboard focus is on <paramref name="element"/> or one of its descendants.
    /// </summary>
    public static bool IsFocusWithin(this UIElement element)
    {
        if (element.XamlRoot == null) return false;

        DependencyObject? current = FocusManager.GetFocusedElement(element.XamlRoot) as DependencyObject;
        while (current != null)
        {
            if (current == element) return true;
            current = VisualTreeHelper.GetParent(current);
        }
        return false;
    }
}
