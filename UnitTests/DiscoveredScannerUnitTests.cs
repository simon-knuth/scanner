using Moq;
using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Scanner.Models;
using Windows.Devices.Scanners;

namespace UnitTests
{
    [TestClass]
    public class DiscoveredScannerUnitTests
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////        
        
        // GenerateBrightnessConfig()
        [TestMethod]
        public void TestGenerateBrightnessConfigNormal()
        {
            var _mockSourceConfig = new Mock<IImageScannerSourceConfiguration>();
            _mockSourceConfig.Setup(x => x.MinBrightness).Returns(-1000);
            _mockSourceConfig.Setup(x => x.DefaultBrightness).Returns(0);
            _mockSourceConfig.Setup(x => x.MaxBrightness).Returns(1000);
            _mockSourceConfig.Setup(x => x.BrightnessStep).Returns(1);

            BrightnessConfig brightnessConfig = DiscoveredScanner.GenerateBrightnessConfig(_mockSourceConfig.Object);

            Assert.IsNotNull(brightnessConfig);
            Assert.AreEqual(brightnessConfig.MinBrightness, -1000);
            Assert.AreEqual(brightnessConfig.DefaultBrightness, 0);
            Assert.AreEqual(brightnessConfig.VirtualDefaultBrightness, -1);
            Assert.AreEqual(brightnessConfig.MaxBrightness, 1000);
            Assert.AreEqual(brightnessConfig.BrightnessStep, 1);
        }

        [TestMethod]
        public void TestGenerateBrightnessConfigBigStep()
        {
            var _mockSourceConfig = new Mock<IImageScannerSourceConfiguration>();
            _mockSourceConfig.Setup(x => x.MinBrightness).Returns(-1000);
            _mockSourceConfig.Setup(x => x.DefaultBrightness).Returns(0);
            _mockSourceConfig.Setup(x => x.MaxBrightness).Returns(1000);
            _mockSourceConfig.Setup(x => x.BrightnessStep).Returns(20);

            BrightnessConfig brightnessConfig = DiscoveredScanner.GenerateBrightnessConfig(_mockSourceConfig.Object);

            Assert.IsNotNull(brightnessConfig);
            Assert.AreEqual(brightnessConfig.MinBrightness, -1000);
            Assert.AreEqual(brightnessConfig.DefaultBrightness, 0);
            Assert.AreEqual(brightnessConfig.VirtualDefaultBrightness, -20);
            Assert.AreEqual(brightnessConfig.MaxBrightness, 1000);
            Assert.AreEqual(brightnessConfig.BrightnessStep, 20);
        }

        [TestMethod]
        public void TestGenerateBrightnessConfigInvalidStep()
        {
            var _mockSourceConfig = new Mock<IImageScannerSourceConfiguration>();
            _mockSourceConfig.Setup(x => x.MinBrightness).Returns(-1000);
            _mockSourceConfig.Setup(x => x.DefaultBrightness).Returns(0);
            _mockSourceConfig.Setup(x => x.MaxBrightness).Returns(1000);
            _mockSourceConfig.Setup(x => x.BrightnessStep).Returns(33);

            BrightnessConfig brightnessConfig = DiscoveredScanner.GenerateBrightnessConfig(_mockSourceConfig.Object);

            Assert.IsNotNull(brightnessConfig);
            Assert.AreEqual(brightnessConfig.MinBrightness, -1000);
            Assert.AreEqual(brightnessConfig.DefaultBrightness, 0);
            Assert.AreEqual(brightnessConfig.VirtualDefaultBrightness, -33);
            Assert.AreEqual(brightnessConfig.MaxBrightness, 1000);
            Assert.AreEqual(brightnessConfig.BrightnessStep, 33);
        }

        [TestMethod]
        public void TestGenerateBrightnessConfigInvalid()
        {
            var _mockSourceConfig = new Mock<IImageScannerSourceConfiguration>();
            _mockSourceConfig.Setup(x => x.BrightnessStep).Returns(0);

            BrightnessConfig brightnessConfig = DiscoveredScanner.GenerateBrightnessConfig(_mockSourceConfig.Object);

            Assert.IsNull(brightnessConfig);
        }

        [TestMethod]
        public void TestGenerateBrightnessConfigVirtualDefaultUp()
        {
            var _mockSourceConfig = new Mock<IImageScannerSourceConfiguration>();
            _mockSourceConfig.Setup(x => x.MinBrightness).Returns(-999);
            _mockSourceConfig.Setup(x => x.DefaultBrightness).Returns(0);
            _mockSourceConfig.Setup(x => x.MaxBrightness).Returns(1000);
            _mockSourceConfig.Setup(x => x.BrightnessStep).Returns(1);

            BrightnessConfig brightnessConfig = DiscoveredScanner.GenerateBrightnessConfig(_mockSourceConfig.Object);

            Assert.IsNotNull(brightnessConfig);
            Assert.AreEqual(brightnessConfig.MinBrightness, -999);
            Assert.AreEqual(brightnessConfig.DefaultBrightness, 0);
            Assert.AreEqual(brightnessConfig.VirtualDefaultBrightness, 1);
            Assert.AreEqual(brightnessConfig.MaxBrightness, 1000);
            Assert.AreEqual(brightnessConfig.BrightnessStep, 1);
        }

        // GenerateContrastConfig()
        [TestMethod]
        public void TestGenerateContrastConfigNormal()
        {
            var _mockSourceConfig = new Mock<IImageScannerSourceConfiguration>();
            _mockSourceConfig.Setup(x => x.MinContrast).Returns(-1000);
            _mockSourceConfig.Setup(x => x.DefaultContrast).Returns(0);
            _mockSourceConfig.Setup(x => x.MaxContrast).Returns(1000);
            _mockSourceConfig.Setup(x => x.ContrastStep).Returns(1);

            ContrastConfig contrastConfig = DiscoveredScanner.GenerateContrastConfig(_mockSourceConfig.Object);

            Assert.IsNotNull(contrastConfig);
            Assert.AreEqual(contrastConfig.MinContrast, -1000);
            Assert.AreEqual(contrastConfig.DefaultContrast, 0);
            Assert.AreEqual(contrastConfig.VirtualDefaultContrast, -1);
            Assert.AreEqual(contrastConfig.MaxContrast, 1000);
            Assert.AreEqual(contrastConfig.ContrastStep, 1);
        }

        [TestMethod]
        public void TestGenerateContrastConfigBigStep()
        {
            var _mockSourceConfig = new Mock<IImageScannerSourceConfiguration>();
            _mockSourceConfig.Setup(x => x.MinContrast).Returns(-1000);
            _mockSourceConfig.Setup(x => x.DefaultContrast).Returns(0);
            _mockSourceConfig.Setup(x => x.MaxContrast).Returns(1000);
            _mockSourceConfig.Setup(x => x.ContrastStep).Returns(20);

            ContrastConfig contrastConfig = DiscoveredScanner.GenerateContrastConfig(_mockSourceConfig.Object);

            Assert.IsNotNull(contrastConfig);
            Assert.AreEqual(contrastConfig.MinContrast, -1000);
            Assert.AreEqual(contrastConfig.DefaultContrast, 0);
            Assert.AreEqual(contrastConfig.VirtualDefaultContrast, -20);
            Assert.AreEqual(contrastConfig.MaxContrast, 1000);
            Assert.AreEqual(contrastConfig.ContrastStep, 20);
        }

        [TestMethod]
        public void TestGenerateContrastConfigInvalidStep()
        {
            var _mockSourceConfig = new Mock<IImageScannerSourceConfiguration>();
            _mockSourceConfig.Setup(x => x.MinContrast).Returns(-1000);
            _mockSourceConfig.Setup(x => x.DefaultContrast).Returns(0);
            _mockSourceConfig.Setup(x => x.MaxContrast).Returns(1000);
            _mockSourceConfig.Setup(x => x.ContrastStep).Returns(33);

            ContrastConfig contrastConfig = DiscoveredScanner.GenerateContrastConfig(_mockSourceConfig.Object);

            Assert.IsNotNull(contrastConfig);
            Assert.AreEqual(contrastConfig.MinContrast, -1000);
            Assert.AreEqual(contrastConfig.DefaultContrast, 0);
            Assert.AreEqual(contrastConfig.VirtualDefaultContrast, -33);
            Assert.AreEqual(contrastConfig.MaxContrast, 1000);
            Assert.AreEqual(contrastConfig.ContrastStep, 33);
        }

        [TestMethod]
        public void TestGenerateContrastConfigInvalid()
        {
            var _mockSourceConfig = new Mock<IImageScannerSourceConfiguration>();
            _mockSourceConfig.Setup(x => x.ContrastStep).Returns(0);

            ContrastConfig contrastConfig = DiscoveredScanner.GenerateContrastConfig(_mockSourceConfig.Object);

            Assert.IsNull(contrastConfig);
        }

        [TestMethod]
        public void TestGenerateContrastConfigVirtualDefaultUp()
        {
            var _mockSourceConfig = new Mock<IImageScannerSourceConfiguration>();
            _mockSourceConfig.Setup(x => x.MinContrast).Returns(-999);
            _mockSourceConfig.Setup(x => x.DefaultContrast).Returns(0);
            _mockSourceConfig.Setup(x => x.MaxContrast).Returns(1000);
            _mockSourceConfig.Setup(x => x.ContrastStep).Returns(1);

            ContrastConfig contrastConfig = DiscoveredScanner.GenerateContrastConfig(_mockSourceConfig.Object);

            Assert.IsNotNull(contrastConfig);
            Assert.AreEqual(contrastConfig.MinContrast, -999);
            Assert.AreEqual(contrastConfig.DefaultContrast, 0);
            Assert.AreEqual(contrastConfig.VirtualDefaultContrast, 1);
            Assert.AreEqual(contrastConfig.MaxContrast, 1000);
            Assert.AreEqual(contrastConfig.ContrastStep, 1);
        }
    }
}
