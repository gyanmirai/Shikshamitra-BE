using System;
using System.Text.Json;
using ShikshaMitra.Students.StudentRecords;

namespace ShikshaMitra.Students.Records;

public static class StudentCertificateDocumentFactory
{
	public static StudentCertificateDocumentDto Create(StudentCertificateResponseDto certificate)
	{
		var type = Enum.Parse<StudentCertificateType>(certificate.CertificateType);
		var document = new StudentCertificateDocumentDto { Title = StudentCertificateTemplate.GetDisplayName(type) };
		string? Value(string key) => certificate.CertificateData.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : null;
		void Add(string label, string? value) => document.Particulars.Add(new() { Label = label, Value = value ?? "Not recorded" });
		Add("Student name", certificate.Student.StudentName);
		Add("General register number", certificate.Student.GrNo);
		Add("Admission / registration number", certificate.Student.AdmissionNo);
		Add("Class", certificate.Student.ClassName);
		Add("Division", certificate.Student.DivisionName);
		Add("Academic year", certificate.Student.AcademicYearName);
		Add("Date of birth", Value("DOB"));
		var identity = $"{certificate.Student.StudentName}, whose particulars are recorded below";
		var school = certificate.Organization.OrganizationName;
		document.Body = type switch
		{
			StudentCertificateType.Bonafide => $"This is to certify that {identity}, is a bonafide student of {school} for the academic year stated below. This certificate is issued on the basis of the institution's student records.",
			StudentCertificateType.Character => $"This Character Certificate is issued to {identity}, on the basis of the student records maintained by {school}. Any conduct assessment supplied by the institution is recorded below.",
			StudentCertificateType.Leaving => $"This is to certify that {identity}, is recorded at {school}. This Leaving Certificate is issued in connection with the linked exit case, whose status and effective date are recorded below.",
			_ => $"This First Trial Certificate is issued to {identity}, on the basis of the student records maintained by {school}, for the purpose stated below. Any additional first trial particulars supplied by the institution are recorded below."
		};
		if (type == StudentCertificateType.Character && Value("Conduct") is { } conduct) Add("Conduct assessment", conduct);
		if (type == StudentCertificateType.FirstTrial)
		{
			if (Value("FirstTrialDetails") is { } details) Add("First trial particulars", details);
		}
		if (type == StudentCertificateType.Leaving && Value("_ExitTransition") is { } exitJson)
		{
			using var exit = JsonDocument.Parse(exitJson);
			foreach (var (key, label) in new[] { ("outcome", "Exit type"), ("status", "Exit case status"), ("effectiveDate", "Exit effective date"), ("reason", "Reason for leaving") })
			{
				if (!exit.RootElement.TryGetProperty(key, out var property)) continue;
				var text = property.GetString();
				if (key == "effectiveDate" && DateTime.TryParse(text, out var date)) text = date.ToString("yyyy-MM-dd");
				Add(label, text);
			}
		}
		Add("Purpose / reason", Value("Reason"));
		return document;
	}
}
