using Microsoft.UI.Xaml.Controls;
using Windows.UI.Xaml;

using static Utilities;


namespace Scanner
{
    static class ErrorMessage
    {
        public static void ShowErrorMessage(TeachingTip teachingTip, string title, string subtitle, TeachingTipPlacementMode preferredPlacement, FrameworkElement target, string glyph)
        {
            if (title != null) teachingTip.Title = title;
            else LocalizedString("ErrorMessageHeader");

            teachingTip.Subtitle = subtitle;

            teachingTip.PreferredPlacement = preferredPlacement;
            if (target != null) teachingTip.Target = target;

            var iconSource = new FontIconSource();
            if (glyph != null) iconSource.Glyph = glyph;
            else iconSource.Glyph = "\uE783";
            teachingTip.IconSource = iconSource;

            ReliablyOpenTeachingTip(teachingTip);
        }

        public static void ShowErrorMessage(TeachingTip teachingTip, string title, string subtitle, TeachingTipPlacementMode preferredPlacement, FrameworkElement target)
        {
            ShowErrorMessage(teachingTip, title, subtitle, preferredPlacement, target, null);
        }

        public static void ShowErrorMessage(TeachingTip teachingTip, string title, string subtitle, FrameworkElement target, string glyph)
        {
            ShowErrorMessage(teachingTip, title, subtitle, TeachingTipPlacementMode.Auto, target, glyph);
        }

        public static void ShowErrorMessage(TeachingTip teachingTip, string title, string subtitle, FrameworkElement target)
        {
            ShowErrorMessage(teachingTip, title, subtitle, TeachingTipPlacementMode.Auto, target, null);
        }

        public static void ShowErrorMessage(TeachingTip teachingTip, string title, string subtitle, TeachingTipPlacementMode preferredPlacement, string glyph)
        {
            ShowErrorMessage(teachingTip, title, subtitle, preferredPlacement, null, glyph);
        }

        public static void ShowErrorMessage(TeachingTip teachingTip, string title, string subtitle, TeachingTipPlacementMode preferredPlacement)
        {
            ShowErrorMessage(teachingTip, title, subtitle, preferredPlacement, null, null);
        }

        public static void ShowErrorMessage(TeachingTip teachingTip, string title, string subtitle, string glyph)
        {
            ShowErrorMessage(teachingTip, title, subtitle, TeachingTipPlacementMode.Auto, null, glyph);
        }

        public static void ShowErrorMessage(TeachingTip teachingTip, string title, string subtitle)
        {
            ShowErrorMessage(teachingTip, title, subtitle, TeachingTipPlacementMode.Auto, null, null);
        }
    }
}
