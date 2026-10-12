using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.Records;

public class StudentIdCardTemplateDto
{
	public Guid? Id { get; set; }
	[StringLength(150)] public string Name { get; set; } = "Student I-Card";
	[StringLength(40)] public string PresetKey { get; set; } = "formal-student";
	[StringLength(10)] public string Orientation { get; set; } = "Horizontal";
	[StringLength(150)] public string SchoolName { get; set; } = "";
	[StringLength(300)] public string SchoolAddress { get; set; } = "";
	[StringLength(300)] public string HeaderText { get; set; } = "Student Identity Card";
	[StringLength(500)] public string FooterText { get; set; } = "Please return this card to the school if found.";
	[StringLength(7)] public string PrimaryColor { get; set; } = "#00897B";
	[StringLength(7)] public string BackgroundColor { get; set; } = "#FFFFFF";
	public int CardsPerPage { get; set; } = 8;
	public bool ShowQrCode { get; set; }
	public bool ShowPhoto { get; set; } = true;
	public List<string> Fields { get; set; } = ["StudentName", "GRNumber", "Class", "Division", "AcademicYear"];
	public StudentIdCardSelectionDto Selection { get; set; } = new();
	public string? ConcurrencyStamp { get; set; }
}

public class StudentIdCardSelectionDto
{
	public Guid? AcademicYearId { get; set; }
	public List<Guid> BoardIds { get; set; } = [];
	public List<Guid> MediumIds { get; set; } = [];
	public List<Guid> StandardIds { get; set; } = [];
	public List<Guid> DivisionIds { get; set; } = [];
	public List<Guid> StudentIds { get; set; } = [];
}

public class StudentIdCardPreviewRequestDto
{
	public Guid? TemplateId { get; set; }
	public List<Guid> StudentIds { get; set; } = [];
}
public class IssueStudentIdCardsDto : StudentIdCardPreviewRequestDto
{
	[Required, StringLength(60)] public string IdempotencyKey { get; set; } = "";
}
public class StudentIdCardsPdfRequestDto : StudentIdCardPreviewRequestDto
{
	public int CardsPerPage { get; set; } = 8;
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
	Task<List<StudentIdCardTemplateDto>> GetTemplatesAsync();
	Task<StudentIdCardTemplateDto> GetTemplateByIdAsync(Guid id);
	Task<StudentIdCardTemplateDto> CreateTemplateAsync(StudentIdCardTemplateDto input);
	Task<StudentIdCardTemplateDto> UpdateTemplateAsync(Guid id, StudentIdCardTemplateDto input);
	Task DeleteTemplateAsync(Guid id);
	Task<List<StudentIdCardDto>> PreviewAsync(StudentIdCardPreviewRequestDto input);
	Task<List<StudentIdCardDto>> IssueAsync(IssueStudentIdCardsDto input);
	Task<List<StudentIdCardDto>> GetHistoryAsync(Guid studentId);
	Task<StudentCertificatePdfDto> ExportPdfAsync(StudentIdCardsPdfRequestDto input);
	Task<StudentCertificatePdfDto> GetPdfAsync(Guid id);
	Task<StudentCertificatePdfDto> ReprintAsync(Guid id, StudentDocumentActionDto input);
	Task<StudentIdCardDto> VoidAsync(Guid id, StudentDocumentActionDto input);
}
