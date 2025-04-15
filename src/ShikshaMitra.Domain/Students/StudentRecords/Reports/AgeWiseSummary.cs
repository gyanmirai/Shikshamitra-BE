using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using ShikshaMitra.Students.StudentEnrollments;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShikshaMitra.Students.StudentRecords.Reports
{
	internal class AgeWiseSummary : IDocument
	{
		private readonly IReadOnlyList<Student> _enrollments;
		private readonly List<AgeGroup> _ageWiseStudentList;

		public AgeWiseSummary(IReadOnlyList<Student> enrollments)
		{
			_enrollments = enrollments;
			_ageWiseStudentList = GetStudentByAge();
		}

		public static int CalculateAge(DateTimeOffset dateOfBirth)
		{
			var today = DateTimeOffset.UtcNow; // Use UTC time to avoid timezone issues
			int age = today.Year - dateOfBirth.Year;

			// Adjust if birthday hasn't occurred yet this year
			if (dateOfBirth.Date > today.Date.AddYears(-age))
			{
				age--;
			}

			return age;
		}

		List<AgeGroup> GetStudentByAge()
		{
			List<AgeGroup> ageGroups = _enrollments?
				.Where(s => s.DateOfBirth.HasValue) // Ignore students with null DOB
				.GroupBy(s => CalculateAge(s.DateOfBirth.Value)) // Use .Value safely
				.Select(g => new AgeGroup
				{
					Age = g.Key,
					MaleCount = g.Count(s => s.GenderType == Common.GenderType.Male),
					FemaleCount = g.Count(s => s.GenderType == Common.GenderType.Female)
				})
				.ToList() ?? new List<AgeGroup>();

			return ageGroups;
		}

		public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

		public void Compose(IDocumentContainer container)
		{
			container.Page(page =>
			{
				page.Size(PageSizes.A4);
				page.Margin(50);
				page.DefaultTextStyle(x => x.FontFamily("Arial"));

				page.Header().Element(ComposeHeader);
				page.Content().Element(ComposeContent);
				page.Footer().Element(ComposeFooter);
			});
		}

		void ComposeHeader(IContainer container)
		{
			container.Column(column =>
			{
				column.Item().Row(row =>
				{
					row.RelativeItem().AlignCenter().Column(c =>
					{
						c.Item().Text("Header Part");
					});
				});
			});
		}

		void ComposeContent(IContainer container)
		{
			container.PaddingVertical(20).Column(column =>
			{
				// Report Title
				// add line for whole page width
				column.Item().PaddingTop(10).BorderBottom(1).BorderColor(Colors.Black);

				column.Item().PaddingTop(10).Text("Age Wise Summary")
					.FontSize(16).Bold().AlignCenter();

				column.Item().PaddingTop(10).BorderBottom(1).BorderColor(Colors.Black);

				// Student Details Section
				column.Item().PaddingTop(20).Table(table =>
				{
					table.ColumnsDefinition(columns =>
					{
						columns.RelativeColumn();
						columns.RelativeColumn();
						columns.RelativeColumn();
						columns.RelativeColumn();
					});

					AddDetailRow(table, "Batch", "2024-2025");
					AddDetailRow(table, "Class", "02");
					AddDetailRow(table, "Division", "A");
					AddDetailRow(table, "As on Date", "17/08/2024");


				});

				// Academic Performance Section
				column.Item().PaddingTop(20).Text("")
					.FontSize(14)
					.Bold();

				column.Item().Table(table =>
				{
					table.ColumnsDefinition(columns =>
					{
						columns.RelativeColumn();
						columns.RelativeColumn();
						columns.RelativeColumn();
						columns.RelativeColumn();
					});

					// Academic Performance Table Header
					table.Header(header =>
					{
						header.Cell().Border(1).Background(Colors.Grey.Lighten2).Padding(5).Text("Age Year").Bold();
						header.Cell().Border(1).Background(Colors.Grey.Lighten2).Padding(5).Text("Male").Bold();
						header.Cell().Border(1).Background(Colors.Grey.Lighten2).Padding(5).Text("Female").Bold();
						header.Cell().Border(1).Background(Colors.Grey.Lighten2).Padding(5).Text("Total").Bold();
					});

					// Sample data rows

					foreach (var item in _ageWiseStudentList)
					{
						table.Cell().Border(1).Padding(5).Text(item.Age + " year");
						table.Cell().Border(1).Padding(5).Text(item.MaleCount);
						table.Cell().Border(1).Padding(5).Text(item.FemaleCount);
						table.Cell().Border(1).Padding(5).Text(item.MaleCount + item.FemaleCount);
					}
				});
			});
		}

		void ComposeFooter(IContainer container)
		{
			container.Row(row =>
			{
				row.RelativeItem().AlignLeft().Text(text =>
				{
					text.CurrentPageNumber();
					text.Span(" / ");
					text.TotalPages();
				});
				row.RelativeItem().AlignRight().Text($"Generated on {DateTime.Now:dd MMM yyyy}");
			});
		}

		private void AddDetailRow(TableDescriptor table, string label, string value)
		{
			table.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text(label).Bold();
			table.Cell().Padding(5).Text(value);
		}
	}
}
