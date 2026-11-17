using System;
using System.Text.Json;
using System.Threading.Tasks;
using System.Linq;
using ShikshaMitra.Common.IdentityCards;
using Shouldly;
using SkiaSharp;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Common.IdentityCards;

public class IdentityCardStudio_Tests
{
	internal static string Raster(int width = 2, int height = 2)
	{
		using var bitmap = new SKBitmap(width, height);
		bitmap.Erase(SKColors.Red);
		using var image = SKImage.FromBitmap(bitmap);
		using var data = image.Encode(SKEncodedImageFormat.Png, 100);
		return "data:image/png;base64," + Convert.ToBase64String(data.ToArray());
	}
	private static IdentityCardLayout Parse(string element) => IdentityCardSchema.ParseLayout("""{"width":428,"height":270,"widthMm":85.6,"heightMm":53.98,"front":{"elements":[""" + element + "]}}", ["static"], 2);

	[Fact]
	public void Studio_raster_ellipse_rotation_opacity_and_typography_render()
	{
		var image = JsonSerializer.Serialize(Raster());
		var layout = Parse("""{"id":"image","type":"image","x":20,"y":20,"w":50,"h":40,"rotation":45,"opacity":0.5,"groupId":"art","imageData":IMAGEDATA,"style":{}}""".Replace("IMAGEDATA", image));
		var svg = IdentityCardSvgRenderer.Render(layout, false, _ => "", _ => true, null, null, "qr", "code");
		svg.ShouldContain("rotate(45 45 40)"); svg.ShouldContain("opacity=\"0.5\""); svg.ShouldContain("data:image/png;base64,");
		svg.ShouldContain("preserveAspectRatio=\"xMidYMid meet\"");
		IdentityCardPdfComposer.Compose(layout, new() { PageSize = "Card" }, [new(svg, null)], false).Take(4).ShouldBe(new byte[] { 37, 80, 68, 70 });
		layout = Parse("""{"id":"shape","type":"shape","x":20,"y":20,"w":50,"h":40,"style":{"shape":"ellipse"}}""");
		IdentityCardSvgRenderer.Render(layout, false, _ => "", _ => true, null, null, "qr", "code").ShouldContain("<ellipse");
		layout = Parse("""{"id":"text","type":"text","x":20,"y":20,"w":200,"h":40,"style":{"fontStyle":"italic","textDecoration":"underline"}}""");
		svg = IdentityCardSvgRenderer.Render(layout, false, _ => "Name", _ => true, null, null, "qr", "code");
		svg.ShouldContain("font-style=\"italic\""); svg.ShouldNotContain("text-decoration=");
	}
	[Theory]
	[InlineData("\"rotation\":181")]
	[InlineData("\"opacity\":-0.1")]
	[InlineData("\"rotation\":\"NaN\"")]
	[InlineData("\"unexpectedPhotoOverride\":\"data:image/png;base64,abc\"")]
	[InlineData("\"style\":{\"shape\":\"triangle\"}")]
	[InlineData("\"style\":{\"fontStyle\":\"oblique\"}")]
	[InlineData("\"style\":{\"textDecoration\":\"blink\"}")]
	[InlineData("\"imageData\":\"data:image/png;base64,iVBORw0KGgo=\"")]
	public void Invalid_studio_properties_are_rejected(string property) => Should.Throw<BusinessException>(() => Parse("""{"id":"x","type":"text","x":0,"y":0,"w":50,"h":40,""" + property + "}"));
	[Fact]
	public void Raster_requires_actual_decode_and_bounded_dimensions()
	{
		foreach (var image in new[] { "https://example.com/a.png", "data:image/svg+xml;base64,PHN2Zy8+", "data:image/png;base64,iVBORw0KGgo=", Raster(2049, 1) })
			Should.Throw<BusinessException>(() => Parse("""{"id":"x","type":"image","x":0,"y":0,"w":50,"h":40,"imageData":""" + JsonSerializer.Serialize(image) + "}"));
	}
	[Fact]
	public async Task Photo_edits_reject_duplicates_stale_sources_and_unselected_ids_before_loading()
	{
		var id = Guid.NewGuid(); var original = Convert.FromBase64String(Raster().Split(',')[1]);
		var photo = IdentityCardPhotos.Read(id, original);
		var reads = 0;
		Task<byte[]?> Load(Guid _) { reads++; return Task.FromResult<byte[]?>(original); }
		await Should.ThrowAsync<BusinessException>(() => IdentityCardPhotos.ValidateAsync([photo, photo], [id], Load));
		await Should.ThrowAsync<BusinessException>(() => IdentityCardPhotos.ValidateAsync([photo], [Guid.NewGuid()], Load));
		reads.ShouldBe(0);
		photo.SourceHash = new string('0', 64);
		(await Should.ThrowAsync<BusinessException>(() => IdentityCardPhotos.ValidateAsync([photo], [id], Load))).Code.ShouldBe(IdentityCardErrorCodes.PhotoSourceChanged);
		photo = IdentityCardPhotos.Read(id, original);
		(await IdentityCardPhotos.ValidateAsync([photo], [id], Load))[id].ShouldBe(original);
	}
	[Fact]
	public void Layout_image_data_cannot_hide_in_photo_elements_or_exceed_group_and_size_limits()
	{
		Should.Throw<BusinessException>(() => Parse("""{"id":"x","type":"photo","x":0,"y":0,"w":50,"h":40,"imageData":""" + JsonSerializer.Serialize(Raster()) + "}"));
		Should.Throw<BusinessException>(() => Parse("""{"id":"x","type":"text","x":0,"y":0,"w":50,"h":40,"groupId":""" + JsonSerializer.Serialize(new string('x', 101)) + "}"));
		Should.Throw<BusinessException>(() => IdentityCardSchema.ParseLayout(new string(' ', 2 * 1024 * 1024 + 1), [], 2));
	}
	[Fact]
	public async Task Processed_photo_batch_cannot_exceed_eight_megabytes()
	{
		var original = Convert.FromBase64String(Raster().Split(',')[1]);
		var bounded = new byte[512 * 1024]; original.CopyTo(bounded, 0);
		var data = "data:image/png;base64," + Convert.ToBase64String(bounded);
		var ids = Enumerable.Range(0, 17).Select(_ => Guid.NewGuid()).ToArray();
		var edits = ids.Select(id => { var source = IdentityCardPhotos.Read(id, original); source.ImageData = data; return source; }).ToList();
		await Should.ThrowAsync<BusinessException>(() => IdentityCardPhotos.ValidateAsync(edits, ids, _ => Task.FromResult<byte[]?>(original)));
		IdentityCardRaster.Decode(data).Length.ShouldBe(512 * 1024);
		Should.Throw<BusinessException>(() => IdentityCardRaster.Decode("data:image/png;base64," + Convert.ToBase64String(new byte[512 * 1024 + 1])));
	}
	[Fact]
	public void Original_photo_reads_resize_large_rasters_but_hash_original_bytes()
	{
		var original = Convert.FromBase64String(Raster(4096, 4).Split(',')[1]);
		var source = IdentityCardPhotos.Read(Guid.NewGuid(), original);
		using var bitmap = SKBitmap.Decode(Convert.FromBase64String(source.ImageData.Split(',')[1]));
		bitmap.Width.ShouldBe(2048); bitmap.Height.ShouldBe(2);
		source.SourceHash.ShouldBe(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(original)).ToLowerInvariant());
		Should.Throw<BusinessException>(() => IdentityCardPhotos.Read(Guid.NewGuid(), Convert.FromBase64String(Raster(8193, 1).Split(',')[1])));
	}
	[Fact]
	public void Resized_original_preserves_camera_EXIF_orientation()
	{
		using var bitmap = new SKBitmap(4096, 128); bitmap.Erase(SKColors.Red);
		using var image = SKImage.FromBitmap(bitmap); using var data = image.Encode(SKEncodedImageFormat.Jpeg, 90);
		byte[] exif = [255, 225, 0, 34, 69, 120, 105, 102, 0, 0, 73, 73, 42, 0, 8, 0, 0, 0, 1, 0, 18, 1, 3, 0, 1, 0, 0, 0, 6, 0, 0, 0, 0, 0, 0, 0];
		var jpeg = data.ToArray(); var original = jpeg.Take(2).Concat(exif).Concat(jpeg.Skip(2)).ToArray();
		var source = IdentityCardPhotos.Read(Guid.NewGuid(), original);
		using var resized = SKBitmap.Decode(Convert.FromBase64String(source.ImageData.Split(',')[1]));
		resized.Width.ShouldBe(64); resized.Height.ShouldBe(2048);
	}
	[Theory]
	[InlineData("gif")]
	[InlineData("webp")]
	public void Existing_original_GIF_and_WebP_normalize_to_PNG_without_changing_source_hash(string format)
	{
		using var bitmap = new SKBitmap(3, 3); bitmap.Erase(SKColors.Red);
		using var image = SKImage.FromBitmap(bitmap); using var data = image.Encode(SKEncodedImageFormat.Webp, 90);
		var original = format == "gif" ? Convert.FromBase64String("R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7") : data.ToArray();
		var source = IdentityCardPhotos.Read(Guid.NewGuid(), original);
		source.ImageData.ShouldStartWith("data:image/png;base64,");
		IdentityCardRaster.Decode(source.ImageData).Length.ShouldBeGreaterThan(0);
		source.SourceHash.ShouldBe(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(original)).ToLowerInvariant());
		Should.Throw<BusinessException>(() => IdentityCardRaster.Decode($"data:image/{format};base64,{Convert.ToBase64String(original)}"));
	}
}
