using System.IO;
using System.Text;
using ICSharpCode.SharpZipLib.Zip;

namespace Ogu4Net.Common
{
    /// <summary>
    /// ZIP压缩工具类
    /// <para>
    /// 提供ZIP文件的压缩和解压功能，支持自定义编码。
    /// 所有方法均为静态方法，无需实例化即可使用。
    /// </para>
    /// </summary>
    public static class ZipUtil
    {
        // 注册编码提供程序以支持GBK等编码
        static ZipUtil()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }

        /// <summary>
        /// 压缩文件夹，默认GBK编码
        /// </summary>
        /// <param name="folderToAdd">要压缩的文件夹</param>
        /// <param name="destZipPath">压缩包路径</param>
        public static void Zip(DirectoryInfo folderToAdd, string destZipPath)
        {
            Zip(folderToAdd, destZipPath, Encoding.GetEncoding("GBK"));
        }

        /// <summary>
        /// 压缩文件夹
        /// </summary>
        /// <param name="folderToAdd">要压缩的文件夹</param>
        /// <param name="destZipPath">压缩包路径</param>
        /// <param name="encoding">编码</param>
        public static void Zip(DirectoryInfo folderToAdd, string destZipPath, Encoding encoding)
        {
            Zip(folderToAdd.FullName, destZipPath, encoding);
        }

        /// <summary>
        /// 压缩文件夹，默认GBK编码
        /// </summary>
        /// <param name="folderPath">要压缩的文件夹路径</param>
        /// <param name="destZipPath">压缩包路径</param>
        public static void Zip(string folderPath, string destZipPath)
        {
            Zip(folderPath, destZipPath, Encoding.GetEncoding("GBK"));
        }

        /// <summary>
        /// 压缩文件夹
        /// </summary>
        /// <param name="folderPath">要压缩的文件夹路径</param>
        /// <param name="destZipPath">压缩包路径</param>
        /// <param name="encoding">编码</param>
        public static void Zip(string folderPath, string destZipPath, Encoding encoding)
        {
            var fastZip = new FastZip
            {
                CreateEmptyDirectories = true
            };
            ZipStrings.CodePage = encoding.CodePage;
            fastZip.CreateZip(destZipPath, folderPath, true, null);
        }

        /// <summary>
        /// 压缩单个文件，默认GBK编码
        /// </summary>
        /// <param name="filePath">要压缩的文件路径</param>
        /// <param name="destZipPath">压缩包路径</param>
        public static void ZipFile(string filePath, string destZipPath)
        {
            ZipFile(filePath, destZipPath, Encoding.GetEncoding("GBK"));
        }

        /// <summary>
        /// 压缩单个文件
        /// </summary>
        /// <param name="filePath">要压缩的文件路径</param>
        /// <param name="destZipPath">压缩包路径</param>
        /// <param name="encoding">编码</param>
        public static void ZipFile(string filePath, string destZipPath, Encoding encoding)
        {
            ZipStrings.CodePage = encoding.CodePage;
            using (var fs = new FileStream(destZipPath, FileMode.Create))
            using (var zipStream = new ZipOutputStream(fs))
            {
                var fileInfo = new FileInfo(filePath);
                var entry = new ZipEntry(fileInfo.Name)
                {
                    DateTime = fileInfo.LastWriteTime,
                    Size = fileInfo.Length
                };

                zipStream.PutNextEntry(entry);

                using (var inputStream = File.OpenRead(filePath))
                {
                    inputStream.CopyTo(zipStream);
                }

                zipStream.CloseEntry();
            }
        }

        /// <summary>
        /// 解压文件，默认GBK编码
        /// </summary>
        /// <param name="zipPath">压缩包路径</param>
        /// <param name="destPath">解压路径</param>
        public static void Unzip(string zipPath, string destPath)
        {
            Unzip(zipPath, destPath, Encoding.GetEncoding("GBK"));
        }

        /// <summary>
        /// 解压文件
        /// </summary>
        /// <param name="zipPath">压缩包路径</param>
        /// <param name="destPath">解压路径</param>
        /// <param name="encoding">编码</param>
        public static void Unzip(string zipPath, string destPath, Encoding encoding)
        {
            var fastZip = new FastZip();
            ZipStrings.CodePage = encoding.CodePage;
            fastZip.ExtractZip(zipPath, destPath, null);
        }

        /// <summary>
        /// 解压文件，默认GBK编码
        /// </summary>
        /// <param name="zipFile">压缩包文件</param>
        /// <param name="destPath">解压路径</param>
        public static void Unzip(FileInfo zipFile, string destPath)
        {
            Unzip(zipFile.FullName, destPath, Encoding.GetEncoding("GBK"));
        }

        /// <summary>
        /// 解压文件
        /// </summary>
        /// <param name="zipFile">压缩包文件</param>
        /// <param name="destPath">解压路径</param>
        /// <param name="encoding">编码</param>
        public static void Unzip(FileInfo zipFile, string destPath, Encoding encoding)
        {
            Unzip(zipFile.FullName, destPath, encoding);
        }
    }
}
