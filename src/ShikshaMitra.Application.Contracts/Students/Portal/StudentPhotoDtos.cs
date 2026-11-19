using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using ShikshaMitra.Students.Records;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.Portal;

public class StudentPhotoInfoDto
{
	public bool HasPhoto { get; set; }
	public string? Url { get; set; }
	public string? Version { get; set; }
	public int MaxBytes { get; set; } = 3 * 1024 * 1024;
	public string[] AllowedContentTypes { get; set; } = ["image/jpeg", "image/png"];
	public bool CanView { get; set; }
	public bool CanUpdate { get; set; }
	public bool RequiresApproval { get; set; }
	public string? ExpectedSnapshot { get; set; }
	public StudentPhotoChangeDto? PendingChange { get; set; }
}

public class StudentPhotoChangeDto
{
	public Guid Id { get; set; }
	public Guid StudentId { get; set; }
	public string Status { get; set; } = string.Empty;
	public string? ProposedPhotoUrl { get; set; }
	public DateTime CreatedAt { get; set; }
	public string? RejectionReason { get; set; }
	public string? ExceptionCode { get; set; }
}

public class SubmitStudentPhotoChangeDto
{
	public Guid PhotoDraftId { get; set; }
	[Required, StringLength(64, MinimumLength = 64)] public string ExpectedSnapshot { get; set; } = string.Empty;
	[Required, StringLength(80)] public string ClientKey { get; set; } = string.Empty;
}

public interface IMobileStudentPhotoAppService : IApplicationService
{
	Task<StudentPhotoInfoDto> GetInfoAsync(Guid studentId);
	Task<StudentPhotoDownloadDto?> GetPhotoAsync(Guid studentId);
	Task<StudentRecordPhotoDraftDto> UploadAsync(Guid studentId, UploadStudentPhotoDto input);
	Task<StudentPhotoDownloadDto> GetDraftAsync(Guid studentId, Guid draftId);
	Task<StudentPhotoChangeDto> SubmitAsync(Guid studentId, SubmitStudentPhotoChangeDto input);
	Task<StudentPhotoChangeDto> GetChangeAsync(Guid studentId, Guid changeId);
}
