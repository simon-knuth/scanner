using FlaUI.Core.AutomationElements;
using FlaUI.Core.Conditions;
using FlaUI.Core.Input;
using FlaUI.Core.Tools;

namespace ScannerTests.Helpers;

public static class FileOpenPickerHelper
{
    #region Constants
    private const string ADDRESS_BAR_AUTOMATION_ID = "1001";
    private const string FILE_NAME_COMBOBOX_AUTOMATION_ID = "1148";
    private const string CONFIRM_BUTTON_AUTOMATION_ID = "1";
    #endregion

    public static void SetPath(ConditionFactory cf, Window window, string path)
    {
        Retry.WhileNull(() => window.FindFirstDescendant(cf.ByAutomationId(ADDRESS_BAR_AUTOMATION_ID))).Result.Click();
        Keyboard.Type(path);
    }

    public static void SetFiles(ConditionFactory cf, Window window, params string[] fileNames)
    {
        Retry.WhileNull(() => window.FindFirstDescendant(cf.ByAutomationId(FILE_NAME_COMBOBOX_AUTOMATION_ID))).Result.Click();

        string fileNamesInputString = "";
        if (fileNames.Length == 1)
        {
            fileNamesInputString = fileNames[0];
        }
        else
        {
            foreach (string fileName in fileNames)
            {
                fileNamesInputString += $"\"{fileName}\" ";
            }
        }
        Keyboard.Type(fileNamesInputString.Trim());
    }

    public static void ConfirmSelection(ConditionFactory cf, Window window)
    {
        Retry.WhileNull(() => window.FindFirstDescendant(cf.ByAutomationId(CONFIRM_BUTTON_AUTOMATION_ID).And(cf.ByControlType(FlaUI.Core.Definitions.ControlType.Button))).AsButton()).Result.Click();
    }
}