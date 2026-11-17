using System;
using SkiaSharp;

namespace ShikshaMitra.Common.IdentityCards;

internal static class IdentityCardRaster
{
	public const int MaxImageBytes = 512 * 1024;
	public static byte[] Decode(string? imageData)
	{
		const string png = "data:image/png;base64,";
		const string jpeg = "data:image/jpeg;base64,";
		if (imageData == null || imageData.Length > MaxImageBytes * 4 / 3 + 32 ||
			!imageData.StartsWith(png, StringComparison.Ordinal) && !imageData.StartsWith(jpeg, StringComparison.Ordinal))
			throw IdentityCardGeometry.Invalid("Use an embedded PNG or JPEG image, up to 512 KB.");
		try
		{
			var bytes = Convert.FromBase64String(imageData[(imageData.StartsWith(png, StringComparison.Ordinal) ? png.Length : jpeg.Length)..]);
			var mime = Validate(bytes, MaxImageBytes);
			if (imageData.StartsWith(png, StringComparison.Ordinal) != (mime == "image/png")) throw IdentityCardGeometry.Invalid("The image type does not match its content.");
			return bytes;
		}
		catch (FormatException) { throw IdentityCardGeometry.Invalid("The embedded image has malformed base64 content."); }
	}
	public static string Validate(byte[] bytes, int maxBytes = MaxImageBytes)
	{
		using var bitmap = DecodeBitmap(bytes, maxBytes);
		return bytes[0] == 137 ? "image/png" : "image/jpeg";
	}
	public static SKBitmap DecodeBitmap(byte[] bytes, int maxBytes, int maxDimension = 2048, long maxPixels = 2048L * 2048, bool allowOriginalFormats = false)
	{
		var png = bytes.Length >= 8 && bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
		var jpeg = bytes.Length >= 3 && bytes[0] == 255 && bytes[1] == 216 && bytes[2] == 255;
		var gif = allowOriginalFormats && bytes.Length >= 6 && (bytes.AsSpan(0, 6).SequenceEqual("GIF87a"u8) || bytes.AsSpan(0, 6).SequenceEqual("GIF89a"u8));
		var webp = allowOriginalFormats && bytes.Length >= 12 && bytes.AsSpan(0, 4).SequenceEqual("RIFF"u8) && bytes.AsSpan(8, 4).SequenceEqual("WEBP"u8);
		if (bytes.Length > maxBytes || !png && !jpeg && !gif && !webp) throw IdentityCardGeometry.Invalid("The image must be a supported bounded raster.");
		using var data = SKData.CreateCopy(bytes);
		using var codec = SKCodec.Create(data);
		if (codec == null || codec.Info.Width < 1 || codec.Info.Height < 1 || codec.Info.Width > maxDimension || codec.Info.Height > maxDimension ||
			(long)codec.Info.Width * codec.Info.Height > maxPixels ||
			codec.EncodedFormat != (png ? SKEncodedImageFormat.Png : jpeg ? SKEncodedImageFormat.Jpeg : gif ? SKEncodedImageFormat.Gif : SKEncodedImageFormat.Webp))
			throw IdentityCardGeometry.Invalid("The image exceeds the supported dimensions or pixel count.");
		var bitmap = new SKBitmap(codec.Info.Width, codec.Info.Height);
		if (codec.GetPixels(bitmap.Info, bitmap.GetPixels()) != SKCodecResult.Success)
		{
			bitmap.Dispose();
			throw IdentityCardGeometry.Invalid("The image raster is incomplete or malformed.");
		}
		return bitmap;
	}
}
