using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Volo.Abp;
using SkiaSharp;

namespace ShikshaMitra.Common.IdentityCards;

internal static class IdentityCardPhotos
{
	public static IdentityCardPhotoDto Read(Guid recordId, byte[]? original)
	{
		if (original == null) throw IdentityCardGeometry.Invalid("This record has no available photo.");
		using var bitmap = IdentityCardRaster.DecodeBitmap(original, 5 * 1024 * 1024, 8192, 32_000_000, allowOriginalFormats: true);
		var pixels = original;
		var mime = original[0] == 255 ? "image/jpeg" : "image/png";
		if (bitmap.Width > 2048 || bitmap.Height > 2048 || original[0] is not (137 or 255))
		{
			var scale = Math.Min(1, Math.Min(2048d / bitmap.Width, 2048d / bitmap.Height));
			using var resized = bitmap.Resize(new SKImageInfo(Math.Max(1, (int)Math.Round(bitmap.Width * scale)), Math.Max(1, (int)Math.Round(bitmap.Height * scale))), new SKSamplingOptions(SKFilterMode.Linear));
			if (resized == null) throw IdentityCardGeometry.Invalid("This photo could not be resized for editing.");
			using var oriented = IdentityCardPhotoOrientation.Apply(resized, original);
			using var image = SKImage.FromBitmap(oriented);
			using var encoded = image.Encode(mime == "image/png" ? SKEncodedImageFormat.Png : SKEncodedImageFormat.Jpeg, 90);
			pixels = encoded.ToArray();
			if (pixels.Length > 5 * 1024 * 1024) throw IdentityCardGeometry.Invalid("The photo is too large to edit. Upload a smaller raster photo.");
		}
		return new() { RecordId = recordId, SourceHash = Hash(original), ImageData = $"data:{mime};base64,{Convert.ToBase64String(pixels)}" };
	}
	public static async Task<Dictionary<Guid, byte[]>> ValidateAsync(List<IdentityCardPhotoDto>? edits, IReadOnlyCollection<Guid> authorizedIds, Func<Guid, Task<byte[]?>> loadOriginal)
	{
		var result = new Dictionary<Guid, byte[]>();
		if (edits == null) return result;
		if (edits.Count > authorizedIds.Count || edits.Any(e => e == null || !authorizedIds.Contains(e.RecordId) || e.SourceHash == null || e.SourceHash.Length != 64) || edits.Select(e => e.RecordId).Distinct().Count() != edits.Count)
			throw IdentityCardGeometry.Invalid("Photo edits must identify distinct records in the selected authorized cohort and their source hashes.");
		var total = 0;
		foreach (var edit in edits)
		{
			var bytes = IdentityCardRaster.Decode(edit.ImageData);
			total += bytes.Length;
			if (total > 8 * 1024 * 1024) throw IdentityCardGeometry.Invalid("Processed photos exceed the 8 MB batch limit.");
			var original = await loadOriginal(edit.RecordId);
			if (original == null || !string.Equals(Hash(original), edit.SourceHash, StringComparison.OrdinalIgnoreCase))
				throw new BusinessException(IdentityCardErrorCodes.PhotoSourceChanged).WithData("detail", "The source photo changed or is unavailable. Reload it and apply the edit again.");
			result.Add(edit.RecordId, bytes);
		}
		return result;
	}
	public static string DataUri(byte[] bytes) => $"data:{IdentityCardRaster.Validate(bytes)};base64,{Convert.ToBase64String(bytes)}";
	private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
}
