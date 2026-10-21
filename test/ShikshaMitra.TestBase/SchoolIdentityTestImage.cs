using System;
using System.Buffers.Binary;
using System.IO;
using System.IO.Compression;

namespace ShikshaMitra;

public static class SchoolIdentityTestImage
{
	public static byte[] Png(int width = 1400, int height = 300)
	{
		using var output = new MemoryStream();
		output.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
		var header = new byte[13];
		BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(0, 4), width);
		BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4, 4), height);
		header[8] = 8;
		header[9] = 6;
		WriteChunk(output, "IHDR"u8.ToArray(), header);
		using var compressed = new MemoryStream();
		using (var compressor = new ZLibStream(compressed, CompressionLevel.Fastest, true))
			compressor.Write(new byte[(width * 4 + 1) * height]);
		WriteChunk(output, "IDAT"u8.ToArray(), compressed.ToArray());
		WriteChunk(output, "IEND"u8.ToArray(), []);
		return output.ToArray();
	}

	private static void WriteChunk(Stream output, byte[] type, byte[] data)
	{
		Span<byte> number = stackalloc byte[4];
		BinaryPrimitives.WriteInt32BigEndian(number, data.Length);
		output.Write(number);
		output.Write(type);
		output.Write(data);
		uint crc = uint.MaxValue;
		foreach (var bytes in new[] { type, data })
			foreach (var value in bytes)
			{
				crc ^= value;
				for (var bit = 0; bit < 8; bit++) crc = (crc >> 1) ^ ((crc & 1) != 0 ? 0xedb88320u : 0);
			}
		BinaryPrimitives.WriteUInt32BigEndian(number, ~crc);
		output.Write(number);
	}
}
