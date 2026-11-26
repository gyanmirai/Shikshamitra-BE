using System.Linq;
using System.Xml.Linq;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using Shouldly;
using SkiaSharp;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Common.IdentityCards;

public class IdentityCardBorderStyle_Tests
{
	[Theory]
	[InlineData(1, "solid", null, null)]
	[InlineData(2, "dashed", "16 8", "butt")]
	[InlineData(2, "dotted", "0.04 8", "round")]
	public void Explicit_line_uses_centered_coordinates_and_matching_svg_pattern(int schemaVersion, string style, string? dashArray, string? lineCap)
	{
		var layout = Parse($$$"""{"id":"line","type":"line","x":10,"y":15,"w":130,"h":10,"style":{"borderStyle":"{{{style}}}","borderWidth":4,"borderColor":"#112233","backgroundColor":"#AABBCC"}}""", schemaVersion);
		var line = RenderedElement(layout);
		line.Name.LocalName.ShouldBe("line");
		Attribute(line, "x1").ShouldBe("10");
		Attribute(line, "y1").ShouldBe("20");
		Attribute(line, "x2").ShouldBe("140");
		Attribute(line, "y2").ShouldBe("20");
		Attribute(line, "stroke").ShouldBe("#112233");
		Attribute(line, "stroke-width").ShouldBe("4");
		Attribute(line, "stroke-dasharray").ShouldBe(dashArray);
		Attribute(line, "stroke-linecap").ShouldBe(lineCap);
	}

	[Fact]
	public void Missing_style_renders_legacy_filled_rect_and_explicit_style_has_stroke_fallbacks()
	{
		var legacy = RenderedElement(Parse("""{"id":"line","type":"line","x":10,"y":15,"w":130,"h":10,"style":{"borderWidth":4,"borderColor":"#112233","backgroundColor":"#AABBCC"}}"""));
		legacy.Name.LocalName.ShouldBe("rect");
		Attribute(legacy, "fill").ShouldBe("#AABBCC");
		Attribute(legacy, "height").ShouldBe("10");
		Attribute(legacy, "stroke").ShouldBeNull();

		var fromBackground = RenderedElement(Parse("""{"id":"line","type":"line","x":10,"y":15,"w":130,"h":10,"style":{"borderStyle":"solid","backgroundColor":"#AABBCC"}}"""));
		Attribute(fromBackground, "stroke").ShouldBe("#AABBCC");
		Attribute(fromBackground, "stroke-width").ShouldBe("10");
		var hidden = RenderedElement(Parse("""{"id":"line","type":"line","x":10,"y":15,"w":130,"h":10,"style":{"borderStyle":"dashed","borderWidth":0}}"""));
		Attribute(hidden, "stroke").ShouldBe("#1D4ED8");
		Attribute(hidden, "stroke-width").ShouldBe("0");
		Attribute(hidden, "stroke-dasharray").ShouldBeNull();
	}

	[Theory]
	[InlineData("rectangle", "rect", "dashed", "16 8", "butt")]
	[InlineData("ellipse", "ellipse", "dotted", "0.04 8", "round")]
	[InlineData("ellipse", "ellipse", "solid", null, null)]
	public void Shape_keeps_fill_and_geometry_with_the_selected_border(string shape, string svgElement, string style, string? dashArray, string? lineCap)
	{
		var layout = Parse($$$"""{"id":"shape","type":"shape","x":10,"y":15,"w":130,"h":10,"style":{"shape":"{{{shape}}}","borderRadius":3,"borderStyle":"{{{style}}}","borderWidth":4,"borderColor":"#112233","backgroundColor":"#AABBCC"}}""");
		var rendered = RenderedElement(layout);
		rendered.Name.LocalName.ShouldBe(svgElement);
		Attribute(rendered, "fill").ShouldBe("#AABBCC");
		Attribute(rendered, "stroke").ShouldBe("#112233");
		Attribute(rendered, "stroke-width").ShouldBe("4");
		Attribute(rendered, "rx").ShouldBe(shape == "ellipse" ? "65" : "3");
		Attribute(rendered, "stroke-dasharray").ShouldBe(dashArray);
		Attribute(rendered, "stroke-linecap").ShouldBe(lineCap);
	}

	[Theory]
	[InlineData(1, "line", "\"Dashed\"")]
	[InlineData(2, "line", "\"dashed \"")]
	[InlineData(2, "shape", "\"double\"")]
	[InlineData(2, "shape", "2")]
	[InlineData(2, "text", "\"solid\"")]
	[InlineData(2, "photo", "\"dashed\"")]
	public void Rejects_noncanonical_styles_and_unsupported_element_types(int schemaVersion, string type, string styleJson)
	{
		var exception = Should.Throw<BusinessException>(() =>
			Parse($$$"""{"id":"element","type":"{{{type}}}","x":10,"y":15,"w":130,"h":10,"style":{"borderStyle":{{{styleJson}}}}}""", schemaVersion));
		exception.Code.ShouldBe(IdentityCardGeometry.InvalidConfiguration);
	}

	[Theory]
	[InlineData("solid", 22, true, 30, true)]
	[InlineData("dashed", 22, true, 30, false)]
	[InlineData("dotted", 11, true, 14, false)]
	public void Line_patterns_survive_native_pdf_svg_rasterization(string style, int firstX, bool firstInk, int secondX, bool secondInk)
	{
		var layout = Parse($$$"""{"id":"line","type":"line","x":10,"y":15,"w":130,"h":10,"style":{"borderStyle":"{{{style}}}","borderWidth":4,"borderColor":"#000000"}}""");
		using var bitmap = Rasterize(layout);
		(bitmap.GetPixel(firstX, 20).Red < 100).ShouldBe(firstInk);
		(bitmap.GetPixel(secondX, 20).Red < 100).ShouldBe(secondInk);
	}

	[Theory]
	[InlineData("rectangle", "dashed")]
	[InlineData("ellipse", "dotted")]
	public void Shape_patterns_leave_border_gaps_and_retain_fill_in_native_pdf_svg_rasterization(string shape, string style)
	{
		var layout = Parse($$$"""{"id":"shape","type":"shape","x":20,"y":20,"w":120,"h":60,"style":{"shape":"{{{shape}}}","borderStyle":"{{{style}}}","borderWidth":4,"borderColor":"#000000","backgroundColor":"#FF0000"}}""");
		using var bitmap = Rasterize(layout);
		var edge = Enumerable.Range(66, 28).Select(x => bitmap.GetPixel(x, 20).Red < 100).ToList();
		edge.Count(ink => ink).ShouldBeGreaterThan(3);
		edge.Count(ink => !ink).ShouldBeGreaterThan(3);
		bitmap.GetPixel(80, 50).Red.ShouldBeGreaterThan((byte)100);
		bitmap.GetPixel(80, 50).Green.ShouldBeLessThan((byte)50);
	}

	private static SKBitmap Rasterize(IdentityCardLayout layout)
	{
		QuestPDF.Settings.License = LicenseType.Community;
		var document = Document.Create(doc => doc.Page(page =>
		{
			page.Size(layout.Width, layout.Height);
			page.Margin(0);
			page.Content().Svg(Render(layout));
		}));
		return SKBitmap.Decode(document.GenerateImages(new ImageGenerationSettings { RasterDpi = 72 }).Single());
	}

	private static IdentityCardLayout Parse(string element, int schemaVersion = 2) =>
		IdentityCardSchema.ParseLayout($$$"""{"width":160,"height":100,"widthMm":85.6,"heightMm":53.98,"front":{"elements":[{{{element}}}]}}""", [], schemaVersion);

	private static XElement RenderedElement(IdentityCardLayout layout) =>
		XDocument.Parse(Render(layout)).Root!.Elements().Single(node => node.Name.LocalName == "g").Elements().Single();

	private static string? Attribute(XElement element, string name) => (string?)element.Attribute(name);

	private static string Render(IdentityCardLayout layout) =>
		IdentityCardSvgRenderer.Render(layout, false, _ => null, _ => true, null, null, "", "");
}
