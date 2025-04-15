using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using ShikshaMitra.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShikshaMitra.Students.StudentRecords.Reports
{
	public class CasteCategoryWiseSummary : IDocument
	{

		private readonly IReadOnlyList<Student> _students;
		private readonly IReadOnlyList<CastGenderSummary> _castGenderSummaries;

		public CasteCategoryWiseSummary(IReadOnlyList<Student> students)
		{
			_students = students;
			_castGenderSummaries = ConvertDataToCasteCategory();

		}
		public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

        public List<CastGenderSummary> ConvertDataToCasteCategory()
        {
            var rawData = _students
                .Where(s => s.Standard != null)
                .Select(s => new
                {
                    StandardId = s.Standard.Id,
                    StandardName = s.Standard.Name,
                    CastType = s.CastType.ToString().ToLower(), // ✅ enum to lowercase string
                    Gender = s.GenderType
                })
                .ToList();

            var result = rawData
                .GroupBy(s => new { s.StandardId, s.StandardName })
                .Select(g => new CastGenderSummary
                {
                    StandardId = g.Key.StandardId,
					StandardName = g.Key.StandardName,
                    CastGenderCounts = g
                        .GroupBy(s => new { s.CastType, s.Gender })
                        .ToDictionary(
                            k => $"{k.Key.CastType} {(k.Key.Gender == GenderType.Female ? "f" : "m")}",
                            v => v.Count()
                        )
                })
                .ToList();

            return result;
        }

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
				column.Item().PaddingTop(10).BorderBottom(1).BorderColor(Colors.Black);

				column.Item().PaddingTop(10).AlignCenter().Text("Caste Category Wise Summary")
					.FontSize(16).Bold();

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
					AddDetailRow(table, "Status", "Active");



				});

				// Academic Performance Section
				column.Item().PaddingTop(20).Text("")
					.FontSize(14)
					.Bold();



				column.Item().PaddingTop(10).Table(table =>
				{
					table.ColumnsDefinition(columns =>
					{
						columns.RelativeColumn(); // Class
						columns.RelativeColumn(); // SC F
						columns.RelativeColumn(); // SC M
						columns.RelativeColumn(); // OBC F
						columns.RelativeColumn(); // OBC M
						columns.RelativeColumn(); // SEBC F
						columns.RelativeColumn(); // SEBC M
						columns.RelativeColumn(); // General F
						columns.RelativeColumn(); // General M
						columns.RelativeColumn(); // ST F
						columns.RelativeColumn(); // ST M
						columns.RelativeColumn(); // Total
					});

					// Table Header
					table.Header(header =>
					{
						header.Cell().Border(1).Background(Colors.Grey.Lighten2).Padding(5).Text("Class").Bold().AlignCenter();
						header.Cell().Border(1).Background(Colors.Grey.Lighten2).Padding(5).Text("SC F").Bold().AlignCenter();
						header.Cell().Border(1).Background(Colors.Grey.Lighten2).Padding(5).Text("SC M").Bold().AlignCenter();
						header.Cell().Border(1).Background(Colors.Grey.Lighten2).Padding(5).Text("OBC F").Bold().AlignCenter();
						header.Cell().Border(1).Background(Colors.Grey.Lighten2).Padding(5).Text("OBC M").Bold().AlignCenter();
						header.Cell().Border(1).Background(Colors.Grey.Lighten2).Padding(5).Text("SEBC F").Bold().AlignCenter();
						header.Cell().Border(1).Background(Colors.Grey.Lighten2).Padding(5).Text("SEBC M").Bold().AlignCenter();
						header.Cell().Border(1).Background(Colors.Grey.Lighten2).Padding(5).Text("General F").Bold().AlignCenter();
						header.Cell().Border(1).Background(Colors.Grey.Lighten2).Padding(5).Text("General M").Bold().AlignCenter();
						header.Cell().Border(1).Background(Colors.Grey.Lighten2).Padding(5).Text("ST F").Bold().AlignCenter();
						header.Cell().Border(1).Background(Colors.Grey.Lighten2).Padding(5).Text("ST M").Bold().AlignCenter();
						header.Cell().Border(1).Background(Colors.Grey.Lighten2).Padding(5).Text("Total").Bold().AlignCenter();
					});

					// Sample Data Rows (Replace with actual data)
                    foreach (var item in _castGenderSummaries)
                    {
                        AddTableRow(table, item.StandardName,
                                item.CastGenderCounts.ContainsKey("sc f") ? item.CastGenderCounts["sc f"] : 0,
                                item.CastGenderCounts.ContainsKey("sc m") ? item.CastGenderCounts["sc m"] : 0,
                                item.CastGenderCounts.ContainsKey("obc f") ? item.CastGenderCounts["obc f"] : 0,
                                item.CastGenderCounts.ContainsKey("obc m") ? item.CastGenderCounts["obc m"] : 0,
                                item.CastGenderCounts.ContainsKey("sebc f") ? item.CastGenderCounts["sebc f"] : 0,
                                item.CastGenderCounts.ContainsKey("sebc m") ? item.CastGenderCounts["sebc m"] : 0,
                                item.CastGenderCounts.ContainsKey("general f") ? item.CastGenderCounts["general f"] : 0,
                                item.CastGenderCounts.ContainsKey("general m") ? item.CastGenderCounts["general m"] : 0,
                                item.CastGenderCounts.ContainsKey("st f") ? item.CastGenderCounts["st f"] : 0,
                                item.CastGenderCounts.ContainsKey("st m") ? item.CastGenderCounts["st m"] : 0,
                                item.CastGenderCounts.Values.Sum());
                    }

                    var totalScF = _castGenderSummaries.Sum(item => item.CastGenderCounts.ContainsKey("sc f") ? item.CastGenderCounts["sc f"] : 0);
                    var totalScM = _castGenderSummaries.Sum(item => item.CastGenderCounts.ContainsKey("sc m") ? item.CastGenderCounts["sc m"] : 0);
                    var totalObcF = _castGenderSummaries.Sum(item => item.CastGenderCounts.ContainsKey("obc f") ? item.CastGenderCounts["obc f"] : 0);
                    var totalObcM = _castGenderSummaries.Sum(item => item.CastGenderCounts.ContainsKey("obc m") ? item.CastGenderCounts["obc m"] : 0);
                    var totalSebcF = _castGenderSummaries.Sum(item => item.CastGenderCounts.ContainsKey("sebc f") ? item.CastGenderCounts["sebc f"] : 0);
                    var totalSebcM = _castGenderSummaries.Sum(item => item.CastGenderCounts.ContainsKey("sebc m") ? item.CastGenderCounts["sebc m"] : 0);
                    var totalGeneralF = _castGenderSummaries.Sum(item => item.CastGenderCounts.ContainsKey("general f") ? item.CastGenderCounts["general f"] : 0);
                    var totalGeneralM = _castGenderSummaries.Sum(item => item.CastGenderCounts.ContainsKey("general m") ? item.CastGenderCounts["general m"] : 0);
                    var totalStF = _castGenderSummaries.Sum(item => item.CastGenderCounts.ContainsKey("st f") ? item.CastGenderCounts["st f"] : 0);
                    var totalStM = _castGenderSummaries.Sum(item => item.CastGenderCounts.ContainsKey("st m") ? item.CastGenderCounts["st m"] : 0);
                    var grandTotal = totalScF + totalScM + totalObcF + totalObcM + totalSebcF + totalSebcM + totalGeneralF + totalGeneralM + totalStF + totalStM;

                    AddTotalRow(table, "Grand Total", totalScF, totalScM, totalObcF, totalObcM, totalSebcF, totalSebcM, totalGeneralF, totalGeneralM, totalStF, totalStM, grandTotal);

					
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

		private void AddTableRow(TableDescriptor table, string className, int scF, int scM, int obcF, int obcM, int sebcF, int sebcM, int generalF, int generalM, int stF, int stM, int total)
		{
			table.Cell().Border(1).Padding(5).Text(className);
			table.Cell().Border(1).Padding(5).Text(scF.ToString());
			table.Cell().Border(1).Padding(5).Text(scM.ToString());
			table.Cell().Border(1).Padding(5).Text(obcF.ToString());
			table.Cell().Border(1).Padding(5).Text(obcM.ToString());
			table.Cell().Border(1).Padding(5).Text(sebcF.ToString());
			table.Cell().Border(1).Padding(5).Text(sebcM.ToString());
			table.Cell().Border(1).Padding(5).Text(generalF.ToString());
			table.Cell().Border(1).Padding(5).Text(generalM.ToString());
			table.Cell().Border(1).Padding(5).Text(stF.ToString());
			table.Cell().Border(1).Padding(5).Text(stM.ToString());
			table.Cell().Border(1).Padding(5).Text(total.ToString());
		}

		private void AddTotalRow(TableDescriptor table, string label, int scF, int scM, int obcF, int obcM, int sebcF, int sebcM, int generalF, int generalM, int stF, int stM, int total)
		{
			table.Cell().ColumnSpan(1).Border(1).Background(Colors.Grey.Lighten3).Padding(5).Text(label).Bold();
			table.Cell().Border(1).Background(Colors.Grey.Lighten3).Padding(5).Text(scF.ToString()).Bold();
			table.Cell().Border(1).Background(Colors.Grey.Lighten3).Padding(5).Text(scM.ToString()).Bold();
			table.Cell().Border(1).Background(Colors.Grey.Lighten3).Padding(5).Text(obcF.ToString()).Bold();
			table.Cell().Border(1).Background(Colors.Grey.Lighten3).Padding(5).Text(obcM.ToString()).Bold();
			table.Cell().Border(1).Background(Colors.Grey.Lighten3).Padding(5).Text(sebcF.ToString()).Bold();
			table.Cell().Border(1).Background(Colors.Grey.Lighten3).Padding(5).Text(sebcM.ToString()).Bold();
			table.Cell().Border(1).Background(Colors.Grey.Lighten3).Padding(5).Text(generalF.ToString()).Bold();
			table.Cell().Border(1).Background(Colors.Grey.Lighten3).Padding(5).Text(generalM.ToString()).Bold();
			table.Cell().Border(1).Background(Colors.Grey.Lighten3).Padding(5).Text(stF.ToString()).Bold();
			table.Cell().Border(1).Background(Colors.Grey.Lighten3).Padding(5).Text(stM.ToString()).Bold();
			table.Cell().Border(1).Background(Colors.Grey.Lighten3).Padding(5).Text(total.ToString()).Bold();
		}

		private void AddDetailRow(TableDescriptor table, string label, string value)
		{
			table.Cell().Padding(5).Text(label);
			table.Cell().Padding(5).Text(value);
		}
	}
}


public class CastGenderSummary
{
	public Guid StandardId { get; set; }

	public string StandardName { get; set; } = string.Empty;
	public Dictionary<string, int> CastGenderCounts { get; set; } = new();
}