using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using ShikshaMitra.Common;
using ShikshaMitra.Students.StudentEnrollments.Registration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace ShikshaMitra.Students.StudentRecords.Reports
{
	public class FeesCategoryWiseSummary : IDocument


	{
		private readonly IReadOnlyList<Student> _students;
		private readonly List<FeesCategoryDivisionWiseSummaryDto> _feesCategoryWiseSummaryDtos;
		public FeesCategoryWiseSummary(IReadOnlyList<Student> students)
		{
			_students = students;
			_feesCategoryWiseSummaryDtos = ConvertFeesCategoryData();
		}

		public List<FeesCategoryDivisionWiseSummaryDto> ConvertFeesCategoryData()
		{
			var rawData = _students
				.Where(s => s.Standard != null)
				.Select(s => new
				{
					StandardId = s.Standard.Id,
					StandardName = s.Standard.Name,
					CastType = s.CastType.ToString().ToLower(), // ✅ enum to lowercase string
					Gender = s.GenderType,
					StudentAdditionalDetails = s.StudentAdditionalDetails,
					Division = s.Division
				})
				.ToList();


			var result = rawData.GroupBy(s => new { s.StandardId, s.StandardName, s.Division })
				.Select(g => new FeesCategoryDivisionWiseSummaryDto
				{

					StandardDivision = $"{g.Key.StandardName}-{g.Key.Division.Name}",
					GeneralMale = g.Count(s =>
						s.StudentAdditionalDetails.RightToEducation == false && s.Gender == GenderType.Male),
					GeneralFemale = g.Count(s =>
						s.StudentAdditionalDetails.RightToEducation == false && s.Gender == GenderType.Female),
					RteMale = g.Count(s =>
						s.StudentAdditionalDetails.RightToEducation == true && s.Gender == GenderType.Male),
					RteFemale = g.Count(s =>
						s.StudentAdditionalDetails.RightToEducation == true && s.Gender == GenderType.Female)
				})
				.ToList();

			

			var standardWiseSummary = result
				.GroupBy(s => s.StandardDivision.Split('-')[0]) // Extract Standard (e.g., "1" from "1-A")
				.Select(g => new FeesCategoryDivisionWiseSummaryDto
				{
					StandardDivision = g.Key, // Standard only (e.g., "1")
					RteFemale = g.Sum(x => x.RteFemale),
					RteMale = g.Sum(x => x.RteMale),
					GeneralFemale = g.Sum(x => x.GeneralFemale),
					GeneralMale = g.Sum(x => x.GeneralMale),
					Total = g.Sum(x => x.Total) // Sum all division totals
				})
				.ToList();


			var mergeData= result.Concat(standardWiseSummary).ToList();

			return mergeData.OrderBy(x => x.StandardDivision).ToList();
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

				column.Item().PaddingTop(10).BorderBottom(1).BorderColor(Colors.Black);
				column.Item().PaddingTop(10).Text("Fees Categorywise Summary")
					.FontSize(16).Bold().AlignCenter();
				column.Item().PaddingTop(10).BorderBottom(1).BorderColor(Colors.Black);



			});
		}


		void ComposeContent(IContainer container)
		{

			container.PaddingVertical(20).Table(table =>
			{
				table.ColumnsDefinition(columns =>
				{
					columns.RelativeColumn(1); // Class
					columns.RelativeColumn(1); // RTE Female
					columns.RelativeColumn(1); // RTE Male
					columns.RelativeColumn(1); // General Female
					columns.RelativeColumn(1); // General Male
					columns.RelativeColumn(1); // Total
				});

				// Table Header
				table.Header(header =>
				{
					header.Cell().Border(1).Background(Colors.Grey.Lighten2).Padding(5).Text("Class").Bold();
					header.Cell().Border(1).Background(Colors.Grey.Lighten2).Padding(5).Text("RTE Female").Bold();
					header.Cell().Border(1).Background(Colors.Grey.Lighten2).Padding(5).Text("RTE Male").Bold();
					header.Cell().Border(1).Background(Colors.Grey.Lighten2).Padding(5).Text("General Female").Bold();
					header.Cell().Border(1).Background(Colors.Grey.Lighten2).Padding(5).Text("General Male").Bold();
					header.Cell().Border(1).Background(Colors.Grey.Lighten2).Padding(5).Text("Total").Bold();
				});

				// Sample Data Rows (Replace with actual data dynamically)
				foreach (var item in _feesCategoryWiseSummaryDtos)
				{
					bool isOnlyStandard = !item.StandardDivision.Contains("-");
					AddTableRow(table, isOnlyStandard ? "Total" : item.StandardDivision, item.RteFemale.ToString(), item.RteMale.ToString(), item.GeneralFemale.ToString(), item.GeneralMale.ToString(), item.RteFemale.ToString() + item.RteMale + item.GeneralFemale + item.GeneralMale);
					//AddTableRow(table, "BALVATIKA-B", "-", "-", "21", "16", "37");
					//AddTableRow(table, "BALVATIKA-C", "-", "-", "14", "22", "36");
					//AddTableRow(table, "BALVATIKA-D", "-", "-", "16", "21", "37");
					//AddTableRow(table, "BALVATIKA-E", "-", "-", "12", "27", "39");
					//AddTotalRow(table, "149");
				}


				//AddTableRow(table, "1-A", "1", "21", "32", "86", "236");
				//AddTableRow(table, "1-B", "-", "-", "19", "29", "48");
				//AddTableRow(table, "1-C", "-", "-", "21", "24", "45");
				//AddTableRow(table, "1-D", "-", "-", "21", "22", "43");
				//AddTableRow(table, "1-E", "-", "-", "25", "21", "46");
				//AddTableRow(table, "1-F", "1", "-", "21", "32", "54");
				//AddTotalRow(table, "236");

				//AddTableRow(table, "2-A", "17", "19", "54", "74", "164");
				//AddTableRow(table, "2-B", "1", "-", "18", "20", "39");
				//AddTableRow(table, "2-C", "-", "-", "16", "24", "40");
				//AddTotalRow(table, "164");
				AddTotalRow(table, "1092"); // Grand Total
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

		private void AddTableRow(TableDescriptor table, string className, string rteFemale, string rteMale, string genFemale, string genMale, string total)
		{
			table.Cell().Border(1).Padding(5).Text(className);
			table.Cell().Border(1).Padding(5).Text(rteFemale);
			table.Cell().Border(1).Padding(5).Text(rteMale);
			table.Cell().Border(1).Padding(5).Text(genFemale);
			table.Cell().Border(1).Padding(5).Text(genMale);
			table.Cell().Border(1).Padding(5).Text(total);
		}

		private void AddTotalRow(TableDescriptor table, string total)
		{
			table.Cell().ColumnSpan(5).Background(Colors.Grey.Lighten3).Border(1).Padding(5).Text("Total").ExtraBold();
			table.Cell().Background(Colors.Grey.Lighten3).Border(1).Padding(5).Text(total).ExtraBold();
		}
	}
}

public class FeesCategoryWiseSummaryDto
{
	public Guid Standard { get; set; }

	public string StandarName { get; set; }

	public string StandardDivision { get; set; }

	public int GeneralMale { get; set; }

	public int GeneralFemale { get; set; }

	public int RteMale { get; set; }

	public int RteFemale { get; set; }
}

public class FeesCategoryDivisionWiseSummaryDto
{
	public string StandardDivision { get; set; } // Example: "1-A" or just "1" for totals
	public int RteFemale { get; set; } // Count of RTE Female students
	public int RteMale { get; set; } // Count of RTE Male students
	public int GeneralFemale { get; set; } // Count of General Female students
	public int GeneralMale { get; set; } // Count of General Male students
	public int Total { get; set; } // Total students in that division or standard
}