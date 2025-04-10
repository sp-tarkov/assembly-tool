using System.Text;
using AsmResolver;

namespace AssemblyLib.Utils;

public static class SysTypeExtentions
{
    /// <summary>
    /// Returns a string trimmed after any non letter character
    /// </summary>
    /// <param name="str"></param>
    /// <returns>Trimmed string if special character found, or the original string</returns>
    public static string TrimAfterSpecialChar(this Utf8String str)
    {
        var sb = new StringBuilder();

        var trimChars = new char[] { '`', '[', ']' };

        foreach (char c in str.ToString())
        {
            if (trimChars.Contains(c))
            {
            }

            if (char.IsLetter(c) || char.IsDigit(c))
            {
                sb.Append(c);
            }
            else
            {
                return sb.ToString();
            }
        }

        if (sb.Length > 0)
        {
            return sb.ToString();
        }

        return str;
    }

    /// <summary>
    /// Returns a string trimmed after any non letter character
    /// </summary>
    /// <param name="str"></param>
    /// <returns>Trimmed string if special character found, or the original string</returns>
    public static string TrimAfterSpecialChar(this string str)
    {
        var sb = new StringBuilder();

        var trimChars = new char[] { '`', '[', ']' };

        foreach (char c in str)
        {
            if (trimChars.Contains(c))
            {
            }

            if (char.IsLetter(c) || char.IsDigit(c))
            {
                sb.Append(c);
            }
            else
            {
                return sb.ToString();
            }
        }

        if (sb.Length > 0)
        {
            return sb.ToString();
        }

        return str;
    }

    /// <summary>
    /// Does the property or field name exist in a given list, this applies prefixes and handles capitalization.
    /// </summary>
    /// <param name="str"></param>
    /// <param name="list"></param>
    /// <returns>True if it in the list</returns>
    public static bool IsFieldOrPropNameInList(this Utf8String str)
    {
        var realString = str.ToString();
        
        if (realString.Trim().StartsWith("_"))
        {
            realString = realString.Replace("_", "");
        }

        var list = DataProvider.Settings!.TypeNamesToMatch;
        var result = list.Any(item => realString.StartsWith(item, StringComparison.CurrentCultureIgnoreCase));

        return result;
    }
}