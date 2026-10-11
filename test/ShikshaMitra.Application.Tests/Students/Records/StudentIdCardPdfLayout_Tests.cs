using System;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using ShikshaMitra.Students.Records;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class StudentIdCardPdfLayout_Tests
{
	[Theory]
	[InlineData("Horizontal", "formal-student", false)]
	[InlineData("Horizontal", "photo-first", true)]
	[InlineData("Vertical", "vertical-badge", true)]
	public void Student_card_layout_renders_pdf_for_orientations_and_qr(string orientation, string preset, bool qr)
	{
		QuestPDF.Settings.License = LicenseType.Community;
		var card = new StudentIdCardDto
		{
			StudentId = Guid.NewGuid(),
			CardNumber = "STU-2026-000001",
			StudentSnapshot = new() { ["StudentName"] = "Test Student", ["GRNumber"] = "123", ["Class"] = "8", ["Division"] = "A" },
			TemplateSnapshot = new StudentIdCardTemplateDto
			{
				SchoolName = "Test School", Orientation = orientation, PresetKey = preset, ShowQrCode = qr,
				Fields = ["StudentName", "GRNumber", "Class", "Division"]
			}
		};
		var vertical = orientation == "Vertical";
		var pdf = Document.Create(document => document.Page(page =>
		{
			page.Size(vertical ? 54 : 85.6f, vertical ? 85.6f : 54, Unit.Millimetre);
			page.Margin(0);
			page.Content().Element(container => StudentIdCardAppService.ComposeCard(container, card, null));
		})).GeneratePdf();
		pdf.AsSpan(0, 4).SequenceEqual("%PDF"u8).ShouldBeTrue();
	}
}
