using System;
using System.IO;
using System.Text;

namespace OpenGIS.Utils.Utils;

/// <summary>
///     编码检测工具类
/// </summary>
public static class EncodingUtil
{
    static EncodingUtil()
    {
        // 注册编码提供程序以支持 GBK、GB2312 等
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    /// <summary>
    ///     检测文件编码
    /// </summary>
    /// <param name="filePath">文件路径</param>
    /// <returns>检测到的编码</returns>
    /// <exception cref="FileNotFoundException">当文件不存在时抛出</exception>
    /// <example>
    ///     <code>
    /// var encoding = EncodingUtil.GetFileEncoding("data.txt");
    /// var content = File.ReadAllText("data.txt", encoding);
    /// </code>
    /// </example>
    public static Encoding GetFileEncoding(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("File not found", filePath);

        using var stream = File.OpenRead(filePath);
        return GetFileEncoding(stream);
    }

    /// <summary>
    ///     检测流编码
    /// </summary>
    /// <param name="stream">输入流</param>
    /// <returns>检测到的编码</returns>
    /// <exception cref="ArgumentNullException">当流为 null 时抛出</exception>
    /// <remarks>此方法会重置流的位置到开头</remarks>
    public static Encoding GetFileEncoding(Stream stream)
    {
        if (stream == null)
            throw new ArgumentNullException(nameof(stream));

        var bufferSize = (int)Math.Min(4096, stream.Length);
        var buffer = new byte[bufferSize];
        var bytesRead = stream.Read(buffer, 0, bufferSize);
        stream.Position = 0; // 重置流位置

        return DetectEncoding(buffer, bytesRead);
    }

    /// <summary>
    ///     检测字节数组编码
    /// </summary>
    /// <param name="buffer">字节数组</param>
    /// <returns>检测到的编码，默认返回 UTF-8</returns>
    /// <remarks>支持检测 UTF-8、UTF-16 LE/BE、GBK/GB2312 等编码</remarks>
    public static Encoding DetectEncoding(byte[] buffer)
    {
        return DetectEncoding(buffer, buffer?.Length ?? 0);
    }

    /// <summary>
    ///     检测字节数组编码
    /// </summary>
    /// <param name="buffer">字节数组</param>
    /// <param name="length">要检测的字节长度</param>
    /// <returns>检测到的编码，默认返回 UTF-8</returns>
    /// <remarks>支持检测 UTF-8、UTF-16 LE/BE、GBK/GB2312 等编码</remarks>
    private static Encoding DetectEncoding(byte[] buffer, int length)
    {
        if (buffer == null || length == 0)
            return Encoding.UTF8;

        // 检测 BOM
        if (length >= 3 && buffer[0] == 0xEF && buffer[1] == 0xBB && buffer[2] == 0xBF)
            return Encoding.UTF8;

        if (length >= 2)
        {
            if (buffer[0] == 0xFF && buffer[1] == 0xFE)
                return Encoding.Unicode; // UTF-16 LE

            if (buffer[0] == 0xFE && buffer[1] == 0xFF)
                return Encoding.BigEndianUnicode; // UTF-16 BE
        }

        // 尝试检测 UTF-8（无 BOM）
        if (IsUTF8(buffer, length))
            return Encoding.UTF8;

        // 尝试检测 GBK/GB2312
        if (IsGBK(buffer, length))
            return Encoding.GetEncoding("GBK");

        // 默认返回系统默认编码
        return Encoding.Default;
    }

    /// <summary>
    ///     转换文件编码
    /// </summary>
    /// <param name="filePath">文件路径</param>
    /// <param name="targetEncoding">目标编码</param>
    /// <exception cref="FileNotFoundException">当文件不存在时抛出</exception>
    public static void ConvertFileEncoding(string filePath, Encoding targetEncoding)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("File not found", filePath);

        var sourceEncoding = GetFileEncoding(filePath);
        var content = File.ReadAllText(filePath, sourceEncoding);
        File.WriteAllText(filePath, content, targetEncoding);
    }

    /// <summary>
    ///     判断是否为 UTF-8 编码
    /// </summary>
    private static bool IsUTF8(byte[] buffer, int length)
    {
        int i = 0;
        while (i < length)
        {
            if (buffer[i] <= 0x7F)
            {
                i++;
                continue;
            }

            int count;
            if ((buffer[i] & 0xE0) == 0xC0)
                count = 1;
            else if ((buffer[i] & 0xF0) == 0xE0)
                count = 2;
            else if ((buffer[i] & 0xF8) == 0xF0)
                count = 3;
            else
                return false;

            i++;
            for (int j = 0; j < count; j++)
            {
                if (i >= length || (buffer[i] & 0xC0) != 0x80)
                    return false;
                i++;
            }
        }

        return true;
    }

    /// <summary>
    ///     判断是否为 GBK 编码
    /// </summary>
    private static bool IsGBK(byte[] buffer, int length)
    {
        for (int i = 0; i < length - 1; i++)
            if (buffer[i] >= 0x81 && buffer[i] <= 0xFE)
            {
                if ((buffer[i + 1] >= 0x40 && buffer[i + 1] <= 0x7E) ||
                    (buffer[i + 1] >= 0x80 && buffer[i + 1] <= 0xFE))
                    i++; // Skip next byte as it's part of the character
                else
                    return false;
            }

        return true;
    }
}
