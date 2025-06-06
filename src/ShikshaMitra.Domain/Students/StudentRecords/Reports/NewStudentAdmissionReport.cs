using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System;
using System.Collections.Generic;

namespace ShikshaMitra.Students.StudentRecords.Reports
{
	public class NewStudentAdmissionReport : IDocument
	{
		private readonly Student _student;
		public NewStudentAdmissionReport(Student student)
		{
			_student = student;
		}
		public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

		public void Compose(IDocumentContainer container)
		{
			container.Page(page =>
			{
				page.Size(PageSizes.A4);
				page.Margin(20);
				page.DefaultTextStyle(x => x.FontSize(10));

				page.Content().Column(col =>
				{
					// School Header
					col.Item().Row(row =>
					{
						row.RelativeItem().Column(col1 =>
						{
							col.Item().Text("Header Part");
						});

						//row.ConstantItem(80).Image("school_logo.png"); // Replace with actual logo path
					});

					col.Item().PaddingVertical(5);

					// Admission Date & Title
					col.Item().Row(row =>
					{
						row.RelativeItem().Text("New Student Admission Report").Bold().FontSize(14);
						row.RelativeItem().AlignRight().Text($"Admission Date: 05/06/2023").Bold();
					});

					col.Item().PaddingVertical(5);

					// School Information
					col.Item().Table(table =>
					{
						table.ColumnsDefinition(columns =>
						{
							columns.RelativeColumn();
							columns.RelativeColumn();
						});

						table.Cell().Row(1).ColumnSpan(2).BorderBottom(1).PaddingBottom(5).Text("School Information").Bold();
						table.Cell().Row(2).Column(1).Text($"School: DDVS - EM - {_student.EducationMediumStage.Name}");
						table.Cell().Row(2).Column(2).AlignRight().Text($"Batch:{_student.AcademicYear.Name}");
					});

					col.Item().PaddingVertical(5);

					// Student Information
					col.Item().Table(table =>
					{
						table.ColumnsDefinition(columns =>
						{
							columns.RelativeColumn();
							columns.RelativeColumn();
							columns.RelativeColumn();
						});

						table.Cell().Row(1).ColumnSpan(3).BorderBottom(1).PaddingBottom(5).Text("Student Information").Bold();
						table.Cell().Row(2).Column(1).Text($"Name: {_student.LastName} {_student.FirstName} {_student.MiddleName}");
						table.Cell().Row(2).Column(2).Text($"Class-Div: {_student.Standard.Name} / {_student.Division.Name}");
						table.Cell().Row(2).Column(3).Text($"Gr No.: {_student.GrNoRecord}");
                        table.Cell().Row(3).Column(1).Text($"Birth Date: {_student.DateOfBirth?.ToString("dd/MM/yyyy")}");
						table.Cell().Row(3).Column(2).Text($"Gender:{_student.GenderType}");

						// Student Image
						//table.Cell().RowSpan(2).Column(3).AlignRight().Image("student_photo.png", ImageScaling.FitArea);
					});

					col.Item().PaddingVertical(5);

					// Communication Information
					col.Item().Table(table =>
					{
						table.ColumnsDefinition(columns =>
						{
							columns.RelativeColumn();
							columns.RelativeColumn();
						});

						table.Cell().Row(1).ColumnSpan(2).BorderBottom(1).PaddingBottom(5).Text("Communication Information").Bold();
						table.Cell().Row(2).Column(1).Text($"Address: {_student.AddressLine1}");
						table.Cell().Row(2).Column(2).Text($"Parent Contact No: {_student.ParentContactNumber}");
						table.Cell().Row(3).Column(1).Text($"Email: {_student.Email}");
						table.Cell().Row(3).Column(2).Text($"Student Contact No: {_student.ParentContactNumber}");
					});

					col.Item().PaddingVertical(5);

					// Other Information
					col.Item().Table(table =>
					{
						table.ColumnsDefinition(columns =>
						{
							columns.RelativeColumn();
							columns.RelativeColumn();
							columns.RelativeColumn();
						});

						table.Cell().Row(1).ColumnSpan(3).BorderBottom(1).PaddingBottom(5).Text("Other Information").Bold();
						table.Cell().Row(2).Column(1).Text($"Religion: {_student.Religion} ");
						table.Cell().Row(2).Column(2).Text($"Family Caste: {_student.CastType}");
						table.Cell().Row(2).Column(3).Text($"Caste Category: {_student.FamilyCast}");
						table.Cell().Row(3).Column(1).Text($"Nationality: {_student.Nationality} ");
						table.Cell().Row(3).Column(2).Text($"Mother Tongue: {_student.MotherTongue} ");
						table.Cell().Row(3).Column(3).Text("UID No.: 242202041022320176");
						//table.Cell().Row(4).Column(1).Text($"Blood Group: {}");
						table.Cell().Row(4).Column(2).Text($"Aadhar Number: {_student.AadhaarNo}");
					});

					col.Item().PaddingVertical(5);

					// Father Information
					col.Item().Table(table =>
					{
						table.ColumnsDefinition(columns =>
						{
							columns.RelativeColumn();
							columns.RelativeColumn();
						});

						table.Cell().Row(1).ColumnSpan(2).BorderBottom(1).PaddingBottom(5).Text("Father Information").Bold();
						table.Cell().Row(2).Column(1).Text($"Father's Name: {_student.FatherDetails.Name} ");
						table.Cell().Row(2).Column(2).Text($"Qualification: {_student.FatherDetails.Qualification}");
						table.Cell().Row(3).Column(1).Text($"Occupation: {_student.FatherDetails.Occupation}");
						table.Cell().Row(3).Column(2).Text($"Contact No: {_student.FatherDetails.ContactNo} ");
						table.Cell().Row(4).Column(1).Text($"Office Address1: {_student.FatherDetails.OfficeAddress1} ");
						table.Cell().Row(4).Column(1).Text($"Office Address2: {_student.FatherDetails.OfficeAddress2} ");
						table.Cell().Row(4).Column(1).Text($"Office Address3: {_student.FatherDetails.OfficeAddress2} ");
						table.Cell().Row(4).Column(2).Text($"Email: {_student.FatherDetails.Email} ");
						//table.Cell().Row(5).Column(1).Text("Are you an ex-student of this school?: NO");
						table.Cell().Row(5).Column(2).Text($"Year Of Passing: {_student.FatherDetails.PassOutYear} ");
						//table.Cell().Row(6).Column(1).Text("Father's Income: 0");
					});

					col.Item().PaddingVertical(5);

					// Mother Information
					col.Item().Table(table =>
					{
						table.ColumnsDefinition(columns =>
						{
							columns.RelativeColumn();
							columns.RelativeColumn();
						});

						table.Cell().Row(1).ColumnSpan(2).BorderBottom(1).PaddingBottom(5).Text("Mother Information").Bold();
						table.Cell().Row(2).Column(1).Text($"Mother's Name: {_student.MotherDetails.Name} ");
						table.Cell().Row(2).Column(2).Text($"Qualification: {_student.MotherDetails.Qualification}");
						table.Cell().Row(3).Column(1).Text($"Occupation: {_student.MotherDetails.Occupation}");
						table.Cell().Row(3).Column(2).Text($"Contact No: {_student.MotherDetails.ContactNo} ");
						table.Cell().Row(4).Column(1).Text($"Office Address1: {_student.MotherDetails.OfficeAddress1} ");
						table.Cell().Row(4).Column(1).Text($"Office Address2: {_student.MotherDetails.OfficeAddress2} ");
						table.Cell().Row(4).Column(1).Text($"Office Address3: {_student.MotherDetails.OfficeAddress3} ");
						table.Cell().Row(4).Column(2).Text($"Email: {_student.MotherDetails.Email} ");
						//table.Cell().Row(5).Column(1).Text("Are you an ex-student of this school?: NO");
						table.Cell().Row(5).Column(2).Text($"Year Of Passing: {_student.MotherDetails.PassOutYear} ");
						//table.Cell().Row(6).Column(1).Text("Father's Income: 0");
						//table.Cell().Row(7).Column(1).Text("Physical Category: ");
					});
				});
			});
		}

		//public static void GeneratePdf(string filePath)
		//{
		//	var document = new NewStudentAdmissionReport();
		//	document.GeneratePdf(filePath);
		//}
	}
}
