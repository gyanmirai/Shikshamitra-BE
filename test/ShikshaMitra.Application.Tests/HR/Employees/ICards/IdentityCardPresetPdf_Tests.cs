using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Common.IdentityCards;

public class IdentityCardPresetPdf_Tests
{
	[Theory]
	[InlineData("classic-identity")]
	[InlineData("staff-professional")]
	public void Approved_frontend_presets_validate_and_render_both_sides(string preset)
	{
		var layout = Load(preset);
		var values = Values();
		string Render(bool back) => IdentityCardSvgRenderer.Render(layout, back, e => e.FieldKey == "static" ? e.Value : values.GetValueOrDefault(e.FieldKey ?? ""), _ => true, null, null, "opaque-id", "STU-0001");
		var pdf = IdentityCardPdfComposer.Compose(layout, new() { PageSize = "Card" }, [new(Render(false), Render(true))], true);
		pdf.AsSpan(0, 4).SequenceEqual("%PDF"u8).ShouldBeTrue();
		var directory = Environment.GetEnvironmentVariable("ID_CARD_ARTIFACTS_PATH");
		if (directory != null)
		{
			Directory.CreateDirectory(directory);
			File.WriteAllBytes(Path.Combine(directory, preset + ".pdf"), pdf);
		}
	}

	[Theory]
	[InlineData("classic-identity", "StudentName")]
	[InlineData("staff-professional", "name")]
	public void Overlong_identity_text_is_rejected_in_the_approved_preset(string preset, string field)
	{
		var layout = Load(preset);
		Should.Throw<BusinessException>(() => IdentityCardSvgRenderer.Render(layout, false, e => e.FieldKey == field ? new string('W', 80) : e.Value ?? "Sample", _ => true, null, null, "opaque-id", "TEST"));
	}

	[Fact]
	public void Logo_containment_preserves_rectangular_artwork_while_photo_fills_its_frame()
	{
		var element = new IdentityCardLayoutElement { Id = "media", Type = "logo", W = 200, H = 40 };
		var png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aRZkAAAAASUVORK5CYII=");
		IdentityCardMediaRenderer.RenderImageOrPlaceholder(element, png, "#000000", "LOGO").ShouldContain("preserveAspectRatio=\"xMidYMid meet\"");
		element.Type = "photo";
		IdentityCardMediaRenderer.RenderImageOrPlaceholder(element, png, "#000000", "PHOTO").ShouldContain("preserveAspectRatio=\"xMidYMid slice\"");
	}

	[Fact]
	public void Qr_svg_is_embedded_as_vector_markup_instead_of_an_unsupported_svg_image()
	{
		var svg = IdentityCardMediaRenderer.RenderEmbeddedSvg(new() { X = 10, Y = 20, W = 44, H = 44 }, IdentityCardMediaRenderer.BuildQrSvg("opaque-id"));
		svg.ShouldContain("<svg");
		svg.ShouldContain("<rect");
		svg.ShouldContain("viewBox");
		svg.ShouldNotContain("data:image/svg");
	}

	private static IdentityCardLayout Load(string preset)
	{
		var fields = preset == "classic-identity"
			? new[] { "StudentName", "GRNumber", "Class", "Division", "AcademicYear", "CardNumber", "schoolName", "schoolAddress", "headerText", "footerText", "static" }
			: ["name", "employeeCode", "designation", "department", "organization", "validityYear", "schoolName", "schoolAddress", "static"];
		return IdentityCardSchema.ParseLayout(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "HR", "Employees", "ICards", "Fixtures", preset + ".json")), fields, 2, true);
	}
	private static Dictionary<string, string> Values() => new()
	{
		["StudentName"] = "Aarav Sharma", ["name"] = "Meera Iyer", ["GRNumber"] = "GR-2026-1042", ["employeeCode"] = "EMP-0042",
		["designation"] = "Senior teacher", ["department"] = "Academics", ["organization"] = "English Primary",
		["Class"] = "Class 8", ["Division"] = "A", ["AcademicYear"] = "2026–2027", ["validityYear"] = "2026–2027",
		["CardNumber"] = "STU-2026-000042", ["schoolName"] = "Greenwood Public School", ["schoolAddress"] = "21 School Road, Pune",
		["headerText"] = "Student identity card", ["footerText"] = "If found, return this card to the school office."
	};
}
