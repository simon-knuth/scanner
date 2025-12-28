using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Media;
using Scanner.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Scanners;
using WinRT.Interop;
using static Scanner.Helpers.Helpers;

namespace Scanner.Models
{
    public partial class Measurement
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        #region Services
        private ISettingsService SettingsService = Ioc.Default.GetRequiredService<ISettingsService>();
        #endregion

        private double inches;


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        private Measurement(double inches)
        {
            this.inches = inches;
        }

        public static Measurement FromInches(double value)
        {
            return new Measurement(value);
        }

        public static Measurement FromCentimeters(double value)
        {
            return new Measurement(value / 2.54);
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public double GetInches()
        {
            return inches;
        }

        public double GetCentimeters()
        {
            return inches * 2.54;
        }

        public override string ToString()
        {
            switch (SettingsService.SettingMeasurementUnits)
            {
                default:
                case SettingMeasurementUnits.Metric:
                    return $"{GetCentimeters():0.##} cm";
                case SettingMeasurementUnits.ImperialUS:
                    return $"{GetInches():0.##} in";
            }

        }
    }
}