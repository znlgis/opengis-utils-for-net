using System;
using System.Globalization;

namespace OpenGIS.Utils.Utils;

/// <summary>
///     数字处理工具类
/// </summary>
public static class NumUtil
{
    /// <summary>
    ///     去除科学计数法，返回普通字符串表示
    /// </summary>
    public static string GetPlainString(double number)
    {
        if (double.IsNaN(number) || double.IsInfinity(number))
            return number.ToString();

        // 使用 "G17" 格式确保精度
        var str = number.ToString("G17", CultureInfo.InvariantCulture);

        // 如果包含 E 或 e，则是科学计数法，需要转换
        if (str.Contains("E") || str.Contains("e"))
            // 使用定点表示法
            return number.ToString("F" + GetDecimalPlaces(number), CultureInfo.InvariantCulture).TrimEnd('0')
                .TrimEnd('.');

        return str;
    }

    /// <summary>
    ///     去除科学计数法，返回普通字符串表示
    /// </summary>
    public static string GetPlainString(decimal number)
    {
        return number.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    ///     四舍五入
    /// </summary>
    public static double Round(double value, int decimals)
    {
        return Math.Round(value, decimals, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    ///     格式化数字，保留指定小数位数
    /// </summary>
    public static string FormatNumber(double value, int decimals)
    {
        return value.ToString("F" + decimals, CultureInfo.InvariantCulture);
    }

    /// <summary>
    ///     获取小数位数
    /// </summary>
    private static int GetDecimalPlaces(double number)
    {
        if (double.IsNaN(number) || double.IsInfinity(number))
            return 0;

        var abs = Math.Abs(number);
        if (abs < 1e-10)
            return 10;
        if (abs >= 1)
            return 6;

        // 对于小数，计算需要的精度
        var exponent = Math.Floor(Math.Log10(abs));
        return Math.Max(0, (int)(6 - exponent));
    }
}
