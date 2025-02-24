using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using ShikshaMitra.Students.StudentEnrollments;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ShikshaMitra.Students.StudentRecords.Reports;

internal class EnrollmentByAgeReport : IDocument
{
	private readonly IReadOnlyList<StudentEnrollment> _enrollments;

	public EnrollmentByAgeReport(IReadOnlyList<StudentEnrollment> enrollments)
	{
		_enrollments = enrollments;
	}

	public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

	public void Compose(IDocumentContainer container)
	{
		container.Page(page =>
		{
			page.Size(QuestPDF.Helpers.PageSizes.A4);
			page.Margin(2, Unit.Centimetre);
			page.Header().Text("Student Enrollment Report - By Age").FontSize(20).Bold();
			//page.Content().Column(column =>
			//{
			//	column.Spacing(10);

			//	// Assuming StudentEnrollment has a DateOfBirth property for age calculation.
			//	var ageGroups = _enrollments
			//		.GroupBy(e => DateTimeOffset.UtcNow.Year - e.DateOfBirth.Value.Year)
			//		.Select(g => new { Age = g.Key, Count = g.Count() })
			//		.OrderBy(g => g.Age);

			//	foreach (var group in ageGroups)
			//	{
			//		column.Item().Text($"Age: {group.Age} - Count: {group.Count}");
			//	}
			//});
			page.Footer()
				.AlignCenter()
				.Text(text =>
				{
					text.Span("Generated on ");
					text.Span(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"));
				});
		});
	}
}
