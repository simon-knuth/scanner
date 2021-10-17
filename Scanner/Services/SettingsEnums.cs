namespace Scanner.Services
{
    public static class SettingsEnums
    {
        public enum AppSetting
        {
            SettingSaveLocationType,
            SettingAppTheme,
            SettingAutoRotate,
            SettingAppendTime,
            SettingEditorOrientation,
            SettingRememberScanOptions,
            SettingErrorStatistics,
            TutorialPageListShown,
            LastKnownVersion,
            ScanNumber,
            LastTouchDrawState,
            IsFirstAppLaunchWithThisVersion,
            IsFirstAppLaunchEver,
            LastUsedCropAspectRatio,
            ShowOpenWithWarning
        }

        public enum SettingSaveLocationType
        {
            SetLocation = 0,
            AskEveryTime = 1
        }

        public enum SettingAppTheme
        {
            System = 0,
            Light = 1,
            Dark = 2
        }

        public enum SettingEditorOrientation
        {
            Vertical = 1,
            Horizontal = 0
        }
    }
}
