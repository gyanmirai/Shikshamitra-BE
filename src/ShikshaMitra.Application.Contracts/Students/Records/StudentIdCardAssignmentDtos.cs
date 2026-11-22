using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using ShikshaMitra.Common.IdentityCards;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Auditing;

namespace ShikshaMitra.Students.Records;

public class StudentIdCardAssignmentListQueryDto : StudentIdCardAssignmentPageQueryDto
{
	public Guid AcademicYearId { get; set; }
	public Guid BoardId { get; set; }
	public Guid MediumId { get; set; }
	public Guid? StandardId { get; set; }
}

public class StudentIdCardAssignmentPageQueryDto : PagedResultRequestDto
{
	[StringLength(150)] public string? Search { get; set; }
}

public class StudentIdCardAssignmentRosterQueryDto : StudentIdCardAssignmentPageQueryDto
{
	public Guid? DivisionId { get; set; }
}

public class StudentIdCardAssignmentDto
{
	public Guid StandardId { get; set; }
	public string StandardName { get; set; } = "";
	public string? StageName { get; set; }
	public bool CanManage { get; set; }
	public Guid? TemplateId { get; set; }
	public string? TemplateName { get; set; }
	public string? ConcurrencyStamp { get; set; }
	public string? TemplateConcurrencyStamp { get; set; }
}

public class SaveStudentIdCardAssignmentDto
{
	public Guid TemplateId { get; set; }
	[StringLength(64)] public string? ConcurrencyStamp { get; set; }
}

public class StudentIdCardAssignmentRosterStudentDto
{
	public Guid StudentId { get; set; }
	public string StudentName { get; set; } = "";
	public string? GrNumber { get; set; }
	public string? DivisionName { get; set; }
	public bool HasValidIssuedCard { get; set; }
}

public class StudentIdCardAssignmentPreviewDto
{
	public List<Guid> StudentIds { get; set; } = [];
	[Required, StringLength(64)] public string ConcurrencyStamp { get; set; } = "";
	[Required, StringLength(64)] public string TemplateConcurrencyStamp { get; set; } = "";
}

public class StudentIdCardAssignmentExportDto : StudentIdCardAssignmentPreviewDto
{
	[DisableAuditing] public List<IdentityCardPhotoDto>? PhotoEdits { get; set; }
	[Range(1, 10)] public int? CardsPerPage { get; set; }
}

public class StudentIdCardAssignmentIssueDto : StudentIdCardAssignmentPreviewDto
{
	[DisableAuditing] public List<IdentityCardPhotoDto>? PhotoEdits { get; set; }
	[Required, StringLength(60)] public string IdempotencyKey { get; set; } = "";
}

public interface IStudentIdCardAssignmentAppService : IApplicationService
{
	Task<PagedResultDto<StudentIdCardAssignmentDto>> GetAssignmentsAsync(StudentIdCardAssignmentListQueryDto input);
	Task<StudentIdCardAssignmentDto> SaveAssignmentAsync(Guid academicYearId, Guid standardId, SaveStudentIdCardAssignmentDto input);
	Task DeleteAssignmentAsync(Guid academicYearId, Guid standardId, string? concurrencyStamp);
	Task<PagedResultDto<StudentIdCardAssignmentRosterStudentDto>> GetAssignmentRosterAsync(Guid academicYearId, Guid standardId, StudentIdCardAssignmentRosterQueryDto input);
	Task<List<StudentIdCardDto>> PreviewAssignmentAsync(Guid academicYearId, Guid standardId, StudentIdCardAssignmentPreviewDto input);
	Task<StudentCertificatePdfDto> ExportAssignmentAsync(Guid academicYearId, Guid standardId, StudentIdCardAssignmentExportDto input);
	Task<List<StudentIdCardDto>> IssueAssignmentAsync(Guid academicYearId, Guid standardId, StudentIdCardAssignmentIssueDto input);
}
