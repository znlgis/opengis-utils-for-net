using System.IO;
using System.Text;
using UtfUnknown;

namespace Ogu4Net.Common
{
    /// <summary>
    /// 文件编码检测工具类
    /// <para>
    /// 提供文件编码自动检测功能，支持UTF-8、GBK、GB2312、GB18030等常见编码。
    /// 所有方法均为静态方法，无需实例化即可使用。
    /// </para>
    /// </summary>
    public static class EncodingUtil
    {
        // 注册编码提供程序以支持GBK等编码
        static EncodingUtil()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }

        /// <summary>
        /// 获取文件编码，默认返回UTF-8
        /// </summary>
        /// <param name="file">文件</param>
        /// <returns>编码</returns>
        public static Encoding GetFileEncoding(FileInfo file)
        {
            return GetFileEncoding(file.FullName);
        }

        /// <summary>
        /// 获取文件编码，默认返回UTF-8
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <returns>编码</returns>
        public static Encoding GetFileEncoding(string filePath)
        {
            if (!File.Exists(filePath))
            {
                return Encoding.UTF8;
            }

            try
            {
                var result = CharsetDetector.DetectFromFile(filePath);
                if (result?.Detected != null)
                {
                    var encodingName = result.Detected.EncodingName;
                    if (!string.IsNullOrEmpty(encodingName))
                    {
                        try
                        {
                            return Encoding.GetEncoding(encodingName);
                        }
                        catch
                        {
                            // 如果编码名称无法识别，尝试一些常见的映射
                            if (encodingName.Contains("GB"))
                            {
                                return Encoding.GetEncoding("GB2312");
                            }
                        }
                    }
                }
            }
            catch
            {
                // 忽略检测错误
            }

            return Encoding.UTF8;
        }

        /// <summary>
        /// 获取字节数组的编码，默认返回UTF-8
        /// </summary>
        /// <param name="data">字节数组</param>
        /// <returns>编码</returns>
        public static Encoding GetEncoding(byte[] data)
        {
            if (data == null || data.Length == 0)
            {
                return Encoding.UTF8;
            }

            try
            {
                var result = CharsetDetector.DetectFromBytes(data);
                if (result?.Detected != null)
                {
                    var encodingName = result.Detected.EncodingName;
                    if (!string.IsNullOrEmpty(encodingName))
                    {
                        try
                        {
                            return Encoding.GetEncoding(encodingName);
                        }
                        catch
                        {
                            // 如果编码名称无法识别，返回默认
                        }
                    }
                }
            }
            catch
            {
                // 忽略检测错误
            }

            return Encoding.UTF8;
        }

        /// <summary>
        /// 获取GBK编码
        /// </summary>
        /// <returns>GBK编码</returns>
        public static Encoding GetGbkEncoding()
        {
            return Encoding.GetEncoding("GBK");
        }

        /// <summary>
        /// 获取GB2312编码
        /// </summary>
        /// <returns>GB2312编码</returns>
        public static Encoding GetGb2312Encoding()
        {
            return Encoding.GetEncoding("GB2312");
        }
    }
}
