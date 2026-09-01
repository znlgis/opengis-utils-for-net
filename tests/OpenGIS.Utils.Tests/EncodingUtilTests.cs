using System.Text;
using FluentAssertions;
using OpenGIS.Utils.Utils;

namespace OpenGIS.Utils.Tests;

public class EncodingUtilTests : IDisposable
{
    private readonly string _testDir;

    public EncodingUtilTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "EncodingUtilTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, true);
    }

    [Fact]
    public void DetectEncoding_DetectsUtf8Bom()
    {
        var bytes = new byte[] { 0xEF, 0xBB, 0xBF, 0x41, 0x42 };

        EncodingUtil.DetectEncoding(bytes).Should().Be(Encoding.UTF8);
    }

    [Fact]
    public void DetectEncoding_DetectsUtf16LeBom()
    {
        var bytes = new byte[] { 0xFF, 0xFE, 0x41, 0x00 };

        EncodingUtil.DetectEncoding(bytes).Should().Be(Encoding.Unicode);
    }

    [Fact]
    public void DetectEncoding_DetectsUtf16BeBom()
    {
        var bytes = new byte[] { 0xFE, 0xFF, 0x00, 0x41 };

        EncodingUtil.DetectEncoding(bytes).Should().Be(Encoding.BigEndianUnicode);
    }

    [Fact]
    public void DetectEncoding_DetectsUtf8WithoutBom()
    {
        var bytes = Encoding.UTF8.GetBytes("Hello, 世界");

        EncodingUtil.DetectEncoding(bytes).Should().Be(Encoding.UTF8);
    }

    [Fact]
    public void DetectEncoding_DetectsGbk()
    {
        var gbk = Encoding.GetEncoding("GBK");
        var bytes = gbk.GetBytes("中文");

        EncodingUtil.DetectEncoding(bytes).Should().Be(gbk);
    }

    [Fact]
    public void DetectEncoding_EmptyBuffer_ReturnsUtf8()
    {
        EncodingUtil.DetectEncoding(Array.Empty<byte>()).Should().Be(Encoding.UTF8);
    }

    [Fact]
    public void DetectEncoding_NullBuffer_ReturnsUtf8()
    {
        EncodingUtil.DetectEncoding(null!).Should().Be(Encoding.UTF8);
    }

    [Fact]
    public void GetFileEncoding_ReadsFromFile()
    {
        var path = Path.Combine(_testDir, "utf8.txt");
        File.WriteAllText(path, "Hello", Encoding.UTF8);

        EncodingUtil.GetFileEncoding(path).Should().Be(Encoding.UTF8);
    }

    [Fact]
    public void GetFileEncoding_ThrowsWhenFileMissing()
    {
        var act = () => EncodingUtil.GetFileEncoding(Path.Combine(_testDir, "missing.txt"));

        act.Should().Throw<FileNotFoundException>();
    }

    [Fact]
    public void GetFileEncoding_ResetsStreamPosition()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("Hello"));

        EncodingUtil.GetFileEncoding(stream);

        stream.Position.Should().Be(0);
    }

    [Fact]
    public void GetFileEncoding_NullStream_Throws()
    {
        var act = () => EncodingUtil.GetFileEncoding((Stream)null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ConvertFileEncoding_ConvertsToTarget()
    {
        var path = Path.Combine(_testDir, "convert.txt");
        File.WriteAllText(path, "中文", Encoding.UTF8);

        EncodingUtil.ConvertFileEncoding(path, Encoding.GetEncoding("GBK"));

        var bytes = File.ReadAllBytes(path);
        EncodingUtil.DetectEncoding(bytes).Should().Be(Encoding.GetEncoding("GBK"));
        File.ReadAllText(path, Encoding.GetEncoding("GBK")).Should().Be("中文");
    }

    [Fact]
    public void ConvertFileEncoding_ThrowsWhenFileMissing()
    {
        var act = () => EncodingUtil.ConvertFileEncoding(Path.Combine(_testDir, "missing.txt"), Encoding.UTF8);

        act.Should().Throw<FileNotFoundException>();
    }
}
