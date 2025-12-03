using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace OpenGIS.Utils.Utils;

/// <summary>
///     排序工具类
/// </summary>
public static class SortUtil
{
    /// <summary>
    ///     自然排序比较字符串
    /// </summary>
    public static int CompareString(string a, string b)
    {
        if (a == null && b == null) return 0;
        if (a == null) return -1;
        if (b == null) return 1;

        var regex = new Regex(@"(\d+)|(\D+)");
        var aParts = regex.Matches(a).Cast<Match>().Select(m => m.Value).ToArray();
        var bParts = regex.Matches(b).Cast<Match>().Select(m => m.Value).ToArray();

        for (int i = 0; i < Math.Min(aParts.Length, bParts.Length); i++)
        {
            var aPart = aParts[i];
            var bPart = bParts[i];

            // 尝试解析为数字
            var aIsNumeric = int.TryParse(aPart, out int aNum);
            var bIsNumeric = int.TryParse(bPart, out int bNum);

            if (aIsNumeric && bIsNumeric)
            {
                // 都是数字，按数值比较
                var numCompare = aNum.CompareTo(bNum);
                if (numCompare != 0)
                    return numCompare;
            }
            else
            {
                // 至少有一个不是数字，按字符串比较
                var strCompare = string.Compare(aPart, bPart, StringComparison.Ordinal);
                if (strCompare != 0)
                    return strCompare;
            }
        }

        // 如果前面都相同，比较长度
        return aParts.Length.CompareTo(bParts.Length);
    }

    /// <summary>
    ///     自然排序
    /// </summary>
    public static IOrderedEnumerable<T> NaturalSort<T>(IEnumerable<T> source, Func<T, string> keySelector)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (keySelector == null)
            throw new ArgumentNullException(nameof(keySelector));

        return source.OrderBy(keySelector, new NaturalStringComparer());
    }

    private class NaturalStringComparer : IComparer<string>
    {
        public int Compare(string? x, string? y)
        {
            return CompareString(x ?? string.Empty, y ?? string.Empty);
        }
    }
}
