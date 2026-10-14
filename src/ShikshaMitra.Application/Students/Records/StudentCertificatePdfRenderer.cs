using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ShikshaMitra.Students.Records;

public static class StudentCertificatePdfRenderer
{
	public static byte[] Render(StudentCertificateResponseDto certificate)
	{
		QuestPDF.Settings.License = LicenseType.Community;
		var content = certificate.Template.Document ?? StudentCertificateDocumentFactory.Create(certificate);
		return Document.Create(document => document.Page(page =>
		{
			page.Size(PageSizes.A4);
			page.Margin(36);
			page.DefaultTextStyle(style => style.FontFamily("Lato").FontSize(10).FontColor(Colors.Grey.Darken4));
			page.Content().Border(1).BorderColor(Colors.Grey.Lighten1).Padding(24).Column(column =>
			{
				column.Spacing(16);
				column.Item().AlignCenter().Text(certificate.Organization.OrganizationName).FontSize(19).Bold();
				if (!string.IsNullOrWhiteSpace(certificate.Organization.Address))
					column.Item().AlignCenter().Text(certificate.Organization.Address).FontSize(9);
				column.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
				column.Item().AlignCenter().Text(content.Title.ToUpperInvariant()).FontSize(16).Bold();
				column.Item().Row(row =>
				{
					row.RelativeItem().Text($"Certificate no: {certificate.CertificateNo}").FontSize(9);
					row.RelativeItem().AlignRight().Text($"Issue date: {certificate.IssueDate:yyyy-MM-dd}").FontSize(9);
				});
				// Preserve configured wording, but avoid repeating the default letterhead and signature text.
				if (certificate.Template.HeaderContent != StudentCertificateTemplate.DefaultHeader(System.Enum.Parse<Students.StudentRecords.StudentCertificateType>(certificate.CertificateType)))
					column.Item().Text(certificate.Template.RenderedHeaderContent);
				column.Item().Text(content.Body).LineHeight(1.6f);
				column.Item().Table(table =>
				{
					table.ColumnsDefinition(columns => { columns.ConstantColumn(155); columns.RelativeColumn(); });
					foreach (var field in content.Particulars)
					{
						table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).PaddingVertical(7).PaddingRight(10).Text(field.Label).SemiBold();
						table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).PaddingVertical(7).Text(field.Value);
					}
				});
				if (certificate.Template.FooterContent != StudentCertificateTemplate.DefaultFooter())
					column.Item().Text(certificate.Template.RenderedFooterContent).LineHeight(1.5f);
				column.Item().PaddingTop(45).Row(row =>
				{
					row.RelativeItem().AlignCenter().Text("School seal").FontSize(10);
					row.RelativeItem().AlignCenter().Column(signature =>
					{
						signature.Item().LineHorizontal(0.5f);
						signature.Item().PaddingTop(6).AlignCenter().Text("Principal / authorised signatory").FontSize(10);
					});
				});
			});
			page.Footer().PaddingTop(10).AlignCenter().Text(text =>
			{
				text.Span($"{certificate.CertificateNo}  |  Page ").FontSize(8).FontColor(Colors.Grey.Darken1);
				text.CurrentPageNumber().FontSize(8);
			});
		})).GeneratePdf();
	}
}
