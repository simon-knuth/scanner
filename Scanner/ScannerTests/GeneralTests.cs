using FlaUI.Core.AutomationElements;
using FlaUI.Core.Conditions;
using FlaUI.Core.Input;
using FlaUI.Core.Tools;
using FlaUI.UIA3;
using ScannerTests.Helpers;

namespace ScannerTests;

#pragma warning disable CS8602 // Dereference of a possibly null reference.
#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.
#pragma warning disable CS8604 // Possible null reference argument.
[TestClass]
public sealed class GeneralTests
{
    [ClassInitialize]
    public static void InitializeTests(TestContext testContext)
    {
        Retry.DefaultInterval = TimeSpan.FromMilliseconds(200);
        Retry.DefaultTimeout = TimeSpan.FromMilliseconds(1000);
    }

    [TestMethod]
    public void General()
    {
        FlaUI.Core.Application? application = null;
        try
        {
            application = FlaUI.Core.Application.LaunchStoreApp(Constants.APP_USER_MODEL_ID);

            var mainWindow = application.GetMainWindow(new UIA3Automation());
            var cf = new ConditionFactory(new UIA3PropertyLibrary());

            Retry.WhileNull(() => mainWindow.FindFirstDescendant(cf.ByAutomationId(Scanner.Tests.ScanOptions.ScannersComboBoxId))).Result.AsComboBox().RightClick();
            Retry.WhileNull(() => mainWindow.FindFirstDescendant(cf.ByAutomationId(Scanner.Tests.ScanOptions.AddDebugScannerButtonId))).Result.AsButton().Click();
            Keyboard.Type(FlaUI.Core.WindowsAPI.VirtualKeyShort.ESC);
            Retry.WhileNull(() => mainWindow.FindFirstDescendant(cf.ByAutomationId(Scanner.Tests.ScanActions.ScanButtonId))).Result.AsButton().Click();
            Window filePickerWindow = Retry.WhileNull(() => mainWindow.ModalWindows.FirstOrDefault()).Result;
            FileOpenPickerHelper.SetPath(cf, filePickerWindow, "D:\\GitHub\\scanner\\Scanner\\Scanner\\Resources\\Test Images");
            FileOpenPickerHelper.SetFiles(cf, filePickerWindow, "Document Landscape 1.png", "Document Landscape 2.png", "Document Portrait 1.png", "Document Portrait 2.png");
            FileOpenPickerHelper.ConfirmSelection(cf, filePickerWindow);
        }
        finally
        {
            application?.Close(true);
        }
    }
}
#pragma warning restore CS8602 // Dereference of a possibly null reference.
#pragma warning restore CS8600 // Converting null literal or possible null value to non-nullable type.
#pragma warning restore CS8604 // Possible null reference argument.