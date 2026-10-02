using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.Records;

public class StudentIdCardTemplateDto
{
	[StringLength(150)] public string SchoolName { get; set; } = "";
	[StringLength(300)] public string HeaderText { get; set; } = "Student Identity Card";
	[StringLength(500)] public string FooterText { get; set; } = "Please return this card to the school if found.";
	[StringLength(7)] public string PrimaryColor { get; set; } = "#00897B";
	public bool ShowPhoto { get; set; } = true;
	public List<string> Fields { get; set; } = ["StudentName", "GRNumber", "Class", "Division", "AcademicYear"];
	public string? ConcurrencyStamp { get; set; }
}

public class StudentIdCardPreviewRequestDto
{
	public List<Guid> StudentIds { get; set; } = [];
}
public class IssueStudentIdCardsDto : StudentIdCardPreviewRequestDto
{
	[Required, StringLength(60)] public string IdempotencyKey { get; set; } = "";
}
public class StudentIdCardDto
{
	public Guid? Id { get; set; }
	public Guid StudentId { get; set; }
	public string CardNumber { get; set; } = "Preview";
	public string Status { get; set; } = "Preview";
	public DateTime? IssuedAt { get; set; }
	public Guid? IssuedByUserId { get; set; }
	public bool HasPhoto { get; set; }
	public Dictionary<string, string?> StudentSnapshot { get; set; } = [];
	public StudentIdCardTemplateDto TemplateSnapshot { get; set; } = new();
}

public interface IStudentIdCardAppService : IApplicationService
{
	Task<StudentIdCardTemplateDto> GetTemplateAsync();
	Task<StudentIdCardTemplateDto> SaveTemplateAsync(StudentIdCardTemplateDto input);
	Task<List<StudentIdCardDto>> PreviewAsync(StudentIdCardPreviewRequestDto input);
	Task<List<StudentIdCardDto>> IssueAsync(IssueStudentIdCardsDto input);
	Task<List<StudentIdCardDto>> GetHistoryAsync(Guid studentId);
	Task<StudentCertificatePdfDto> GetPdfAsync(Guid id);
	Task<StudentCertificatePdfDto> ReprintAsync(Guid id, StudentDocumentActionDto input);
	Task<StudentIdCardDto> VoidAsync(Guid id, StudentDocumentActionDto input);
}
