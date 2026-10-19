using System.Buffers.Binary;
using System.IO.Compression;
using MindMatch.Application.Services;
using MindMatch.Domain.Entities;

namespace MindMatch.Test.Application;

public sealed class MediaContentValidationTests
{
    [Fact]
    public void ValidPngSignatureAndChunkCrcs_AreAccepted()
    {
        var png = CreatePng();

        var result = MediaService.ValidateContent(png, MediaCategory.ProfilePhoto, "image/png");

        Assert.Equal(("image/png", "png", (int?)1, (int?)1), result);
    }

    [Fact]
    public void JpegWithFrameButNoScan_IsRejected()
    {
        Assert.Throws<ArgumentException>(() => MediaService.ValidateContent(
            CreateJpegWithoutScan(), MediaCategory.ProfilePhoto, "image/jpeg"));
    }

    [Fact]
    public void StructurallyCompleteMediaHeaders_AreAccepted()
    {
        Assert.Equal("audio/wav", MediaService.ValidateContent(CreateWav(), MediaCategory.MessageVoice, "audio/wav").ContentType);
        Assert.Equal("audio/mpeg", MediaService.ValidateContent(CreateMp3Frame(), MediaCategory.MessageVoice, "audio/mpeg").ContentType);
        Assert.Equal("audio/ogg", MediaService.ValidateContent(CreateOggPage(), MediaCategory.MessageVoice, "audio/ogg").ContentType);
        Assert.Equal("video/mp4", MediaService.ValidateContent(CreateMp4(), MediaCategory.MessageVideo, "video/mp4").ContentType);
    }

    [Theory]
    [MemberData(nameof(TruncatedSignatures))]
    public void TruncatedMagicBytes_AreRejected(byte[] content, MediaCategory category, string contentType)
    {
        Assert.Throws<ArgumentException>(() => MediaService.ValidateContent(content, category, contentType));
    }

    public static IEnumerable<object[]> TruncatedSignatures()
    {
        yield return ["RIFFWAVE"u8.ToArray(), MediaCategory.MessageVoice, "audio/wav"];
        yield return ["OggS"u8.ToArray(), MediaCategory.MessageVoice, "audio/ogg"];
        yield return ["ID3"u8.ToArray(), MediaCategory.MessageVoice, "audio/mpeg"];
        yield return [new byte[] { 0x1A, 0x45, 0xDF, 0xA3 }, MediaCategory.MessageVoice, "audio/webm"];
        yield return [BuildShortMp4Header(), MediaCategory.MessageVideo, "video/mp4"];
        yield return [BuildShortPngHeader(), MediaCategory.ProfilePhoto, "image/png"];
        yield return [new byte[] { 0xFF, 0xD8, 0xFF, 0xD9 }, MediaCategory.ProfilePhoto, "image/jpeg"];
    }

    private static byte[] CreateWav()
    {
        var bytes = new byte[46];
        "RIFF"u8.CopyTo(bytes);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4, 4), (uint)(bytes.Length - 8));
        "WAVEfmt "u8.CopyTo(bytes.AsSpan(8));
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(16, 4), 16);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(20, 2), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(22, 2), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(24, 4), 44_100);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(28, 4), 88_200);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(32, 2), 2);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(34, 2), 16);
        "data"u8.CopyTo(bytes.AsSpan(36));
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(40, 4), 2);
        bytes[44] = 0x12;
        bytes[45] = 0x34;
        return bytes;
    }

    private static byte[] CreateMp3Frame()
    {
        var bytes = new byte[417];
        bytes[0] = 0xFF; bytes[1] = 0xFB; bytes[2] = 0x90; bytes[3] = 0x64;
        return bytes;
    }

    private static byte[] CreateOggPage()
    {
        var bytes = new byte[29];
        "OggS"u8.CopyTo(bytes);
        bytes[26] = 1;
        bytes[27] = 1;
        bytes[28] = 0x42;
        return bytes;
    }

    private static byte[] CreateMp4()
    {
        var bytes = new byte[41];
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(0, 4), 16);
        "ftypisom"u8.CopyTo(bytes.AsSpan(4));
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(12, 4), 0);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(16, 4), 9);
        "mdat"u8.CopyTo(bytes.AsSpan(20));
        bytes[24] = 0x01;
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(25, 4), 16);
        "moov"u8.CopyTo(bytes.AsSpan(29));
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(33, 4), 8);
        "mvhd"u8.CopyTo(bytes.AsSpan(37));
        return bytes;
    }

    private static byte[] CreatePng()
    {
        using var output = new MemoryStream();
        output.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        var header = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(0, 4), 1);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4, 4), 1);
        header[8] = 8;
        header[9] = 6;
        WritePngChunk(output, "IHDR"u8, header);
        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
            zlib.Write(new byte[] { 0, 255, 0, 0, 255 });
        WritePngChunk(output, "IDAT"u8, compressed.ToArray());
        WritePngChunk(output, "IEND"u8, []);
        return output.ToArray();
    }

    private static void WritePngChunk(Stream output, ReadOnlySpan<byte> type, byte[] data)
    {
        Span<byte> number = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(number, data.Length);
        output.Write(number);
        output.Write(type);
        output.Write(data);
        var crcBytes = new byte[type.Length + data.Length];
        type.CopyTo(crcBytes);
        data.CopyTo(crcBytes, type.Length);
        BinaryPrimitives.WriteUInt32BigEndian(number, PngCrc(crcBytes));
        output.Write(number);
    }

    private static uint PngCrc(ReadOnlySpan<byte> bytes)
    {
        uint crc = 0xFFFFFFFF;
        foreach (var value in bytes)
        {
            crc ^= value;
            for (var bit = 0; bit < 8; bit++)
                crc = (crc & 1) == 1 ? 0xEDB88320u ^ (crc >> 1) : crc >> 1;
        }
        return ~crc;
    }

    private static byte[] BuildShortMp4Header()
    {
        var bytes = new byte[16];
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(0, 4), 16);
        "ftypisom"u8.CopyTo(bytes.AsSpan(4));
        return bytes;
    }

    private static byte[] BuildShortPngHeader()
    {
        var bytes = new byte[33];
        new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }.CopyTo(bytes, 0);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(8, 4), 13);
        "IHDR"u8.CopyTo(bytes.AsSpan(12));
        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(16, 4), 1);
        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(20, 4), 1);
        return bytes;
    }

    private static byte[] CreateJpegWithoutScan()
    {
        var bytes = new byte[17];
        bytes[0] = 0xFF; bytes[1] = 0xD8;
        bytes[2] = 0xFF; bytes[3] = 0xC0;
        bytes[4] = 0; bytes[5] = 11;
        bytes[6] = 8;
        bytes[7] = 0; bytes[8] = 1;
        bytes[9] = 0; bytes[10] = 1;
        bytes[11] = 1;
        bytes[12] = 1; bytes[13] = 0x11; bytes[14] = 0;
        bytes[15] = 0xFF; bytes[16] = 0xD9;
        return bytes;
    }
}
