using System;
using System.Buffers.Binary;
using QuestPDF.Infrastructure;
using Volo.Abp;

namespace ShikshaMitra.TenantSettings.SchoolIdentity;

public static class SchoolIdentityImageValidation
{
	public static (string ContentType, int Width, int Height) Validate(SchoolIdentityAssetKind kind, byte[] bytes)
		=> ValidateCore(kind, bytes, enforceMinimumDimensions: true);

	public static (string ContentType, int Width, int Height) ValidateLegacy(SchoolIdentityAssetKind kind, byte[] bytes)
		=> ValidateCore(kind, bytes, enforceMinimumDimensions: false);

	private static (string ContentType, int Width, int Height) ValidateCore(SchoolIdentityAssetKind kind, byte[] bytes, bool enforceMinimumDimensions)
	{
		var limit = kind switch
		{
			SchoolIdentityAssetKind.Logo => 2 * 1024 * 1024,
			SchoolIdentityAssetKind.Banner => 5 * 1024 * 1024,
			SchoolIdentityAssetKind.Signature => 1024 * 1024,
			_ => throw new BusinessException(SchoolIdentityErrorCodes.InvalidAssetKind)
		};
		if (bytes.Length == 0 || bytes.Length > limit)
			throw new BusinessException(SchoolIdentityErrorCodes.ImageSizeInvalid);
		var (type, width, height) = Dimensions(bytes);
		if (width < 1 || height < 1 || width > 10000 || height > 10000 || (long)width * height > 25000000)
			throw new BusinessException(SchoolIdentityErrorCodes.ImageDimensionsInvalid);
		var (minimumWidth, minimumHeight) = kind == SchoolIdentityAssetKind.Banner ? (1400, 300) : (32, 32);
		if (enforceMinimumDimensions && (width < minimumWidth || height < minimumHeight))
			throw new BusinessException(SchoolIdentityErrorCodes.ImageTooSmall)
				.WithData("MinimumWidth", minimumWidth).WithData("MinimumHeight", minimumHeight)
				.WithData("Width", width).WithData("Height", height);
		try
		{
			using var image = Image.FromBinaryData(bytes);
		}
		catch (Exception exception) when (exception is not OutOfMemoryException)
		{
			throw new BusinessException(SchoolIdentityErrorCodes.ImageInvalid, innerException: exception);
		}
		return (type, width, height);
	}

	private static (string, int, int) Dimensions(byte[] bytes)
	{
		ReadOnlySpan<byte> data = bytes;
		if (data.Length >= 24 && data[..8].SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })
			&& data.Slice(12, 4).SequenceEqual("IHDR"u8))
			return ("image/png", BinaryPrimitives.ReadInt32BigEndian(data.Slice(16, 4)), BinaryPrimitives.ReadInt32BigEndian(data.Slice(20, 4)));
		if (data.Length >= 4 && data[0] == 255 && data[1] == 216)
		{
			var offset = 2;
			while (offset + 4 <= data.Length)
			{
				if (data[offset++] != 255) break;
				while (offset < data.Length && data[offset] == 255) offset++;
				if (offset + 3 > data.Length) break;
				var marker = data[offset++];
				if (marker == 217 || marker == 218) break;
				if (marker == 1 || marker is >= 208 and <= 215) continue;
				var length = BinaryPrimitives.ReadUInt16BigEndian(data.Slice(offset, 2));
				if (length < 2 || offset + length > data.Length) break;
				if (marker is 192 or 193 or 194 or 195 or 197 or 198 or 199 or 201 or 202 or 203 or 205 or 206 or 207 && length >= 7)
					return ("image/jpeg", BinaryPrimitives.ReadUInt16BigEndian(data.Slice(offset + 5, 2)), BinaryPrimitives.ReadUInt16BigEndian(data.Slice(offset + 3, 2)));
				offset += length;
			}
		}
		throw new BusinessException(SchoolIdentityErrorCodes.ImageInvalid);
	}
}
