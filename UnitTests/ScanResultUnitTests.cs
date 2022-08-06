using Moq;
using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Scanner.Models;
using Windows.Devices.Scanners;
using Scanner;
using System.Collections.Generic;

namespace UnitTests
{
    [TestClass]
    public class GetNewIndexAccordingToMergeConfig
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////        

        // GetNewIndexAccordingToMergeConfig()
        [TestMethod]
        public void TestGetNewIndexAccordingToMergeConfigNormal()
        {
            ScanMergeConfig scanMergeConfig = new ScanMergeConfig
            {
                InsertIndices = new List<int>
                {
                    0, 2, 4
                },
                SurplusPagesIndex = 5,
                InsertReversed = false
            };

            // 5 total pages
            int calculatedIndex = ScanResult.GetNewIndexAccordingToMergeConfig(scanMergeConfig, 0, 5);
            Assert.AreEqual(calculatedIndex, 0);

            calculatedIndex = ScanResult.GetNewIndexAccordingToMergeConfig(scanMergeConfig, 1, 5);
            Assert.AreEqual(calculatedIndex, 2);

            calculatedIndex = ScanResult.GetNewIndexAccordingToMergeConfig(scanMergeConfig, 2, 5);
            Assert.AreEqual(calculatedIndex, 4);

            calculatedIndex = ScanResult.GetNewIndexAccordingToMergeConfig(scanMergeConfig, 3, 5);
            Assert.AreEqual(calculatedIndex, 5);

            calculatedIndex = ScanResult.GetNewIndexAccordingToMergeConfig(scanMergeConfig, 4, 5);
            Assert.AreEqual(calculatedIndex, 6);

            // 2 total pages
            calculatedIndex = ScanResult.GetNewIndexAccordingToMergeConfig(scanMergeConfig, 0, 2);
            Assert.AreEqual(calculatedIndex, 0);

            calculatedIndex = ScanResult.GetNewIndexAccordingToMergeConfig(scanMergeConfig, 1, 2);
            Assert.AreEqual(calculatedIndex, 2);
        }

        [TestMethod]
        public void TestGetNewIndexAccordingToMergeConfigReversed()
        {
            ScanMergeConfig scanMergeConfig = new ScanMergeConfig
            {
                InsertIndices = new List<int>
                {
                    0, 2, 4
                },
                SurplusPagesIndex = 5,
                InsertReversed = true
            };

            // 5 total pages
            int calculatedIndex = ScanResult.GetNewIndexAccordingToMergeConfig(scanMergeConfig, 0, 5);
            Assert.AreEqual(calculatedIndex, 6);

            calculatedIndex = ScanResult.GetNewIndexAccordingToMergeConfig(scanMergeConfig, 1, 5);
            Assert.AreEqual(calculatedIndex, 5);

            calculatedIndex = ScanResult.GetNewIndexAccordingToMergeConfig(scanMergeConfig, 2, 5);
            Assert.AreEqual(calculatedIndex, 4);

            calculatedIndex = ScanResult.GetNewIndexAccordingToMergeConfig(scanMergeConfig, 3, 5);
            Assert.AreEqual(calculatedIndex, 2);

            calculatedIndex = ScanResult.GetNewIndexAccordingToMergeConfig(scanMergeConfig, 4, 5);
            Assert.AreEqual(calculatedIndex, 0);

            // 2 total pages
            calculatedIndex = ScanResult.GetNewIndexAccordingToMergeConfig(scanMergeConfig, 0, 2);
            Assert.AreEqual(calculatedIndex, 2);

            calculatedIndex = ScanResult.GetNewIndexAccordingToMergeConfig(scanMergeConfig, 1, 2);
            Assert.AreEqual(calculatedIndex, 0);
        }
    }
}
