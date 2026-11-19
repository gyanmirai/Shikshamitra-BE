using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using ShikshaMitra.Common.PDF;

namespace ShikshaMitra.TenantSettings.SchoolIdentity;

internal static class SchoolIdentityPreviewRenderer
{
	internal static byte[] Render(string schoolName, SchoolIdentityProfileDto profile, SchoolIdentityDocumentAssets images)
	{
		QuestPDF.Settings.License = LicenseType.Community;
		return Document.Create(document => document.Page(page =>
		{
			page.Size(PageSizes.A4); page.Margin(36);
			page.DefaultTextStyle(style => style.FontSize(11).WithPortableFonts());
			page.Content().Column(column =>
			{
				column.Spacing(16);
				if (images.Banner != null) column.Item().MaxHeight(100).Image(images.Banner).FitArea();
				else
				{
					if (images.Logo != null) column.Item().AlignCenter().Width(65).Height(65).Image(images.Logo).FitArea();
					column.Item().AlignCenter().Text(schoolName).FontSize(20).Bold();
				}
				column.Item().Text("School Identity Preview").FontSize(16).Bold();
				column.Item().Text("Preview only. This page demonstrates the selected assets and is not an issued school document.");
				foreach (var (kind, item) in profile.Effective)
					column.Item().Text($"{kind}: {item.Source}; {(item.Selection.Mode == SchoolIdentityAssetMode.Hidden ? "Do not display" : item.Asset?.FileName ?? "Not configured")}");
				column.Item().PaddingTop(30).AlignRight().Column(signature =>
				{
					if (images.Signature != null) signature.Item().Width(120).Height(40).Image(images.Signature).FitArea();
					if (images.PrincipalName != null) signature.Item().Text(images.PrincipalName);
					if (images.Designation != null) signature.Item().Text(images.Designation);
				});
			});
			page.Footer().AlignCenter().Text("Preview only").FontSize(9);
		})).GeneratePdf();
	}
}
