using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Ogu4Net.Common
{
    /// <summary>
    /// 字符串自然排序工具类
    /// <para>
    /// 提供包含数字的字符串自然排序功能，如"第5章" &lt; "第10章"。
    /// 所有方法均为静态方法，无需实例化即可使用。
    /// </para>
    /// </summary>
    public static class SortUtil
    {
        private static readonly Regex SplitStringPattern = new Regex(@"(\D+)|(\d+)", RegexOptions.Compiled);
        private static readonly Regex IsNumPattern = new Regex(@"^\d+$", RegexOptions.Compiled);

        /// <summary>
        /// 包含数字的字符串进行比较（按照从小到大排序）
        /// </summary>
        /// <param name="string1">字符串1，如：第5章第100节课</param>
        /// <param name="string2">字符串2，如：第5章第10节课</param>
        /// <returns>比较结果，0：相等，-1：string1小于string2，1：string1大于string2</returns>
        public static int CompareString(string? string1, string? string2)
        {
            if (string1 == null && string2 == null)
                return 0;
            if (string1 == null)
                return -1;
            if (string2 == null)
                return 1;

            // 拆分两个字符串
            var lstString1 = SplitString(string1);
            var lstString2 = SplitString(string2);

            // 依次对比拆分出的每个值
            int index = 0;
            while (true)
            {
                // 如果两个列表完全相同
                if (lstString1.Count == lstString2.Count && index >= lstString1.Count)
                    return 0;

                string str1 = index < lstString1.Count ? lstString1[index] : "";
                string str2 = index < lstString2.Count ? lstString2[index] : "";

                // 字符串相等则继续判断下一组数据
                if (str1 == str2)
                {
                    index++;
                    continue;
                }

                // 是纯数字，比较数字大小
                if (IsNum(str1) && IsNum(str2))
                {
                    long num1 = long.Parse(str1);
                    long num2 = long.Parse(str2);
                    return num1 < num2 ? -1 : 1;
                }

                return string.Compare(str1, str2, StringComparison.OrdinalIgnoreCase);
            }
        }

        /// <summary>
        /// 获取自然排序比较器
        /// </summary>
        /// <returns>自然排序比较器</returns>
        public static IComparer<string> GetNaturalComparer()
        {
            return new NaturalStringComparer();
        }

        /// <summary>
        /// 拆分字符串
        /// 输入：第5章第100节课
        /// 返回：[第,5,章第,100,节课]
        /// </summary>
        private static List<string> SplitString(string str)
        {
            var list = new List<string>();
            var matches = SplitStringPattern.Matches(str);
            foreach (Match match in matches)
            {
                list.Add(match.Value);
            }
            return list;
        }

        /// <summary>
        /// 是否是纯数字
        /// </summary>
        private static bool IsNum(string str)
        {
            return !string.IsNullOrEmpty(str) && IsNumPattern.IsMatch(str);
        }

        /// <summary>
        /// 自然排序比较器
        /// </summary>
        private class NaturalStringComparer : IComparer<string>
        {
            public int Compare(string? x, string? y)
            {
                return CompareString(x, y);
            }
        }
    }
}
