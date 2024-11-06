using Microsoft.UI.Xaml;
using Microsoft.Windows.ApplicationModel.Resources;
using System;
using System.Globalization;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Services.Store;
using Windows.System;
using WinRT.Interop;

namespace Scanner.Helpers
{
    public static class Helpers
    {
        private static ResourceLoader resourceLoader = new ResourceLoader();

        public static int ComparePackageVersions(PackageVersion x, PackageVersion y)
        {
            if (x.Major != y.Major)
            {
                return x.Major.CompareTo(y.Major);
            }

            if (x.Minor != y.Minor)
            {
                return x.Minor.CompareTo(y.Minor);
            }

            if (x.Build != y.Build)
            {
                return x.Build.CompareTo(y.Build);
            }

            if (x.Revision != y.Revision)
            {
                return x.Revision.CompareTo(y.Revision);
            }

            return 0;
        }

        static async Task<T> DelayedResultTask<T>(TimeSpan delay, Func<T> fallbackMaker)
        {
            await Task.Delay(delay);
            return fallbackMaker();
        }

        public static async Task<T> TaskWithTimeoutAndFallback<T>(Task<T> task, TimeSpan timeout, Func<T> fallbackMaker)
        {
            return await await Task.WhenAny(task, DelayedResultTask<T>(timeout, fallbackMaker));
        }

        public static string GetCurrentVersion()
        {
            PackageVersion version = Package.Current.Id.Version;
            return String.Format("{0}.{1}.{2}.{3}", version.Major, version.Minor, version.Build, version.Revision);
        }

        public static PackageVersion? TryParsePackageVersion(string version)
        {
            try
            {
                string[] versionComponents = version.Split('.', StringSplitOptions.RemoveEmptyEntries);
                if (versionComponents.Length != 4) return null;

                PackageVersion result = new PackageVersion(
                    ushort.Parse(versionComponents[0]),
                    ushort.Parse(versionComponents[1]),
                    ushort.Parse(versionComponents[2]),
                    ushort.Parse(versionComponents[3])
                );
                return result;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static string FormatFileSize(long byteCount)
        {
            string[] sizeSuffixes = { "bytes", "KB", "MB", "GB", "TB", "PB", "EB", "ZB", "YB" };
            const int suffixIndex = 1024;
            if (byteCount == 0)
            {
                return "0 bytes";
            }

            var bytes = Math.Abs(byteCount);
            var place = Convert.ToInt32(Math.Floor(Math.Log(bytes, suffixIndex)));
            var num = Math.Round(bytes / Math.Pow(suffixIndex, place), 1);
            var suffix = sizeSuffixes[place];

            return $"{(Math.Sign(byteCount) * num):n1} {suffix}";
        }

        public static string GetLocalizedResource(string resourceName)
        {
            return resourceLoader.GetString(resourceName);
        }

        public static bool IsWindows11()
        {
            string version = Windows.System.Profile.AnalyticsInfo.VersionInfo.DeviceFamilyVersion;
            ulong versionNumber = ulong.Parse(version);
            ulong majorVersion = (versionNumber & 0xFFFF000000000000L) >> 48;
            ulong minorVersion = (versionNumber & 0x00000000FFFF0000L) >> 16;
            return majorVersion == 10 && minorVersion >= 22000;
        }

        public static string DateTimeToIso8601(DateTime dateTime)
        {
            DateTime utc = dateTime.ToUniversalTime();
            return dateTime.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ssZ");
        }

        public static DateTime Iso8601ToDateTime(string input)
        {
            return DateTime.ParseExact(input, "yyyy-MM-dd HH:mm:ssZ", null, DateTimeStyles.RoundtripKind);
        }
    }
}