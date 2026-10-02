using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.Records;

public class StudentGuardianPortalLinkDto
{
	public Guid LinkId { get; set; }
	public Guid UserId { get; set; }
	public string Relationship { get; set; } = "";
	public string Email { get; set; } = "";
	public bool EmailConfirmed { get; set; }
	public bool IsActive { get; set; }
}
public class StudentGuardianAccessReviewDto
{
	public Guid Id { get; set; }
	public Guid StudentId { get; set; }
	public Guid ChangeRequestId { get; set; }
	public string Status { get; set; } = "Pending";
	public string OnApprovedChangeMode { get; set; } = "SuspendUntilReview";
	public string ConcurrencyStamp { get; set; } = "";
	public List<StudentGuardianPortalLinkDto> PreviousLinks { get; set; } = [];
	public DateTime CreationTime { get; set; }
	public Guid? ReviewedByUserId { get; set; }
	public DateTime? ReviewedAt { get; set; }
}
public class VerifiedStudentGuardianDto
{
	[Required] public string Relationship { get; set; } = "guardian";
	[Required, EmailAddress, StringLength(256)] public string Email { get; set; } = "";
	public Guid? ExistingUserId { get; set; }
	public bool ConfirmIdentityVerified { get; set; }
}
public class ResolveStudentGuardianAccessDto
{
	[Required, StringLength(40)] public string ConcurrencyStamp { get; set; } = "";
	[Required, StringLength(2000, MinimumLength = 5)] public string Reason { get; set; } = "";
	public List<Guid> RevokeLinkIds { get; set; } = [];
	public List<Guid> KeepLinkIds { get; set; } = [];
	public bool ConfirmRetainedGuardiansVerified { get; set; }
	public VerifiedStudentGuardianDto? NewGuardian { get; set; }
}
public interface IStudentGuardianAccessAppService : IApplicationService
{
	Task<List<StudentGuardianPortalLinkDto>> GetLinksAsync(Guid studentId);
	Task<List<StudentGuardianAccessReviewDto>> GetReviewsAsync(Guid studentId);
	Task<StudentGuardianPortalLinkDto?> GetVerifiedGuardianCandidateAsync(Guid studentId, string relationship);
	Task<StudentGuardianAccessReviewDto> ResolveAsync(Guid reviewId, ResolveStudentGuardianAccessDto input);
}
