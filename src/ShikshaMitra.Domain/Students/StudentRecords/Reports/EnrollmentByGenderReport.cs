using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using ShikshaMitra.Students.StudentEnrollments;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ShikshaMitra.Students.StudentRecords.Reports;

internal class EnrollmentByGenderReport : IDocument
{
	private readonly IReadOnlyList<StudentEnrollment> _enrollments;

	public EnrollmentByGenderReport(IReadOnlyList<StudentEnrollment> enrollments)
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
			page.Header().Text("Student Enrollment Report - By Gender").FontSize(20).Bold();
			//page.Content().Column(column =>
			//{
			//	column.Spacing(10);
			//	var genderGroups = _enrollments.GroupBy(e => e.GenderType)
			//								   .Select(g => new { Gender = g.Key, Count = g.Count() });

			//	foreach (var group in genderGroups)
			//	{
			//		column.Item().Text($"Gender: {group.Gender} - Count: {group.Count}");
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
