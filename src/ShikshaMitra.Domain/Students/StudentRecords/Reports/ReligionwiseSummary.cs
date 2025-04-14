using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using ShikshaMitra.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Threading.Tasks;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.StudentRecords.Reports
{
	public class ReligionwiseSummary : IDocument
	{
		private readonly IReadOnlyList<Student> _students;
		public ReligionwiseSummary(IReadOnlyList<Student> students)
		{
			_students = students;
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
		public List<StandardDivisionDynamicCount> GetReligionWiseSummary()
		{
			var allReligions = _students.Select(s => s.Religion).Distinct().OrderBy(r => r).ToList();
			var allStandards = _students.Select(s => $"{s.Standard.Name}-{s.Division.Name}").Distinct().ToList();
			var grouped = _students
				.GroupBy(s => new { StandardDivisionName = $"{s.Standard.Name}-{s.Division.Name}", s.Religion })
				.ToDictionary(
					g => (g.Key.StandardDivisionName, g.Key.Religion),
					g => new
					{
						Male = g.Count(x => x.GenderType == GenderType.Male),
						Female = g.Count(x => x.GenderType == GenderType.Female)
					}
				);
			List<StandardDivisionDynamicCount> result = allStandards.Select(std =>
			{
				var model = new StandardDivisionDynamicCount
				{
					StandardDivisionName = std
				};

				foreach (var rel in allReligions)
				{
					var key = (std, rel);
					grouped.TryGetValue(key, out var counts);

					model.GenderCounts[$"{rel.ToLower()}_male"] = counts?.Male ?? 0;
					model.GenderCounts[$"{rel.ToLower()}_female"] = counts?.Female ?? 0;
				}

				return model;
			}).ToList();

			return result;
		}

		public List<string> GetFirstHeader()
		{
			List<string> result = _students.Select(s => s.Religion).Distinct().OrderBy(r => r).ToList();
			return result;
		}
		void ComposeHeader(IContainer container)
		{
			container.Column(column =>
			{
				column.Item().Row(row =>
				{
					row.RelativeItem().AlignCenter().Column(c =>
					{
						c.Item().Text("Header PArt");
					});
				});

				// add line for whole page width
				column.Item().PaddingTop(10).BorderBottom(1).BorderColor(Colors.Black);

				column.Item().PaddingTop(10).Text("Religionwise Summary")
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
					AddDetailRow(table, "As on Date", "01/01/2025");

				});
			});
		}


		void ComposeContent(IContainer container)
		{
			container.PaddingVertical(20).Table(table =>
			{
				table.ColumnsDefinition(columns =>
				{
					columns.RelativeColumn(1);

					foreach (var item in GetFirstHeader())
					{
						 columns.RelativeColumn(1);
					}
						//columns.RelativeColumn(1); // Column 1 - Test1
						//columns.RelativeColumn(1); // Column 1 - Test2
						//columns.RelativeColumn(1); // Column 2
						//columns.RelativeColumn(1); // Column 2
						//columns.RelativeColumn(1); // Column 3
						//columns.RelativeColumn(1); // Column 3
						//columns.RelativeColumn(1); // Column 4
						//columns.RelativeColumn(1); // Column 4
						columns.RelativeColumn(1); // Column 5
				});

				// Table Header (Including both main and sub-headers in one call)
				table.Header(header =>
				{
					// First Row: Merged Header for Column 1
					header.Cell().Border(1).Background(Colors.Grey.Lighten2).Padding(5).Text("Class/Div").Bold().AlignCenter();
					foreach (var item in GetFirstHeader())
					{
						header.Cell().ColumnSpan(2).Border(1).Background(Colors.Grey.Lighten2).Padding(5).Text(item).Bold().AlignCenter();

					}
					//header.Cell().ColumnSpan(2).Border(1).Background(Colors.Grey.Lighten2).Padding(5).Text("Hindu").Bold().AlignCenter();
					//header.Cell().ColumnSpan(2).Border(1).Background(Colors.Grey.Lighten2).Padding(5).Text("Muslim").Bold().AlignCenter();
					//header.Cell().ColumnSpan(2).Border(1).Background(Colors.Grey.Lighten2).Padding(5).Text("Sikh").Bold().AlignCenter();
					//header.Cell().ColumnSpan(2).Border(1).Background(Colors.Grey.Lighten2).Padding(5).Text("Cristian").Bold().AlignCenter();
					header.Cell().ColumnSpan(1).Border(1).Background(Colors.Grey.Lighten2).Padding(5).Text("Total").Bold().AlignCenter();

					// Second Row: Sub-Headers for Column 1

					header.Cell().Border(1).Background(Colors.Grey.Lighten3).Padding(5).Text("");

					foreach (var item in GetFirstHeader())
					{

						header.Cell().Border(1).Background(Colors.Grey.Lighten3).Padding(5).Text("Male").Bold().AlignCenter();
						header.Cell().Border(1).Background(Colors.Grey.Lighten3).Padding(5).Text("Female").Bold().AlignCenter();

					}

					//header.Cell().Border(1).Background(Colors.Grey.Lighten3).Padding(5).Text("Male").Bold().AlignCenter();
					//header.Cell().Border(1).Background(Colors.Grey.Lighten3).Padding(5).Text("Female").Bold().AlignCenter();
					//header.Cell().Border(1).Background(Colors.Grey.Lighten3).Padding(5).Text("Male").Bold().AlignCenter();
					//header.Cell().Border(1).Background(Colors.Grey.Lighten3).Padding(5).Text("Female").Bold().AlignCenter();
					//header.Cell().Border(1).Background(Colors.Grey.Lighten3).Padding(5).Text("Male").Bold().AlignCenter();
					//header.Cell().Border(1).Background(Colors.Grey.Lighten3).Padding(5).Text("Female").Bold().AlignCenter();
					//header.Cell().Border(1).Background(Colors.Grey.Lighten3).Padding(5).Text("Male").Bold().AlignCenter();
					//header.Cell().Border(1).Background(Colors.Grey.Lighten3).Padding(5).Text("Female").Bold().AlignCenter();
					header.Cell().Border(1).Background(Colors.Grey.Lighten3).Padding(5).Text("").Bold();

				});



				//int totalColumnParameter = GetFirstHeader().Count();
				//// Sample Data Rows (Replace with actual data dynamically)
				//foreach (var (item, index) in GetReligionWiseSummary().Select((s, i) => (s, i)))
				//{
				//	List<string> columnValue = [];
				//	var total = item.GenderCounts.Values.Sum();
				//	for (int i = 0; i < totalColumnParameter; i++)
				//	{
				//		var religionVal = GetFirstHeader()[i].ToLower();
				//		columnValue.Add(item.GenderCounts[$"{religionVal}_male"].ToString());
				//		columnValue.Add(item.GenderCounts[$"{religionVal}_female"].ToString());

				//	}
				//	// add last column for total
				//	columnValue.Add(total.ToString());

				//	AddTableRow(table, item.StandardDivisionName, columnValue);


				//}
				//AddTableRow(table, "1-A", "21", "16", "0", "0", "0", "0", "21", "16", "37");
				//AddTableRow(table, "2-A", "21", "16", "0", "0", "0", "0", "21", "16", "37");
				//AddTableRow(table, "3-A", "21", "16", "0", "0", "0", "0", "21", "16", "37");

				//It keep pending  bcz of need to help of hiren bhai
				//AddTotalRow(table, "Total:", "21", "16", "0", "0", "0", "0", "21", "16", "37");
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

		private void AddTableRow(TableDescriptor table, string col0, List<string> args)
		{
			table.Cell().Border(1).Padding(5).Text(col0).AlignCenter();
			foreach (var arg in args)
			{
				Console.WriteLine(arg);
				table.Cell().Border(1).Padding(5).Text(arg).AlignCenter();

			}
			//table.Cell().Border(1).Padding(5).Text(col1).AlignCenter();
			//table.Cell().Border(1).Padding(5).Text(col2).AlignCenter();
			//table.Cell().Border(1).Padding(5).Text(col3).AlignCenter();
			//table.Cell().Border(1).Padding(5).Text(col4).AlignCenter();
			//table.Cell().Border(1).Padding(5).Text(col5).AlignCenter();
			//table.Cell().Border(1).Padding(5).Text(col6).AlignCenter();
			//table.Cell().Border(1).Padding(5).Text(col7).AlignCenter();
			//table.Cell().Border(1).Padding(5).Text(col8).AlignCenter();
			//table.Cell().Border(1).Padding(5).Text(col9).AlignCenter();
		}

		private void AddTotalRow(TableDescriptor table, string col0, string col1, string col2, string col3, string col4, string col5, string col6, string col7, string col8, string col9)
		{
			table.Cell().Border(1).Background(Colors.Grey.Lighten3).Padding(5).Text(col0).AlignCenter().Bold();
			table.Cell().Border(1).Background(Colors.Grey.Lighten3).Padding(5).Text(col1).AlignCenter().Bold();
			table.Cell().Border(1).Background(Colors.Grey.Lighten3).Padding(5).Text(col2).AlignCenter().Bold();
			table.Cell().Border(1).Background(Colors.Grey.Lighten3).Padding(5).Text(col3).AlignCenter().Bold();
			table.Cell().Border(1).Background(Colors.Grey.Lighten3).Padding(5).Text(col4).AlignCenter().Bold();
			table.Cell().Border(1).Background(Colors.Grey.Lighten3).Padding(5).Text(col5).AlignCenter().Bold();
			table.Cell().Border(1).Background(Colors.Grey.Lighten3).Padding(5).Text(col6).AlignCenter().Bold();
			table.Cell().Border(1).Background(Colors.Grey.Lighten3).Padding(5).Text(col7).AlignCenter().Bold();
			table.Cell().Border(1).Background(Colors.Grey.Lighten3).Padding(5).Text(col8).AlignCenter().Bold();
			table.Cell().Border(1).Background(Colors.Grey.Lighten3).Padding(5).Text(col9).AlignCenter().Bold();
		}

		private void AddDetailRow(TableDescriptor table, string label, string value)
		{
			table.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text(label).Bold();
			table.Cell().Padding(5).Text(value);
		}
	}
}
public class StandardDivisionDynamicCount
{
	public string StandardDivisionName { get; set; }
	public Dictionary<string, int> GenderCounts { get; set; } = new();
}