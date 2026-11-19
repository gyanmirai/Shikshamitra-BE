using System;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace ShikshaMitra.Academic.Reports;

internal static class AcademicReportIdentityHeader
{
	internal static void Compose(IContainer container, AcademicReportPreviewDto report, Action<IContainer> title)
	{
		container.Column(column =>
		{
			column.Spacing(8);
			if (report.IdentityBanner != null) column.Item().MaxHeight(70).Image(report.IdentityBanner).FitArea();
			else if (report.IdentityLogo != null)
				column.Item().Row(row =>
				{
					row.ConstantItem(45).Height(45).Image(report.IdentityLogo).FitArea();
					row.RelativeItem().PaddingLeft(10).AlignMiddle().Text(report.IdentitySchoolName ?? "").FontSize(16).Bold();
				});
			column.Item().Element(title);
		});
	}
}
