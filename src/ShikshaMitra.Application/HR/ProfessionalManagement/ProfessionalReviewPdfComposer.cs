using System;
using System.Collections.Generic;
using System.Linq;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ShikshaMitra.HR.ProfessionalManagement;

internal static class ProfessionalReviewPdfComposer
{
	public static byte[] Compose(
		ProfessionalReviewDto review,
		IReadOnlyCollection<ClassroomObservationDto> observations,
		IReadOnlyCollection<PerformanceEvidenceDto> evidence,
		IReadOnlyCollection<PerformanceFeedbackDto> feedback,
		IReadOnlyCollection<PerformanceAuditEventDto> audit,
		DateTime generatedAt)
	{
		var criterionNames = review.Template?.Sections
			.SelectMany(section => section.Criteria)
			.ToDictionary(criterion => criterion.Id, criterion => criterion.Name) ?? new Dictionary<Guid, string>();

		return Document.Create(container =>
		{
			container.Page(page =>
			{
				page.Size(PageSizes.A4);
				page.Margin(28);
				page.DefaultTextStyle(style => style.FontSize(9).FontColor(Colors.Grey.Darken4));
				page.Header().Column(column =>
				{
					column.Item().Text("Performance Management Appraisal").FontSize(18).Bold().FontColor("#0F172A");
					column.Item().Text("Indian school HR appraisal record").FontSize(9).FontColor(Colors.Grey.Darken1);
					column.Item().PaddingTop(8).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
				});
				page.Content().PaddingTop(12).Column(column =>
				{
					column.Spacing(12);
					column.Item().Element(c => ComposeKeyValueBlock(c, "Employee And Cycle", new[] { ("Employee", review.EmployeeName), ("Employee Code", review.EmployeeIdentifier ?? "-"), ("Role", review.EmployeeRole ?? "-"), ("Cycle", review.CycleName), ("Template", review.TemplateName ?? "-"), ("Final Rating", review.FinalRating ?? "-"), ("Final Score", review.FinalScore?.ToString("0.##") ?? "-") }));
					column.Item().Text("Reviewer Stages").Bold().FontSize(12).FontColor("#0F172A");
					foreach (var stage in review.Stages.OrderBy(x => x.StageType))
					{
						column.Item().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(8).Column(stageColumn =>
						{
							stageColumn.Item().Text($"{stage.StageType} - {stage.Status}").Bold();
							if (!string.IsNullOrWhiteSpace(stage.SummaryComment)) stageColumn.Item().Text(stage.SummaryComment);
							foreach (var response in stage.Responses.Take(12))
							{
								var criterionName = criterionNames.GetValueOrDefault(response.CriterionId) ?? $"Criterion {response.CriterionId}";
								stageColumn.Item().Text($"{criterionName}: {(response.IsNotApplicable ? "Not Applicable" : response.Rating?.ToString() ?? "-")} - {response.Comment ?? "-"}").FontSize(8);
							}
						});
					}
					column.Item().Element(c => ComposeSimpleList(c, "Classroom Observations", observations.Select(x => $"{x.ObservationDate:dd MMM yyyy}: {x.ClassName ?? "-"} {x.SubjectName ?? "-"} - {x.Notes ?? "-"}")));
					column.Item().Element(c => ComposeSimpleList(c, "Evidence Summary", evidence.Select(x => $"{x.Title}: {x.FileName ?? x.Url ?? x.Notes ?? "-"}")));
					column.Item().Element(c => ComposeSimpleList(c, "Qualitative Feedback", feedback.Select(x => $"{x.Source}: {x.ProviderName ?? "Anonymous"} - {x.FeedbackText}")));
					if (review.Outcome != null)
					{
						column.Item().Element(c => ComposeKeyValueBlock(c, "Outcome And Recommendations", new[] { ("Strengths", review.Outcome.Strengths ?? "-"), ("Improvement Areas", review.Outcome.ImprovementAreas ?? "-"), ("Recommendation", review.Outcome.Recommendation.ToString()), ("Increment Recommended", review.Outcome.IncrementRecommended ? "Yes" : "No"), ("Promotion Recommended", review.Outcome.PromotionRecommended ? "Yes" : "No"), ("Notes", review.Outcome.RecommendationNotes ?? "-") }));
					}
					column.Item().Element(c => ComposeSimpleList(c, "Audit Trail", audit.Take(10).Select(x => $"{x.CreationTime:dd MMM yyyy HH:mm}: {x.EventType} - {x.Summary}")));
					column.Item().PaddingTop(12).Text("Acknowledgement: appraisal outcomes are recommendations only and do not update payroll automatically.").Italic().FontColor(Colors.Grey.Darken1);
				});
				page.Footer().AlignCenter().Text(text =>
				{
					text.Span("Generated ");
					text.Span(generatedAt.ToString("dd MMM yyyy"));
					text.Span(" | Page ");
					text.CurrentPageNumber();
					text.Span(" of ");
					text.TotalPages();
				});
			});
		}).GeneratePdf();
	}

	private static void ComposeKeyValueBlock(IContainer container, string title, IEnumerable<(string Label, string Value)> rows)
	{
		container.Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(column =>
		{
			column.Item().Text(title).Bold().FontSize(12).FontColor("#0F172A");
			column.Item().PaddingTop(6).Table(table =>
			{
				table.ColumnsDefinition(columns => { columns.ConstantColumn(130); columns.RelativeColumn(); });
				foreach (var row in rows)
				{
					table.Cell().PaddingVertical(2).Text(row.Label).SemiBold();
					table.Cell().PaddingVertical(2).Text(row.Value);
				}
			});
		});
	}

	private static void ComposeSimpleList(IContainer container, string title, IEnumerable<string> rows)
	{
		var items = rows.Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
		container.Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(column =>
		{
			column.Item().Text(title).Bold().FontSize(12).FontColor("#0F172A");
			if (items.Count == 0)
			{
				column.Item().PaddingTop(4).Text("-");
				return;
			}
			foreach (var item in items) column.Item().PaddingTop(4).Text(item);
		});
	}
}
