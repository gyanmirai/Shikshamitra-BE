using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Common.PDF;
using ShikshaMitra.Common.PDF.Elements;
using Volo.Abp.DependencyInjection;
using Q = QuestPDF.Infrastructure;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmGeneratedDocumentRenderer(
	AdmissionCrmGeneratedDocumentAvailabilityReader availabilityReader,
	IPdfService pdfService) : ITransientDependency
{
	public async Task<AdmissionCrmGeneratedDocumentDto> RenderAsync(
		AdmissionInquiryLead lead,
		AdmissionCrmGeneratedDocumentType documentType,
		string language,
		DateTimeOffset generatedAt,
		bool isPreview,
		string? reason)
	{
		var documentTypeName = AdmissionCrmGeneratedDocumentText.TypeName(documentType, language);
		var applicantName = string.Join(" ", new[] { lead.FirstName, lead.MiddleName, lead.LastName }
			.Where(value => !string.IsNullOrWhiteSpace(value))).Trim();
		var hallTicket = await availabilityReader.EnsureAvailableAsync(lead, documentType);
		var lines = CreateHeaderLines(lead, documentTypeName, applicantName, language, generatedAt);
		if (hallTicket != null)
		{
			lines.Add(new TextElement { Text = $"Exam date: {hallTicket.StartTime:dd MMM yyyy}", Padding = 4, PaddingUnit = Q.Unit.Point });
			lines.Add(new TextElement { Text = $"Exam time: {hallTicket.StartTime:hh:mm tt} - {hallTicket.EndTime:hh:mm tt}", Padding = 4, PaddingUnit = Q.Unit.Point });
			lines.Add(new TextElement { Text = $"Venue: {hallTicket.Venue}", Padding = 4, PaddingUnit = Q.Unit.Point });
		}

		lines.Add(new TextElement { Text = AdmissionCrmGeneratedDocumentText.Instruction(documentType), Padding = 12, PaddingUnit = Q.Unit.Point });
		if (!string.IsNullOrWhiteSpace(reason))
		{
			lines.Add(new TextElement { Text = AdmissionCrmGeneratedDocumentText.Label("Reason", language) + ": " + reason, Padding = 4, PaddingUnit = Q.Unit.Point });
		}

		var content = await pdfService.GeneratePdfAsBytes(new PDFDocument
		{
			Page = new PageSpecs { BackgroundColor = "#ffffff", Margin = 36, MarginUnit = Q.Unit.Point, DefaultFontSize = 11, PageSize = "A4" },
			Header = new TextElement { Text = "ShikshaMitra Admissions", FontSize = 20, FontWeight = Q.FontWeight.Bold, Color = "#163a5f", Alignment = TextAlignment.Center, Padding = 8, PaddingUnit = Q.Unit.Point },
			Content = new ColumnElement { Elements = lines, Spacing = 6 }
		});
		var fileName = $"admissions-{AdmissionCrmGeneratedDocumentText.Slug(documentType)}-{lead.Id:N}-{generatedAt:yyyyMMddHHmmss}.pdf";
		return new AdmissionCrmGeneratedDocumentDto
		{
			LeadId = lead.Id,
			AcademicYearId = lead.AcademicYearId,
			DocumentType = documentType,
			DocumentTypeName = documentTypeName,
			Language = language,
			FileName = fileName,
			Content = content,
			IsPreview = isPreview,
			GeneratedAt = generatedAt
		};
	}

	private static List<ShikshaMitra.Common.PDF.Elements.IElement> CreateHeaderLines(
		AdmissionInquiryLead lead,
		string documentTypeName,
		string applicantName,
		string language,
		DateTimeOffset generatedAt) =>
		[
			new TextElement { Text = documentTypeName, FontSize = 18, FontWeight = Q.FontWeight.Bold, Alignment = TextAlignment.Center, Padding = 8, PaddingUnit = Q.Unit.Point },
			new TextElement { Text = AdmissionCrmGeneratedDocumentText.Label("Applicant", language) + ": " + (string.IsNullOrWhiteSpace(applicantName) ? "Applicant" : applicantName), Padding = 4, PaddingUnit = Q.Unit.Point },
			new TextElement { Text = AdmissionCrmGeneratedDocumentText.Label("Contact", language) + ": " + (lead.PrimaryContactNo ?? lead.Email ?? "Not provided"), Padding = 4, PaddingUnit = Q.Unit.Point },
			new TextElement { Text = AdmissionCrmGeneratedDocumentText.Label("Desired class", language) + ": " + (lead.DesiredClassName ?? "Not specified"), Padding = 4, PaddingUnit = Q.Unit.Point },
			new TextElement { Text = AdmissionCrmGeneratedDocumentText.Label("Admission intent", language) + ": " + lead.AdmissionIntent, Padding = 4, PaddingUnit = Q.Unit.Point },
			new TextElement { Text = AdmissionCrmGeneratedDocumentText.Label("Generated", language) + ": " + generatedAt.ToString("dd MMM yyyy, HH:mm", CultureInfo.InvariantCulture), Padding = 4, PaddingUnit = Q.Unit.Point },
			new TextElement { Text = AdmissionCrmGeneratedDocumentText.Label("Reference", language) + ": " + lead.Id.ToString("N")[..12].ToUpperInvariant(), Padding = 4, PaddingUnit = Q.Unit.Point }
		];
}
