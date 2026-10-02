using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.Records;

public class StudentRecordDuplicateInput
{
	public Guid StudentId { get; set; }
	public Guid OtherStudentId { get; set; }
	[Required, StringLength(2000, MinimumLength = 5)] public string Reason { get; set; } = string.Empty;
}
public class StudentRecordDuplicateQuery : PagedResultRequestDto
{
	public Guid? StudentId { get; set; }
	public string? Status { get; set; }
}
public class StudentRecordDuplicateDecision
{
	[Required] public string ConcurrencyStamp { get; set; } = string.Empty;
	[Required, StringLength(2000, MinimumLength = 5)] public string Reason { get; set; } = string.Empty;
	public bool ConfirmedDuplicate { get; set; }
}
public class StudentRecordDuplicateDto
{
	public Guid Id { get; set; }
	public Guid StudentId { get; set; }
	public Guid OtherStudentId { get; set; }
	public string StudentName { get; set; } = string.Empty;
	public string OtherStudentName { get; set; } = string.Empty;
	public string Reason { get; set; } = string.Empty;
	public string? ReviewReason { get; set; }
	public string Status { get; set; } = string.Empty;
	public string ConcurrencyStamp { get; set; } = string.Empty;
	public Guid RequestedBy { get; set; }
	public Guid? ReviewedBy { get; set; }
	public DateTime CreationTime { get; set; }
	public bool CanReview { get; set; }
}
public interface IStudentRecordDuplicateAppService : IApplicationService
{
	Task<PagedResultDto<StudentRecordDuplicateDto>> GetListAsync(StudentRecordDuplicateQuery input);
	Task<StudentRecordDuplicateDto> CreateAsync(StudentRecordDuplicateInput input);
	Task<StudentRecordDuplicateDto> ResolveAsync(Guid id, StudentRecordDuplicateDecision input);
}
