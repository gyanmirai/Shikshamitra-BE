using System;
using System.Buffers.Binary;
using System.IO;
using SkiaSharp;
using Volo.Abp;

namespace ShikshaMitra.Students.Portal;

/// <summary>Bounds pixel allocation before invoking the cross-platform pixel decoder.</summary>
internal static class StudentPhotoImageValidation
{
	internal static void EnsureDecodable(byte[] bytes, bool png)
	{
		try
		{
			var (width, height) = png ? ReadPng(bytes) : ReadJpeg(bytes);
			if (width < 1 || height < 1 || width > 4096 || height > 4096) throw new InvalidDataException();
			using var input = new MemoryStream(bytes, writable: false);
			using var codec = SKCodec.Create(input) ?? throw new InvalidDataException();
			if (codec.Info.Width != width || codec.Info.Height != height || codec.EncodedFormat != (png ? SKEncodedImageFormat.Png : SKEncodedImageFormat.Jpeg))
				throw new InvalidDataException();
			var info = new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
			using var pixels = new SKBitmap(info);
			if (pixels.GetPixels() == IntPtr.Zero || codec.GetPixels(info, pixels.GetPixels()) != SKCodecResult.Success)
				throw new InvalidDataException();
		}
		catch (Exception exception) when (exception is not OutOfMemoryException)
		{ throw new BusinessException("Students:Records:InvalidPhoto"); }
	}

	private static (int Width, int Height) ReadPng(ReadOnlySpan<byte> bytes)
	{
		if (bytes.Length < 45 || !bytes[..8].SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })) throw new InvalidDataException();
		var offset = 8;
		var width = 0;
		var height = 0;
		var imageData = false;
		while (offset <= bytes.Length - 12)
		{
			var length = BinaryPrimitives.ReadUInt32BigEndian(bytes.Slice(offset, 4));
			if (length > bytes.Length - offset - 12) throw new InvalidDataException();
			var count = (int)length;
			var type = bytes.Slice(offset + 4, 4);
			if (Crc32(bytes.Slice(offset + 4, count + 4)) != BinaryPrimitives.ReadUInt32BigEndian(bytes.Slice(offset + count + 8, 4))) throw new InvalidDataException();
			if (offset == 8)
			{
				if (count != 13 || !type.SequenceEqual("IHDR"u8)) throw new InvalidDataException();
				width = checked((int)BinaryPrimitives.ReadUInt32BigEndian(bytes.Slice(offset + 8, 4)));
				height = checked((int)BinaryPrimitives.ReadUInt32BigEndian(bytes.Slice(offset + 12, 4)));
				if (width < 1 || height < 1 || width > 4096 || height > 4096) throw new InvalidDataException();
				var bitDepth = bytes[offset + 16];
				var colorType = bytes[offset + 17];
				if (colorType is not (0 or 2 or 3 or 4 or 6)) throw new InvalidDataException();
				if (colorType == 3 ? bitDepth is not (1 or 2 or 4 or 8) : colorType == 0 ? bitDepth is not (1 or 2 or 4 or 8 or 16) : bitDepth is not (8 or 16))
					throw new InvalidDataException();
				if (bytes[offset + 18] != 0 || bytes[offset + 19] != 0 || bytes[offset + 20] > 1) throw new InvalidDataException();
			}
			else if (type.SequenceEqual("IHDR"u8) || type.SequenceEqual("acTL"u8)) throw new InvalidDataException();
			if (type.SequenceEqual("IDAT"u8) && count > 0)
			{
				imageData = true;
			}
			offset += count + 12;
			if (type.SequenceEqual("IEND"u8))
			{
				if (count != 0 || !imageData || offset != bytes.Length) throw new InvalidDataException();
				return (width, height);
			}
		}
		throw new InvalidDataException();
	}

	private static (int Width, int Height) ReadJpeg(ReadOnlySpan<byte> bytes)
	{
		if (bytes.Length < 12 || bytes[0] != 0xff || bytes[1] != 0xd8 || bytes[^2] != 0xff || bytes[^1] != 0xd9) throw new InvalidDataException();
		var offset = 2;
		var width = 0;
		var height = 0;
		var scanned = false;
		while (offset < bytes.Length)
		{
			if (bytes[offset++] != 0xff) throw new InvalidDataException();
			while (offset < bytes.Length && bytes[offset] == 0xff) offset++;
			if (offset >= bytes.Length) throw new InvalidDataException();
			var marker = bytes[offset++];
			if (marker == 0xd9)
			{
				if (!scanned || offset != bytes.Length) throw new InvalidDataException();
				return (width, height);
			}
			if (marker is 0 or 0xd8 or >= 0xd0 and <= 0xd7 || offset > bytes.Length - 2) throw new InvalidDataException();
			var length = BinaryPrimitives.ReadUInt16BigEndian(bytes.Slice(offset, 2));
			if (length < 2 || length > bytes.Length - offset) throw new InvalidDataException();
			if (marker is 0xc0 or 0xc1 or 0xc2)
			{
				if (length < 8 || width != 0) throw new InvalidDataException();
				height = BinaryPrimitives.ReadUInt16BigEndian(bytes.Slice(offset + 3, 2));
				width = BinaryPrimitives.ReadUInt16BigEndian(bytes.Slice(offset + 5, 2));
			}
			offset += length;
			if (marker == 0xda)
			{
				if (width == 0 || length < 6 || length != 6 + 2 * bytes[offset - length + 2]) throw new InvalidDataException();
				var entropy = false;
				while (offset < bytes.Length)
				{
					if (bytes[offset] != 0xff) { entropy = true; offset++; continue; }
					var markerStart = offset++;
					while (offset < bytes.Length && bytes[offset] == 0xff) offset++;
					if (offset == bytes.Length) throw new InvalidDataException();
					var next = bytes[offset];
					if (next == 0) { entropy = true; offset++; continue; }
					if (next is >= 0xd0 and <= 0xd7) { offset++; continue; }
					offset = markerStart;
					break;
				}
				if (!entropy) throw new InvalidDataException();
				scanned = true;
			}
		}
		throw new InvalidDataException();
	}

	private static readonly uint[] CrcTable = CreateCrcTable();
	internal static uint Crc32(ReadOnlySpan<byte> data)
	{
		var crc = uint.MaxValue;
		foreach (var value in data) crc = CrcTable[(crc ^ value) & 255] ^ (crc >> 8);
		return ~crc;
	}
	private static uint[] CreateCrcTable()
	{
		var table = new uint[256];
		for (uint i = 0; i < table.Length; i++)
		{
			var value = i;
			for (var bit = 0; bit < 8; bit++) value = (value & 1) != 0 ? 0xedb88320u ^ (value >> 1) : value >> 1;
			table[i] = value;
		}
		return table;
	}
}
