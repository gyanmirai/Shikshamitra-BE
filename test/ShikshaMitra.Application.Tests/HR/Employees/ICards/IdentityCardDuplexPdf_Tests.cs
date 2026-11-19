using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Common.IdentityCards;

public class IdentityCardDuplexPdf_Tests
{
	[Theory]
	[InlineData("long-edge", 106.5f, 36.04f)]
	[InlineData("short-edge", 17.9f, 206.98f)]
	public void Partial_duplex_sheets_keep_fixed_empty_slots(string flip, float expectedX, float expectedY)
	{
		var settings = new IdentityCardPrintSettings { DuplexFlip = flip };
		var sheet = IdentityCardGeometry.Sheet(Layout(), settings);
		sheet.Capacity.ShouldBe(8);
		var back = sheet.Position(0, settings, true);
		back.X.ShouldBe(expectedX, 0.02f);
		back.Y.ShouldBe(expectedY, 0.02f);
		var front = sheet.Position(0, settings, false);
		front.X.ShouldBe(17.9f, 0.02f);
		front.Y.ShouldBe(36.04f, 0.02f);
	}

	[Fact]
	public void Nine_cards_produce_front_then_back_for_each_sheet()
	{
		var pdf = IdentityCardPdfComposer.Compose(Layout(), new(), Enumerable.Repeat(Artwork(), 9).ToList(), true);
		PageCount(pdf).ShouldBe(4);
		SaveEvidence("duplex-nine-a4.pdf", pdf);
	}

	[Fact]
	public void Card_output_has_exact_physical_pages_and_per_card_sides()
	{
		var settings = new IdentityCardPrintSettings { PageSize = "Card" };
		var pdf = IdentityCardPdfComposer.Compose(Layout(), settings, [Artwork(), Artwork()], true);
		PageCount(pdf).ShouldBe(4);
		var boxes = Regex.Matches(Encoding.Latin1.GetString(pdf), @"/MediaBox\s*\[\s*0\s+0\s+([0-9.]+)\s+([0-9.]+)\s*\]");
		boxes.Count.ShouldBeGreaterThan(0);
		foreach (Match box in boxes)
		{
			float.Parse(box.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture).ShouldBe(242.64567f, 0.02f);
			float.Parse(box.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture).ShouldBe(153.01418f, 0.02f);
		}
		SaveEvidence("duplex-two-card.pdf", pdf);
	}

	[Theory]
	[InlineData("both", false, 1)]
	[InlineData("front", true, 1)]
	[InlineData("back", true, 1)]
	public void Requested_sides_do_not_emit_blank_back_pages(string side, bool backEnabled, int pages)
	{
		var pdf = IdentityCardPdfComposer.Compose(Layout(), new() { SideMode = side }, [Artwork()], backEnabled);
		PageCount(pdf).ShouldBe(pages);
	}

	[Fact]
	public void Large_cards_are_valid_for_card_output_and_zero_margin_A4()
	{
		var layout = Layout(); layout.WidthMm = 200; layout.HeightMm = 100;
		var parsed = IdentityCardSchema.ParseLayout(JsonSerializer.Serialize(layout), [], 2, true);
		IdentityCardGeometry.Sheet(parsed, new() { PageSize = "Card" }).Capacity.ShouldBe(1);
		IdentityCardGeometry.Sheet(parsed, new() { Margin = 0 }).Capacity.ShouldBe(2);
	}

	[Fact]
	public void No_capacity_and_clipping_offsets_are_rejected_without_shrinking()
	{
		Should.Throw<BusinessException>(() => IdentityCardGeometry.Sheet(new() { WidthMm = 200, HeightMm = 200 }, new() { Margin = 30 }));
		Should.Throw<BusinessException>(() => IdentityCardGeometry.Sheet(Layout(), new() { BackOffsetXmm = 30 }));
	}

	[Fact]
	public void Text_overflow_returns_actionable_error_instead_of_truncation()
	{
		var element = new IdentityCardLayoutElement { Id = "name", Type = "text", W = 40, H = 12, Style = new() { FontSize = 12 } };
		var error = Should.Throw<BusinessException>(() => IdentityCardSvgRenderer.RenderText(element, "A very long student name", true));
		error.Data["detail"]!.ToString()!.ShouldContain("Enlarge");
	}

	[Fact]
	public void Schema_two_requires_physical_dimensions_and_rejects_unknown_bindings()
	{
		var layout = Layout();
		layout.WidthMm = null;
		Should.Throw<BusinessException>(() => IdentityCardSchema.ParseLayout(JsonSerializer.Serialize(layout), ["name"], 2));
		layout.WidthMm = 85.6f;
		layout.Front.Elements[0].FieldKey = "privateBankAccount";
		Should.Throw<BusinessException>(() => IdentityCardSchema.ParseLayout(JsonSerializer.Serialize(layout), ["name"], 2));
	}

	private static IdentityCardLayout Layout() => new()
	{
		Width = 428, Height = 270, WidthMm = 85.6f, HeightMm = 53.98f,
		Front = new() { Elements = [new() { Id = "front", Type = "text", Value = "FRONT 001", X = 20, Y = 40, W = 380, H = 60, Style = new() { FontSize = 24 } }] },
		Back = new() { Elements = [new() { Id = "back", Type = "text", Value = "BACK 001", X = 20, Y = 40, W = 380, H = 60, Style = new() { FontSize = 24 } }] }
	};
	private static IdentityCardArtwork Artwork()
	{
		var layout = Layout();
		string Render(bool back) => IdentityCardSvgRenderer.Render(layout, back, e => e.Value, _ => true, null, null, "test", "TEST");
		return new(Render(false), Render(true));
	}
	private static int PageCount(byte[] pdf) => Regex.Matches(Encoding.Latin1.GetString(pdf), @"/Type\s*/Page\b").Count;
	private static void SaveEvidence(string fileName, byte[] pdf)
	{
		var directory = Environment.GetEnvironmentVariable("ID_CARD_ARTIFACTS_PATH");
		if (directory == null) return;
		Directory.CreateDirectory(directory);
		File.WriteAllBytes(Path.Combine(directory, fileName), pdf);
	}
}
