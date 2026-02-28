using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ICSharpCode.SharpZipLib.Core;
using ICSharpCode.SharpZipLib.Zip;

namespace OpenGIS.Utils.Utils;

/// <summary>
///     ZIP 压缩工具类
/// </summary>
public static class ZipUtil
{
    private const int CompressionLevel = 9;
    private const int BufferSize = 4096;

    /// <summary>
    ///     压缩文件夹
    /// </summary>
    /// <param name="folderPath">要压缩的文件夹路径</param>
    /// <param name="zipPath">输出的 ZIP 文件路径</param>
    /// <exception cref="DirectoryNotFoundException">当文件夹不存在时抛出</exception>
    public static void Zip(string folderPath, string zipPath)
    {
        Zip(folderPath, zipPath, Encoding.UTF8);
    }

    /// <summary>
    ///     压缩文件夹（指定编码）
    /// </summary>
    /// <param name="folderPath">要压缩的文件夹路径</param>
    /// <param name="zipPath">输出的 ZIP 文件路径</param>
    /// <param name="encoding">文件名编码</param>
    /// <exception cref="DirectoryNotFoundException">当文件夹不存在时抛出</exception>
    public static void Zip(string folderPath, string zipPath, Encoding encoding)
    {
        if (!Directory.Exists(folderPath))
            throw new DirectoryNotFoundException($"Folder not found: {folderPath}");

        var outputDirectory = Path.GetDirectoryName(zipPath);
        if (!string.IsNullOrEmpty(outputDirectory) && !Directory.Exists(outputDirectory))
            Directory.CreateDirectory(outputDirectory);

        using var fsOut = File.Create(zipPath);
        using var zipStream = new ZipOutputStream(fsOut);

        zipStream.SetLevel(CompressionLevel);
#pragma warning disable CS0618 // ZipStrings.CodePage is obsolete but StringCodec not available on stream in SharpZipLib 1.4.2
        ZipStrings.CodePage = encoding.CodePage;
#pragma warning restore CS0618

        var folderOffset = folderPath.Length + (folderPath.EndsWith(Path.DirectorySeparatorChar.ToString()) ? 0 : 1);
        CompressFolder(folderPath, zipStream, folderOffset);
    }

    /// <summary>
    ///     解压缩文件
    /// </summary>
    /// <param name="zipPath">ZIP 文件路径</param>
    /// <param name="destPath">解压目标目录</param>
    /// <exception cref="FileNotFoundException">当 ZIP 文件不存在时抛出</exception>
    public static void Unzip(string zipPath, string destPath)
    {
        Unzip(zipPath, destPath, Encoding.UTF8);
    }

    /// <summary>
    ///     解压缩文件（指定编码）
    /// </summary>
    /// <param name="zipPath">ZIP 文件路径</param>
    /// <param name="destPath">解压目标目录</param>
    /// <param name="encoding">文件名编码</param>
    /// <exception cref="FileNotFoundException">当 ZIP 文件不存在时抛出</exception>
    public static void Unzip(string zipPath, string destPath, Encoding encoding)
    {
        if (!File.Exists(zipPath))
            throw new FileNotFoundException("ZIP file not found", zipPath);

        if (!Directory.Exists(destPath))
            Directory.CreateDirectory(destPath);

        using var fsInput = File.OpenRead(zipPath);
        using var zipInputStream = new ZipInputStream(fsInput);
#pragma warning disable CS0618 // ZipStrings.CodePage is obsolete but StringCodec not available on stream in SharpZipLib 1.4.2
        ZipStrings.CodePage = encoding.CodePage;
#pragma warning restore CS0618

        var buffer = new byte[BufferSize];
        ZipEntry? theEntry;

        var destFullPath = Path.GetFullPath(destPath);
        if (!destFullPath.EndsWith(Path.DirectorySeparatorChar.ToString()))
            destFullPath += Path.DirectorySeparatorChar;

        while ((theEntry = zipInputStream.GetNextEntry()) != null)
        {
            var directoryName = Path.GetDirectoryName(theEntry.Name);
            var fileName = Path.GetFileName(theEntry.Name);

            // Create directory
            if (!string.IsNullOrEmpty(directoryName))
            {
                var dirPath = Path.GetFullPath(Path.Combine(destPath, directoryName));
                if (!dirPath.StartsWith(destFullPath))
                    throw new IOException($"Entry is outside of the target dir: {theEntry.Name}");
                Directory.CreateDirectory(dirPath);
            }

            if (!string.IsNullOrEmpty(fileName))
            {
                var fullPath = Path.GetFullPath(Path.Combine(destPath, theEntry.Name));
                if (!fullPath.StartsWith(destFullPath))
                    throw new IOException($"Entry is outside of the target dir: {theEntry.Name}");
                using var streamWriter = File.Create(fullPath);
                StreamUtils.Copy(zipInputStream, streamWriter, buffer);
            }
        }
    }

    /// <summary>
    ///     压缩多个文件
    /// </summary>
    /// <param name="filePaths">要压缩的文件路径集合</param>
    /// <param name="zipPath">输出的 ZIP 文件路径</param>
    /// <exception cref="ArgumentNullException">当文件路径集合为 null 时抛出</exception>
    public static void CompressFiles(IEnumerable<string> filePaths, string zipPath)
    {
        if (filePaths == null)
            throw new ArgumentNullException(nameof(filePaths));

        var outputDirectory = Path.GetDirectoryName(zipPath);
        if (!string.IsNullOrEmpty(outputDirectory) && !Directory.Exists(outputDirectory))
            Directory.CreateDirectory(outputDirectory);

        using var fsOut = File.Create(zipPath);
        using var zipStream = new ZipOutputStream(fsOut);

        zipStream.SetLevel(CompressionLevel);
        var buffer = new byte[BufferSize];

        foreach (var filePath in filePaths)
        {
            if (!File.Exists(filePath))
                continue;

            var fi = new FileInfo(filePath);
            var newEntry = new ZipEntry(fi.Name) { DateTime = fi.LastWriteTime, Size = fi.Length };

            zipStream.PutNextEntry(newEntry);

            using var streamReader = File.OpenRead(filePath);
            StreamUtils.Copy(streamReader, zipStream, buffer);

            zipStream.CloseEntry();
        }
    }

    private static void CompressFolder(string path, ZipOutputStream zipStream, int folderOffset)
    {
        var buffer = new byte[BufferSize];
        var files = Directory.GetFiles(path);

        foreach (var filename in files)
        {
            var fi = new FileInfo(filename);
            var entryName = ZipEntry.CleanName(filename.Substring(folderOffset));

            var newEntry = new ZipEntry(entryName) { DateTime = fi.LastWriteTime, Size = fi.Length };

            zipStream.PutNextEntry(newEntry);

            using var streamReader = File.OpenRead(filename);
            StreamUtils.Copy(streamReader, zipStream, buffer);

            zipStream.CloseEntry();
        }

        var folders = Directory.GetDirectories(path);
        foreach (var folder in folders) CompressFolder(folder, zipStream, folderOffset);
    }
}
