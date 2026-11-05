using System;
using System.IO;
namespace jmsTools.Manage
{
    internal static class AppPaths
    {
        public static string DataDirectory {
            get {
                // The launcher supplies a stable path outside its immutable program cache.
                var configured = Environment.GetEnvironmentVariable("NANAVANITY_DATA_DIRECTORY");
                var path = string.IsNullOrWhiteSpace(configured) ? AppDomain.CurrentDomain.BaseDirectory : Path.GetFullPath(configured);
                Directory.CreateDirectory(path);
                return path;
            }
        }
    }
}
