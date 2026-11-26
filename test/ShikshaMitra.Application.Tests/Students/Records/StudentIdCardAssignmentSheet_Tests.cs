using System;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ShikshaMitra.Common.IdentityCards;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Students.Records;

public class StudentIdCardAssignmentSheet_Tests
{
	[Fact]
	public void Assignment_artwork_omits_print_placeholders_and_redundant_classic_year()
	{
		var card = new StudentIdCardDto
		{
			StudentSnapshot = new() { ["StudentName"] = "Synthetic Student", ["AcademicYear"] = "2031-2032" },
			TemplateSnapshot = new()
			{
				SchemaVersion = 2, Fields = ["StudentName", "AcademicYear"],
				LayoutJson = """
					{"presetKey":"classic-identity","width":428,"height":270,"widthMm":85.6,"heightMm":53.98,
					 "front":{"elements":[
					  {"id":"logo","type":"logo","x":19,"y":18,"w":34,"h":34},
					  {"id":"photo","type":"photo","x":19,"y":104,"w":84,"h":96},
					  {"id":"name","type":"text","fieldKey":"StudentName","x":120,"y":105,"w":289,"h":25},
					  {"id":"address","type":"text","fieldKey":"schoolAddress","x":66,"y":44,"w":342,"h":15},
					  {"id":"additional-record","type":"text","fieldKey":"AcademicYear","x":132,"y":195,"w":277,"h":12},
					  {"id":"validity","type":"text","fieldKey":"AcademicYear","x":19,"y":242,"w":307,"h":14},
					  {"id":"number","type":"text","fieldKey":"CardNumber","x":19,"y":221,"w":307,"h":18},
					  {"id":"barcode","type":"barcode","x":19,"y":50,"w":60,"h":20}]}}
					"""
			}
		};
		card.TemplateSnapshot.ShowPhoto = true;
		var artwork = StudentIdCardDesign.Artwork(card, null, null, forAssignmentPrint: true).FrontSvg;
		var text = XDocument.Parse(artwork).Descendants().Where(node => node.Name.LocalName == "tspan").Select(node => node.Value).ToArray();
		text.ShouldBe(new[] { "Synthetic Student", "2031-2032" }, ignoreOrder: true);
		artwork.ShouldNotContain("PHOTO");
		artwork.ShouldNotContain("LOGO");
		StudentIdCardDesign.Artwork(card, null, null).FrontSvg.ShouldContain("Preview");

		card.CardNumber = "ID-2031-000001";
		StudentIdCardDesign.Artwork(card, null, null, forAssignmentPrint: true).FrontSvg.ShouldContain(card.CardNumber);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public void Trim_marks_are_outside_the_occupied_stickers_and_point_to_actual_edges(bool back)
	{
		var settings = new IdentityCardPrintSettings { Gap = 0, BleedMm = 1, BackOffsetXmm = 2 };
		var layout = new IdentityCardLayout { Width = 428, Height = 270, WidthMm = 85.6f, HeightMm = 53.98f };
		var sheet = IdentityCardGeometry.Sheet(layout, settings);
		var first = sheet.Position(0, settings, back);
		var second = sheet.Position(1, settings, back);
		var top = Math.Min(first.Y, second.Y);
		var bottom = top + sheet.CardHeightMm;
		var left = Math.Min(first.X, second.X);
		var right = Math.Max(first.X, second.X) + sheet.CardWidthMm;
		var guides = IdentityCardSheetTrimGuides.BuildSvg(sheet, settings, 2, back);
		var lines = XDocument.Parse(guides).Descendants().Where(node => node.Name.LocalName == "line").ToArray();
		lines.Length.ShouldBe(12);
		float Coord(XElement line, string attr) => float.Parse(line.Attribute(attr)!.Value, CultureInfo.InvariantCulture);
		lines.All(line =>
			Coord(line, "y2") < top - settings.BleedMm || Coord(line, "y1") > bottom + settings.BleedMm ||
			Coord(line, "x2") < left - settings.BleedMm || Coord(line, "x1") > right + settings.BleedMm).ShouldBeTrue();
		lines.Any(line => Math.Abs(Coord(line, "x1") - left) < 0.001f && Coord(line, "x1") == Coord(line, "x2")).ShouldBeTrue();
		lines.Any(line => Math.Abs(Coord(line, "x1") - right) < 0.001f && Coord(line, "x1") == Coord(line, "x2")).ShouldBeTrue();
	}

	[Theory]
	[InlineData("Horizontal")]
	[InlineData("Vertical")]
	public void Legacy_assignments_export_on_a4_with_exact_card_slots(string orientation)
	{
		var template = new StudentIdCardTemplateDto
		{
			SchoolName = "Example School", Orientation = orientation, ShowPhoto = true, ShowQrCode = true,
			Fields = ["StudentName", "Class", "Division", "GRNumber", "AcademicYear", "DateOfBirth"]
		};
		var card = new StudentIdCardDto
		{
			StudentId = Guid.NewGuid(),
			TemplateSnapshot = template,
			StudentSnapshot = new()
			{
				["StudentName"] = "Synthetic Student", ["Class"] = "Class 2", ["Division"] = "A",
				["GRNumber"] = "GR-001", ["AcademicYear"] = "2031-2032", ["DateOfBirth"] = "01-01-2015"
			}
		};
		var pdf = StudentIdCardAppService.RenderLegacyAssignmentSheet([(card, null, null), (card, null, null)], template, 8);
		var markup = Encoding.Latin1.GetString(pdf);
		Regex.Matches(markup, @"/Type\s*/Page\b").Count.ShouldBe(1);
		var mediaBox = Regex.Match(markup, @"/MediaBox\s*\[\s*0\s+0\s+([0-9.]+)\s+([0-9.]+)\s*\]");
		mediaBox.Success.ShouldBeTrue();
		const float pdfRoundingMm = 0.05f;
		(float.Parse(mediaBox.Groups[1].Value, CultureInfo.InvariantCulture) / IdentityCardGeometry.PointsPerMm).ShouldBe(210f, pdfRoundingMm);
		(float.Parse(mediaBox.Groups[2].Value, CultureInfo.InvariantCulture) / IdentityCardGeometry.PointsPerMm).ShouldBe(297f, pdfRoundingMm);
	}
}
