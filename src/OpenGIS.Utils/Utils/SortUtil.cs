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
    // Cache compiled regex for performance
    private static readonly Regex NaturalSortRegex = new Regex(@"(\d+)|(\D+)", RegexOptions.Compiled);

    /// <summary>
    ///     自然排序比较字符串
    /// </summary>
    /// <param name="a">第一个字符串</param>
    /// <param name="b">第二个字符串</param>
    /// <returns>比较结果：小于0表示a在b前，等于0表示相同，大于0表示a在b后</returns>
    /// <remarks>自然排序将数字部分按数值比较，文本部分按字典序比较，例如："file1.txt" &lt; "file2.txt" &lt; "file10.txt"</remarks>
    public static int CompareString(string a, string b)
    {
        if (a == null && b == null) return 0;
        if (a == null) return -1;
        if (b == null) return 1;

        var aParts = NaturalSortRegex.Matches(a).Cast<Match>().Select(m => m.Value).ToArray();
        var bParts = NaturalSortRegex.Matches(b).Cast<Match>().Select(m => m.Value).ToArray();

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
    /// <typeparam name="T">元素类型</typeparam>
    /// <param name="source">数据源</param>
    /// <param name="keySelector">键选择器函数</param>
    /// <returns>排序后的序列</returns>
    /// <exception cref="ArgumentNullException">当source或keySelector为null时抛出</exception>
    /// <remarks>使用自然排序算法对集合进行排序，适用于包含数字的文件名或标识符</remarks>
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
