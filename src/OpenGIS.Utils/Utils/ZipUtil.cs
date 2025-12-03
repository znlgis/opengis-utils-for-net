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
    /// <summary>
    ///     压缩文件夹
    /// </summary>
    public static void Zip(string folderPath, string zipPath)
    {
        Zip(folderPath, zipPath, Encoding.UTF8);
    }

    /// <summary>
    ///     压缩文件夹（指定编码）
    /// </summary>
    public static void Zip(string folderPath, string zipPath, Encoding encoding)
    {
        if (!Directory.Exists(folderPath))
            throw new DirectoryNotFoundException($"Folder not found: {folderPath}");

        var outputDirector = Path.GetDirectoryName(zipPath);
        if (!string.IsNullOrEmpty(outputDirector) && !Directory.Exists(outputDirector))
            Directory.CreateDirectory(outputDirector);

        using (var fsOut = File.Create(zipPath))
        using (var zipStream = new ZipOutputStream(fsOut))
        {
            zipStream.SetLevel(9); // 0-9, 9 being the highest compression

            // 设置编码
            ZipStrings.CodePage = encoding.CodePage;

            var folderOffset =
                folderPath.Length + (folderPath.EndsWith(Path.DirectorySeparatorChar.ToString()) ? 0 : 1);
            CompressFolder(folderPath, zipStream, folderOffset);

            zipStream.IsStreamOwner = true;
            zipStream.Close();
        }
    }

    /// <summary>
    ///     解压缩文件
    /// </summary>
    public static void Unzip(string zipPath, string destPath)
    {
        Unzip(zipPath, destPath, Encoding.UTF8);
    }

    /// <summary>
    ///     解压缩文件（指定编码）
    /// </summary>
    public static void Unzip(string zipPath, string destPath, Encoding encoding)
    {
        if (!File.Exists(zipPath))
            throw new FileNotFoundException("ZIP file not found", zipPath);

        if (!Directory.Exists(destPath)) Directory.CreateDirectory(destPath);

        // 设置编码
        ZipStrings.CodePage = encoding.CodePage;

        using (var fsInput = File.OpenRead(zipPath))
        using (var zipInputStream = new ZipInputStream(fsInput))
        {
            ZipEntry? theEntry;
            while ((theEntry = zipInputStream.GetNextEntry()) != null)
            {
                var directoryName = Path.GetDirectoryName(theEntry.Name);
                var fileName = Path.GetFileName(theEntry.Name);

                // Create directory
                if (!string.IsNullOrEmpty(directoryName))
                {
                    var dirPath = Path.Combine(destPath, directoryName);
                    Directory.CreateDirectory(dirPath);
                }

                if (!string.IsNullOrEmpty(fileName))
                {
                    var fullPath = Path.Combine(destPath, theEntry.Name);
                    using (var streamWriter = File.Create(fullPath))
                    {
                        StreamUtils.Copy(zipInputStream, streamWriter, new byte[4096]);
                    }
                }
            }
        }
    }

    /// <summary>
    ///     压缩多个文件
    /// </summary>
    public static void CompressFiles(IEnumerable<string> filePaths, string zipPath)
    {
        if (filePaths == null)
            throw new ArgumentNullException(nameof(filePaths));

        var outputDirectory = Path.GetDirectoryName(zipPath);
        if (!string.IsNullOrEmpty(outputDirectory) && !Directory.Exists(outputDirectory))
            Directory.CreateDirectory(outputDirectory);

        using (var fsOut = File.Create(zipPath))
        using (var zipStream = new ZipOutputStream(fsOut))
        {
            zipStream.SetLevel(9);

            foreach (var filePath in filePaths)
            {
                if (!File.Exists(filePath))
                    continue;

                var fi = new FileInfo(filePath);
                var entryName = fi.Name;

                var newEntry = new ZipEntry(entryName) { DateTime = fi.LastWriteTime, Size = fi.Length };

                zipStream.PutNextEntry(newEntry);

                var buffer = new byte[4096];
                using (var streamReader = File.OpenRead(filePath))
                {
                    StreamUtils.Copy(streamReader, zipStream, buffer);
                }

                zipStream.CloseEntry();
            }

            zipStream.IsStreamOwner = true;
            zipStream.Close();
        }
    }

    private static void CompressFolder(string path, ZipOutputStream zipStream, int folderOffset)
    {
        var files = Directory.GetFiles(path);

        foreach (var filename in files)
        {
            var fi = new FileInfo(filename);
            var entryName = filename.Substring(folderOffset);
            entryName = ZipEntry.CleanName(entryName);

            var newEntry = new ZipEntry(entryName) { DateTime = fi.LastWriteTime, Size = fi.Length };

            zipStream.PutNextEntry(newEntry);

            var buffer = new byte[4096];
            using (var streamReader = File.OpenRead(filename))
            {
                StreamUtils.Copy(streamReader, zipStream, buffer);
            }

            zipStream.CloseEntry();
        }

        var folders = Directory.GetDirectories(path);
        foreach (var folder in folders) CompressFolder(folder, zipStream, folderOffset);
    }
}
