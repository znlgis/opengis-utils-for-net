using System;
using System.IO;
using System.Linq;
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
    public static Encoding GetFileEncoding(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("File not found", filePath);

        using (var stream = File.OpenRead(filePath))
        {
            return GetFileEncoding(stream);
        }
    }

    /// <summary>
    ///     检测流编码
    /// </summary>
    public static Encoding GetFileEncoding(Stream stream)
    {
        if (stream == null)
            throw new ArgumentNullException(nameof(stream));

        var buffer = new byte[Math.Min(4096, stream.Length)];
        var bytesRead = stream.Read(buffer, 0, buffer.Length);
        stream.Position = 0; // 重置流位置

        return DetectEncoding(buffer.Take(bytesRead).ToArray());
    }

    /// <summary>
    ///     检测字节数组编码
    /// </summary>
    public static Encoding DetectEncoding(byte[] buffer)
    {
        if (buffer == null || buffer.Length == 0)
            return Encoding.UTF8;

        // 检测 BOM
        if (buffer.Length >= 3 && buffer[0] == 0xEF && buffer[1] == 0xBB && buffer[2] == 0xBF)
            return Encoding.UTF8;

        if (buffer.Length >= 2)
        {
            if (buffer[0] == 0xFF && buffer[1] == 0xFE)
                return Encoding.Unicode; // UTF-16 LE

            if (buffer[0] == 0xFE && buffer[1] == 0xFF)
                return Encoding.BigEndianUnicode; // UTF-16 BE
        }

        // 尝试检测 UTF-8（无 BOM）
        if (IsUTF8(buffer))
            return Encoding.UTF8;

        // 尝试检测 GBK/GB2312
        if (IsGBK(buffer))
            return Encoding.GetEncoding("GBK");

        // 默认返回系统默认编码
        return Encoding.Default;
    }

    /// <summary>
    ///     转换文件编码
    /// </summary>
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
    private static bool IsUTF8(byte[] buffer)
    {
        int i = 0;
        while (i < buffer.Length)
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
                if (i >= buffer.Length || (buffer[i] & 0xC0) != 0x80)
                    return false;
                i++;
            }
        }

        return true;
    }

    /// <summary>
    ///     判断是否为 GBK 编码
    /// </summary>
    private static bool IsGBK(byte[] buffer)
    {
        for (int i = 0; i < buffer.Length - 1; i++)
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
