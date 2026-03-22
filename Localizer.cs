using System;
using Windows.ApplicationModel.Resources;

namespace Bridge
{
    internal static class Localizer
    {
        // Lazy so a failed init does not crash the whole app at static ctor time
        private static ResourceLoader? _loader;

        private static ResourceLoader? GetLoader()
        {
            if (_loader != null) return _loader;
            try
            {
                // In WinUI 3 / Windows App SDK the resource map name is the .resw
                // filename without extension — NOT the folder path.
                // Strings\<locale>\Resources.resw  →  map name = "Resources"
                _loader = ResourceLoader.GetForViewIndependentUse("Resources");
            }
            catch
            {
                // resource PRI not available (e.g. unpackaged debug run without MakePri)
                _loader = null;
            }
            return _loader;
        }

        public static string Get(string key)
        {
            try
            {
                var s = GetLoader()?.GetString(key);
                return string.IsNullOrEmpty(s) ? key : s;
            }
            catch
            {
                return key;
            }
        }

        public static string GetFormat(string key, params object[] args)
        {
            var fmt = Get(key);
            try { return string.Format(fmt, args); }
            catch { return fmt; }
        }

        public static string GetOrDefault(string key, string def)
        {
            try
            {
                var s = GetLoader()?.GetString(key);
                return string.IsNullOrEmpty(s) ? def : s;
            }
            catch { return def; }
        }
    }
}
