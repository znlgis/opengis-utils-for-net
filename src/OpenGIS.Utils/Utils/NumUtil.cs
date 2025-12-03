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
    /// <param name="number">双精度浮点数</param>
    /// <returns>普通表示法的数字字符串</returns>
    public static string GetPlainString(double number)
    {
        if (double.IsNaN(number) || double.IsInfinity(number))
            return number.ToString(CultureInfo.InvariantCulture);

        // 使用 "G17" 格式确保精度
        var str = number.ToString("G17", CultureInfo.InvariantCulture);

        // 如果包含 E 或 e，则是科学计数法，需要转换
        if (str.IndexOf('E') >= 0 || str.IndexOf('e') >= 0)
        {
            var decimalPlaces = GetDecimalPlaces(number);
            // 使用定点表示法
            return number.ToString($"F{decimalPlaces}", CultureInfo.InvariantCulture)
                .TrimEnd('0')
                .TrimEnd('.');
        }

        return str;
    }

    /// <summary>
    ///     去除科学计数法，返回普通字符串表示
    /// </summary>
    /// <param name="number">Decimal 数</param>
    /// <returns>普通表示法的数字字符串</returns>
    public static string GetPlainString(decimal number)
    {
        return number.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    ///     四舍五入
    /// </summary>
    /// <param name="value">待舍入的值</param>
    /// <param name="decimals">保留的小数位数</param>
    /// <returns>舍入后的值</returns>
    public static double Round(double value, int decimals)
    {
        return Math.Round(value, decimals, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    ///     格式化数字，保留指定小数位数
    /// </summary>
    /// <param name="value">数值</param>
    /// <param name="decimals">小数位数</param>
    /// <returns>格式化后的字符串</returns>
    public static string FormatNumber(double value, int decimals)
    {
        return value.ToString($"F{decimals}", CultureInfo.InvariantCulture);
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
