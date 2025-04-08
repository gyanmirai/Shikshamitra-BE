using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShikshaMitra.Students.StudentRecords.Reports
{
	public class StudentDetailInformation : IDocument


	{

		private readonly IReadOnlyList<Student> _student;

		public StudentDetailInformation(IReadOnlyList<Student> student)
		{
			_student = student;
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
		}  // ✅ Closing brace added for ComposeHeader

		void ComposeContent(IContainer container)
		{
			container.PaddingVertical(20).Column(column =>
			{
				column.Item().PaddingTop(10).BorderBottom(1).BorderColor(Colors.Black);

				column.Item().PaddingTop(10).AlignCenter().Text("Student Detail Information")
					.FontSize(16).Bold();

				column.Item().PaddingTop(10).BorderBottom(1).BorderColor(Colors.Black);
				// Student Details Section
				column.Item().PaddingTop(20).Text("Student Details").ExtraBold().FontSize(12);

				column.Item().Table(table =>
				{
					table.ColumnsDefinition(columns =>
					{
						columns.RelativeColumn();
						columns.RelativeColumn();
					});

					AddDetailRow(table, "Name", "CHAVDA VIHAA VIPULKUMAR");
					AddDetailRow(table, "Gr No.", "2174");
					AddDetailRow(table, "Class-Div", "JR.KG / A");
					AddDetailRow(table, "Blood Group", "-");
				});

				column.Item().PaddingTop(20).Text("Admission Details").ExtraBold().FontSize(12);
				column.Item().Table(table =>
				{
					table.ColumnsDefinition(columns =>
					{
						columns.RelativeColumn();
						columns.RelativeColumn();
					});

					AddDetailRow(table, "Admission Standard", "JR.KG");
					AddDetailRow(table, "Admission Date", "18/06/2024");
					AddDetailRow(table, "Previous School", "NA");
					AddDetailRow(table, "Previous Standard", "-");
				});

				column.Item().PaddingTop(20).Text("Family Details").ExtraBold().FontSize(12);
				column.Item().Table(table =>
				{
					table.ColumnsDefinition(columns =>
					{
						columns.RelativeColumn();
						columns.RelativeColumn();
					});

					AddDetailRow(table, "Father's Name", "VIPULKUMAR");
					AddDetailRow(table, "Qualification", "Graduate");
					AddDetailRow(table, "Contanct No", "+91 6355030866");
					AddDetailRow(table, "Mother's Name", "AARTIBEN");
					AddDetailRow(table, "Qualification", "Graduate");
					AddDetailRow(table, "Contanct No", "+91 6355030866");
				});

				column.Item().PaddingTop(20).Text("Demographic Details").ExtraBold().FontSize(12);
				column.Item().Table(table =>
				{
					table.ColumnsDefinition(columns =>
					{
						columns.RelativeColumn();
						columns.RelativeColumn();
					});

					AddDetailRow(table, "Birth Place", "SURAT");
					AddDetailRow(table, "Date Of Birth", "16/07/2020");
					AddDetailRow(table, "Gender", "Female");
					AddDetailRow(table, "Religion", "Hindu");
					AddDetailRow(table, "Family Cast", "Ahir");
					AddDetailRow(table, "Mother Tongue", "Gujarati");
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
