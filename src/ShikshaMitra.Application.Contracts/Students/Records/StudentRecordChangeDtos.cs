using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.Records;

public class CreateStudentRecordChangeDto
{
	public Guid StudentId { get; set; }
	[Required, MaxLength(40)] public string Section { get; set; } = string.Empty;
	[Required, MaxLength(80)] public string ClientKey { get; set; } = string.Empty;
	[Required, MaxLength(2000)] public string Reason { get; set; } = string.Empty;
	[Required] public string ExpectedSnapshot { get; set; } = string.Empty;
	public JsonElement Payload { get; set; }
	public DateTimeOffset? EffectiveAt { get; set; }
	public Guid? CorrectsChangeId { get; set; }
}
public class StudentRecordChangeQueryDto : PagedAndSortedResultRequestDto
{
	public Guid? StudentId { get; set; }
	public Guid? AcademicYearId { get; set; }
	public string? Status { get; set; }
}
public class StudentRecordDecisionDto
{
	[Required, MaxLength(2000)] public string Reason { get; set; } = string.Empty;
	[Required] public string ConcurrencyStamp { get; set; } = string.Empty;
}
public class StudentRecordChangeDto : EntityDto<Guid>
{
	public string? PhotoPreviewUrl { get; set; }
	public Guid StudentId { get; set; }
	public string StudentName { get; set; } = string.Empty;
	public Guid AcademicYearId { get; set; }
	public Guid RequestedBy { get; set; }
	public Guid? CorrectsChangeId { get; set; }
	public Guid? PolicyVersionId { get; set; }
	public string Section { get; set; } = string.Empty;
	public string Action { get; set; } = string.Empty;
	public string Reason { get; set; } = string.Empty;
	public string Status { get; set; } = string.Empty;
	public string ConcurrencyStamp { get; set; } = string.Empty;
	public DateTimeOffset EffectiveAt { get; set; }
	public DateTimeOffset? AppliedAt { get; set; }
	public DateTime CreationTime { get; set; }
	public int ApprovalCount { get; set; }
	public int RequiredApprovals { get; set; }
	public JsonElement? Before { get; set; }
	public JsonElement? Proposed { get; set; }
	public string? ExceptionCode { get; set; }
	public bool CanApprove { get; set; }
	public bool CanCancel { get; set; }
	public bool PayloadMasked { get; set; }
	public List<StudentRecordDecisionHistoryDto> Decisions { get; set; } = [];
}
public class StudentRecordDecisionHistoryDto
{
	public Guid ActorId { get; set; }
	public int Stage { get; set; }
	public string Decision { get; set; } = string.Empty;
	public string Reason { get; set; } = string.Empty;
	public DateTime CreationTime { get; set; }
}
public class StudentRecordSectionSnapshotDto
{
	public string Section { get; set; } = string.Empty;
	public string Snapshot { get; set; } = string.Empty;
	public JsonElement Data { get; set; }
}
public interface IStudentRecordChangeAppService : IApplicationService
{
	Task<PagedResultDto<StudentRecordChangeDto>> GetListAsync(StudentRecordChangeQueryDto input);
	Task<StudentRecordChangeDto> GetAsync(Guid id);
	Task<StudentRecordSectionSnapshotDto> GetSnapshotAsync(Guid studentId, string section);
	Task<StudentRecordChangeDto> CreateAsync(CreateStudentRecordChangeDto input);
	Task<StudentRecordChangeDto> ApproveAsync(Guid id, StudentRecordDecisionDto input);
	Task<StudentRecordChangeDto> RejectAsync(Guid id, StudentRecordDecisionDto input);
	Task<StudentRecordChangeDto> CancelAsync(Guid id, StudentRecordDecisionDto input);
}
