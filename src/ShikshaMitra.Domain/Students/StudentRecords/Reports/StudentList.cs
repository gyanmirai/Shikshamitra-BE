using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace ShikshaMitra.Students.StudentRecords.Reports
{
	public class StudentList : IDocument
	{
		private readonly IReadOnlyList<Student> _student;

		public StudentList(IReadOnlyList<Student> student)
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
				// Report Title
				column.Item().PaddingTop(10).BorderBottom(1).BorderColor(Colors.Black);
				column.Item().PaddingTop(10).AlignCenter().Text("Student List")
					.FontSize(16)
					.Bold();
				column.Item().PaddingTop(10).BorderBottom(1).BorderColor(Colors.Black);

				// Student Details Section
				column.Item().Border(1).PaddingTop(3).Table(table =>
				{
					table.ColumnsDefinition(columns =>
					{
						columns.RelativeColumn();
						columns.RelativeColumn();
					});

					foreach (var item in _student)
					{
					AddDetailRow(table, "Student Name", $"{item.LastName} {item.FirstName} ${item.MiddleName}");
					AddDetailRow(table, "GR No.", $"{item.GrNoRecord}");
					AddDetailRow(table, "Father's Name", item.MiddleName);
					AddDetailRow(table, "Mother's Name",item.MotherDetails.Name);
						AddDetailRow(table, "Father's Mobile No.", item.FatherDetails.ContactNo);

					AddDetailRow(table, "Mother's Mobile No.", item.MotherDetails.ContactNo);
					AddDetailRow(table, "Religion",item.Religion);
                        AddDetailRow(table, "Category", item?.StudentAdditionalDetails?.RightToEducation == true ? "RTE" : "General");
					AddDetailRow(table, "Address", item.AddressLine1);
						AddDetailRow(table, "Sex", item.GenderType.ToString());

					AddDetailRow(table, "Date of Birth", item.DateOfBirth?.ToString("dd-MM-yyyy") ?? string.Empty);
					//AddDetailRow(table, "Annual Income (Father)", "-");
					//AddDetailRow(table, "Annual Income (Mother)", "0");
					AddDetailRow(table, "Only Child of the Parents?", item.StudentAdditionalDetails.SingleParentChild==true ?"YES" : "NO" );
					AddDetailRow(table, "Aadhaar No.", item.AadhaarNo);
						//AddDetailRow(table, "UID No.", "242202041032320088");
						//AddDetailRow(table, "Blood Group", )
					AddDetailRow(table, "Bank Account No.", item.ParentBankDetails.AccountNumber);

					AddDetailRow(table, "Bank Name", item.ParentBankDetails.BankName);
					AddDetailRow(table, "Bank Branch", item.ParentBankDetails.BranchName);
					AddDetailRow(table, "Bank IFSC Code", item.ParentBankDetails.IfscCode);

					AddDetailRow(table, "Parent Email", item.FatherDetails.Email);

					}

				});

				// Notes Section
				//column.Item().PaddingTop(20).Text("Note:____________ ");
				//column.Item().AlignRight().BorderTop(1).PaddingTop(10).Text("Parent's Signature: ");
				column.Item().PaddingTop(25).Row(row =>
				{
					row.AutoItem().PaddingRight(75).Text("Note : _____________").FontSize(14);
					row.RelativeItem().AlignRight().PaddingTop(7).BorderTop(1).Text("Parent's Signature").FontSize(14);

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
