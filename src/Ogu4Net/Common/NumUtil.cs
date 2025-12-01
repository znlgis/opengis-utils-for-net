using System.Globalization;

namespace Ogu4Net.Common
{
    /// <summary>
    /// 数字格式化工具类
    /// <para>
    /// 提供数字格式化功能，如去除科学计数法显示等。
    /// 所有方法均为静态方法，无需实例化即可使用。
    /// </para>
    /// </summary>
    public static class NumUtil
    {
        /// <summary>
        /// 去除科学计数法显示
        /// </summary>
        /// <param name="d">数字</param>
        /// <returns>去除科学计数法的字符串</returns>
        public static string GetPlainString(double d)
        {
            // 使用"0.################"格式避免科学计数法
            return d.ToString("0.################", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// 去除科学计数法显示（指定小数位数）
        /// </summary>
        /// <param name="d">数字</param>
        /// <param name="decimalPlaces">小数位数</param>
        /// <returns>去除科学计数法的字符串</returns>
        public static string GetPlainString(double d, int decimalPlaces)
        {
            string format = "0." + new string('#', decimalPlaces);
            return d.ToString(format, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// 去除decimal的科学计数法显示
        /// </summary>
        /// <param name="d">数字</param>
        /// <returns>去除科学计数法的字符串</returns>
        public static string GetPlainString(decimal d)
        {
            return d.ToString("0.################", CultureInfo.InvariantCulture);
        }
    }
}
