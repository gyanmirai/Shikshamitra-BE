using System;
using ShikshaMitra.Students.Records;
using ShikshaMitra.Students.StudentRecords;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Students.Records;

public class StudentCertificatePdfRenderer_Tests
{
	[Theory]
	[InlineData(StudentCertificateType.Bonafide, "Bonafide Certificate")]
	[InlineData(StudentCertificateType.Character, "Character Certificate")]
	[InlineData(StudentCertificateType.Leaving, "Leaving Certificate")]
	[InlineData(StudentCertificateType.FirstTrial, "First Trial Certificate")]
	public void Renders_each_type_with_number_date_and_student_particulars(StudentCertificateType type, string title)
	{
		var certificate = Create(type);
		certificate.Template.Document = StudentCertificateDocumentFactory.Create(certificate);
		certificate.Template.Document.Title.ShouldBe(title);
		certificate.Template.Document.Particulars.ShouldContain(x => x.Label == "Purpose / reason" && x.Value == "Scholarship application");
		StudentCertificatePdfRenderer.Render(certificate).AsSpan(0, 4).ToArray().ShouldBe("%PDF"u8.ToArray());
	}

	[Fact]
	public void Approved_exit_is_described_as_an_exit_case_without_claiming_student_has_already_left()
	{
		var certificate = Create(StudentCertificateType.Leaving);
		certificate.CertificateData["_ExitTransition"] = "{\"outcome\":\"Transfer\",\"status\":\"Approved\",\"effectiveDate\":\"2026-11-01T00:00:00\",\"reason\":\"Family relocation\"}";
		var document = StudentCertificateDocumentFactory.Create(certificate);
		document.Particulars.ShouldContain(x => x.Label == "Exit case status" && x.Value == "Approved");
		document.Particulars.ShouldContain(x => x.Label == "Exit effective date" && x.Value == "2026-11-01");
		document.Particulars.ShouldContain(x => x.Label == "Reason for leaving" && x.Value == "Family relocation");
		document.Body.ShouldNotContain("has left");
	}

	[Fact]
	public void Uses_frozen_document_and_does_not_invent_conduct_or_examination_results()
	{
		var certificate = Create(StudentCertificateType.FirstTrial);
		certificate.Template.Document = StudentCertificateDocumentFactory.Create(certificate);
		certificate.Template.Document.Body.ShouldNotContain("passed");
		certificate.Template.Document.Particulars.ShouldNotContain(x => x.Label == "Result");
		certificate.CertificateData["FirstTrialDetails"] = "Verified trial particulars";
		StudentCertificateDocumentFactory.Create(certificate).Particulars.ShouldContain(x => x.Value == "Verified trial particulars");
		certificate.Template.Document.Particulars.ShouldNotContain(x => x.Value == "Verified trial particulars");
		StudentCertificatePdfRenderer.Render(certificate).Length.ShouldBeGreaterThan(100);
		var character = StudentCertificateDocumentFactory.Create(Create(StudentCertificateType.Character));
		character.Body.ShouldNotContain("good conduct");
	}

	[Fact]
	public void Long_purpose_and_custom_wording_render_without_overflow()
	{
		var certificate = Create(StudentCertificateType.Bonafide);
		certificate.CertificateData["Reason"] = new string('X', 4000);
		certificate.Template.HeaderContent = "Custom letterhead";
		certificate.Template.RenderedHeaderContent = new string('Y', 4000);
		StudentCertificatePdfRenderer.Render(certificate).Length.ShouldBeGreaterThan(100);
	}

	private static StudentCertificateResponseDto Create(StudentCertificateType type) => new()
	{
		CertificateNo = "CERT-2026-000001", CertificateType = type.ToString(), IssueDate = new DateTimeOffset(2026, 10, 13, 0, 0, 0, TimeSpan.Zero),
		Student = new() { StudentName = "Asha Patel", GrNo = "GR-1042", AdmissionNo = "ADM-402", ClassName = "Class 8", DivisionName = "A", AcademicYearName = "2026-27" },
		Organization = new() { OrganizationName = "Example School", Address = "Example campus" },
		CertificateData = new() { ["DOB"] = "2013-06-15", ["Reason"] = "Scholarship application" },
		Template = new() { HeaderContent = StudentCertificateTemplate.DefaultHeader(type), FooterContent = StudentCertificateTemplate.DefaultFooter() }
	};
}
