using FluentAssertions;
using OpenGIS.Utils.Utils;

namespace OpenGIS.Utils.Tests;

public class ZipUtilTests : IDisposable
{
    private readonly string _testDir;

    public ZipUtilTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "ZipUtilTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, true);
    }

    [Fact]
    public void Zip_Unzip_RoundTrip()
    {
        // Arrange
        var sourceDir = Path.Combine(_testDir, "source");
        Directory.CreateDirectory(sourceDir);
        File.WriteAllText(Path.Combine(sourceDir, "test.txt"), "Hello, World!");
        File.WriteAllText(Path.Combine(sourceDir, "data.csv"), "a,b,c\n1,2,3");

        var zipPath = Path.Combine(_testDir, "output.zip");
        var destDir = Path.Combine(_testDir, "dest");

        // Act
        ZipUtil.Zip(sourceDir, zipPath);
        ZipUtil.Unzip(zipPath, destDir);

        // Assert
        File.Exists(zipPath).Should().BeTrue();
        File.Exists(Path.Combine(destDir, "test.txt")).Should().BeTrue();
        File.Exists(Path.Combine(destDir, "data.csv")).Should().BeTrue();
        File.ReadAllText(Path.Combine(destDir, "test.txt")).Should().Be("Hello, World!");
        File.ReadAllText(Path.Combine(destDir, "data.csv")).Should().Be("a,b,c\n1,2,3");
    }

    [Fact]
    public void CompressFiles_WithMultipleFiles()
    {
        // Arrange
        var file1 = Path.Combine(_testDir, "file1.txt");
        var file2 = Path.Combine(_testDir, "file2.txt");
        File.WriteAllText(file1, "content1");
        File.WriteAllText(file2, "content2");

        var zipPath = Path.Combine(_testDir, "compressed.zip");

        // Act
        ZipUtil.CompressFiles(new[] { file1, file2 }, zipPath);

        // Assert
        File.Exists(zipPath).Should().BeTrue();

        // Verify by unzipping
        var destDir = Path.Combine(_testDir, "extracted");
        ZipUtil.Unzip(zipPath, destDir);
        File.Exists(Path.Combine(destDir, "file1.txt")).Should().BeTrue();
        File.Exists(Path.Combine(destDir, "file2.txt")).Should().BeTrue();
    }

    [Fact]
    public void Zip_ThrowsOnNonexistentFolder()
    {
        var act = () => ZipUtil.Zip("/nonexistent/folder", Path.Combine(_testDir, "out.zip"));

        act.Should().Throw<DirectoryNotFoundException>();
    }

    [Fact]
    public void Unzip_ThrowsOnNonexistentFile()
    {
        var act = () => ZipUtil.Unzip("/nonexistent/file.zip", _testDir);

        act.Should().Throw<FileNotFoundException>();
    }

    [Fact]
    public void Unzip_PathTraversalProtection()
    {
        // Create a zip with a path traversal entry using SharpZipLib directly
        var zipPath = Path.Combine(_testDir, "malicious.zip");
        using (var fsOut = File.Create(zipPath))
        using (var zipStream = new ICSharpCode.SharpZipLib.Zip.ZipOutputStream(fsOut))
        {
            zipStream.SetLevel(5);
            var entry = new ICSharpCode.SharpZipLib.Zip.ZipEntry("../../evil.txt")
            {
                DateTime = DateTime.Now,
                Size = 4
            };
            zipStream.PutNextEntry(entry);
            var buffer = System.Text.Encoding.UTF8.GetBytes("evil");
            zipStream.Write(buffer, 0, buffer.Length);
            zipStream.CloseEntry();
        }

        var destDir = Path.Combine(_testDir, "safe_dest");

        // Act
        var act = () => ZipUtil.Unzip(zipPath, destDir);

        // Assert - should throw IOException for path traversal
        act.Should().Throw<IOException>()
            .WithMessage("*outside*");
    }

    [Fact]
    public void Zip_WithSubdirectories()
    {
        // Arrange
        var sourceDir = Path.Combine(_testDir, "nested_source");
        var subDir = Path.Combine(sourceDir, "sub");
        Directory.CreateDirectory(subDir);
        File.WriteAllText(Path.Combine(sourceDir, "root.txt"), "root");
        File.WriteAllText(Path.Combine(subDir, "nested.txt"), "nested");

        var zipPath = Path.Combine(_testDir, "nested.zip");
        var destDir = Path.Combine(_testDir, "nested_dest");

        // Act
        ZipUtil.Zip(sourceDir, zipPath);
        ZipUtil.Unzip(zipPath, destDir);

        // Assert
        File.Exists(Path.Combine(destDir, "root.txt")).Should().BeTrue();
        File.Exists(Path.Combine(destDir, "sub", "nested.txt")).Should().BeTrue();
    }
}
