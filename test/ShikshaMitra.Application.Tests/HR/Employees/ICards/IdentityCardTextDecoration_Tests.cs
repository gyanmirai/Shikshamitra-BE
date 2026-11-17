using System.Linq;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using Shouldly;
using SkiaSharp;
using Xunit;

namespace ShikshaMitra.Common.IdentityCards;

public class IdentityCardTextDecoration_Tests
{
	[Theory]
	[InlineData("underline", "left")]
	[InlineData("underline", "center")]
	[InlineData("underline", "right")]
	[InlineData("line-through", "left")]
	[InlineData("line-through", "center")]
	[InlineData("line-through", "right")]
	public void Wrapped_decorations_survive_native_print_rasterization(string decoration, string align)
	{
		QuestPDF.Settings.License = LicenseType.Community;
		var element = new IdentityCardLayoutElement { Id = "text", Type = "text", X = 20, Y = 20, W = 75, H = 90, Opacity = .5f,
			Style = new() { FontSize = 20, TextAlign = align, TextDecoration = decoration, FontStyle = "italic" } };
		var layout = new IdentityCardLayout { Width = 160, Height = 130, Front = new() { Elements = [element] } };
		var svg = IdentityCardSvgRenderer.Render(layout, false, _ => "MMMM MMMM", _ => true, null, null, "", "");
		var document = Document.Create(d => d.Page(p => { p.Size(160, 130); p.Margin(0); p.Content().Svg(svg); }));
		using var bitmap = SKBitmap.Decode(document.GenerateImages(new ImageGenerationSettings { RasterDpi = 72 }).Single());
		foreach (var baseline in new[] { 40, 64 })
		{
			var top = baseline + (decoration == "underline" ? 1 : -9);
			var bottom = baseline + (decoration == "underline" ? 6 : -2);
			var run = LongestInkRun(bitmap, top, bottom);
			run.Length.ShouldBeGreaterThan(50, $"{decoration} must print continuously beneath/across each wrapped line.");
			if (align == "left") run.Start.ShouldBeInRange(18, 22);
			if (align == "center") ((run.Start + run.End) / 2f).ShouldBe(57.5f, 2);
			if (align == "right") run.End.ShouldBeInRange(93, 97);
		}
	}
	[Fact]
	public void Bold_italic_native_glyph_advance_matches_its_measured_underline()
	{
		QuestPDF.Settings.License = LicenseType.Community;
		var element = new IdentityCardLayoutElement { Id = "title", Type = "text", X = 20, Y = 20, W = 300, H = 40,
			Style = new() { FontFamily = "Arial", FontSize = 18, FontWeight = "bold", FontStyle = "italic", TextDecoration = "underline" } };
		var layout = new IdentityCardLayout { Width = 350, Height = 75, Front = new() { Elements = [element] } };
		var svg = IdentityCardSvgRenderer.Render(layout, false, _ => "Visual studio print proof", _ => true, null, null, "", "");
		var document = Document.Create(d => d.Page(p => { p.Size(350, 75); p.Margin(0); p.Content().Svg(svg); }));
		using var bitmap = SKBitmap.Decode(document.GenerateImages(new ImageGenerationSettings { RasterDpi = 72 }).Single());
		var right = 0;
		for (var y = 20; y <= 38; y++)
			for (var x = 20; x < 330; x++)
				if (bitmap.GetPixel(x, y).Red < 200) right = System.Math.Max(right, x);
		var decorationRight = 0;
		for (var y = 40; y <= 44; y++)
			for (var x = 20; x < 330; x++)
				if (bitmap.GetPixel(x, y).Red < 200) decorationRight = System.Math.Max(decorationRight, x);
		System.Math.Abs(decorationRight - right).ShouldBeLessThanOrEqualTo(3, "The native renderer must use the same typeface as the measured decoration.");
	}
	private static (int Length, int Start, int End) LongestInkRun(SKBitmap bitmap, int top, int bottom)
	{
		var longest = (Length: 0, Start: 0, End: 0);
		for (var y = top; y <= bottom; y++)
		{
			var run = 0;
			for (var x = 15; x < 100; x++)
			{
				var color = bitmap.GetPixel(x, y);
				run = color.Red < 230 ? run + 1 : 0;
				if (run > longest.Length) longest = (run, x - run + 1, x);
			}
		}
		return longest;
	}
}
