using System;
using Windows.ApplicationModel.Resources;

namespace Bridge
{
    internal static class Localizer
    {
        private static readonly ResourceLoader _loader = ResourceLoader.GetForViewIndependentUse("Strings/Resources");

        public static string Get(string key)
        {
            try
            {
                var s = _loader.GetString(key);
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
            try { return string.Format(fmt, args); } catch { return fmt; }
        }
    }
}
