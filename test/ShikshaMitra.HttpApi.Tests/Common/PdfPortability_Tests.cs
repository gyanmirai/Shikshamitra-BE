using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using NSubstitute;
using QuestPDF.Infrastructure;
using ShikshaMitra.Academic.Reports;
using ShikshaMitra.Common;
using ShikshaMitra.Common.PDF;
using ShikshaMitra.Common.PDF.Elements;
using ShikshaMitra.HR.Reports;
using ShikshaMitra.HR.ProfessionalManagement;
using ShikshaMitra.Library.Reports;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Common.Tests;

public class PdfPortability_Tests
{
	[Theory]
	[InlineData("hr")]
	[InlineData("library")]
	[InlineData("academic")]
	[InlineData("timetable")]
	[InlineData("dynamic")]
	[InlineData("generic")]
	[InlineData("professional")]
	public async Task Existing_generators_render_Gujarati_without_system_fonts(string generator)
	{
		QuestPDF.Settings.License = LicenseType.Community;
		var row = new Dictionary<string, string?> { ["name"] = "દીપ્તિ શાહ" };
		var academic = new AcademicReportPreviewDto
		{
			ReportType = "ગુજરાતી અહેવાલ",
			Columns = [new() { Id = "name", Label = "કર્મચારી" }],
			Rows = [row],
			TotalCount = 1
		};
		var bytes = generator switch
		{
			"hr" => await new HRPdfService().GenerateReportPdf(new HrReportPreviewDto
			{
				ReportType = "ગુજરાતી અહેવાલ",
				Columns = [new() { Id = "name", Label = "કર્મચારી" }],
				Rows = [row],
				TotalCount = 1
			}),
			"library" => await new LibraryPdfService().GenerateReportPdfAsync(new LibraryReportPreviewDto
			{
				ReportType = "ગુજરાતી અહેવાલ",
				Columns = [new() { Id = "name", Label = "કર્મચારી" }],
				Rows = [row],
				TotalCount = 1
			}),
			"academic" => await new AcademicPdfService().GenerateReportPdf(academic),
			"timetable" => await new TimetableReportsPdfService().GenerateAsync(academic),
			"dynamic" => new AcademicDynamicReportPdfService().Generate(new() { Title = "ગુજરાતી અહેવાલ", Summary = "કર્મચારી" }, academic),
			"generic" => await new QuestPdfService(Substitute.For<IFontService>(), Substitute.For<IImageService>())
				.GeneratePdfAsBytes(new PDFDocument { Page = new PageSpecs(), Content = new TextElement { Text = "દીપ્તિ શાહ" } }),
			"professional" => ProfessionalReviewPdfComposer.Compose(new ProfessionalReviewDto
			{
				EmployeeName = "દીપ્તિ શાહ",
				CycleName = "ગુજરાતી અહેવાલ"
			}, [], [], [], [], DateTime.UtcNow),
			_ => throw new ArgumentOutOfRangeException(nameof(generator))
		};
		Encoding.ASCII.GetString(bytes, 0, 5).ShouldBe("%PDF-");
		bytes.Length.ShouldBeGreaterThan(1000);
		QuestPDF.Settings.UseSystemFonts.ShouldBeFalse();
		QuestPDF.Settings.ThrowOnMissingFontFamilies.ShouldBeTrue();
		QuestPDF.Settings.ThrowOnMissingTextGlyphs.ShouldBeTrue();
		var output = Environment.GetEnvironmentVariable("PAYSLIP_VERIFY_ARTIFACTS");
		if (!string.IsNullOrWhiteSpace(output))
		{
			Directory.CreateDirectory(output);
			await File.WriteAllBytesAsync(Path.Combine(output, "portable-" + generator + ".pdf"), bytes);
		}
	}
}