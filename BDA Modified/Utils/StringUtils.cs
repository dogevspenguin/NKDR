using System.Collections.Generic;
using System.Text;
using KSP.Localization;

namespace BDArmory.Utils
{
    public static class StringUtils
    {
        static Dictionary<string, string> localizedStrings = new Dictionary<string, string>(); // Cache localized strings so that they don't need to be repeatedly localized.

        public static string Localize(string template)
        {
            if (!localizedStrings.TryGetValue(template, out string result))
            {
                result = Localizer.Format(template);
                localizedStrings[template] = result;
            }
            return result;
        }

        // static StringBuilder localizedStringBuilder1 = new StringBuilder();
        public static string Localize(string template, params string[] list) => Localizer.Format(template, list); // Don't have a good way to handle <<1>> yet.
        // {
        //     localizedStringBuilder1.Clear();
        //     localizedStringBuilder1.Append(Localize(template));
        //     for (int i = 0; i < list.Length; ++i)
        //     {
        //         localizedStringBuilder1.Append($" {list[i]}");
        //     }
        //     return localizedStringBuilder1.ToString();
        // }

        // static StringBuilder localizedStringBuilder2 = new StringBuilder();
        public static string Localize(string template, params object[] list) => Localizer.Format(template, list); // Don't have a good way to handle <<1>> yet.
        // {
        //     localizedStringBuilder2.Clear();
        //     localizedStringBuilder2.Append(Localize(template));
        //     for (int i = 0; i < list.Length; ++i)
        //     {
        //         localizedStringBuilder2.Append($" {list[i]}");
        //     }
        //     return localizedStringBuilder2.ToString();
        // }
    }
}