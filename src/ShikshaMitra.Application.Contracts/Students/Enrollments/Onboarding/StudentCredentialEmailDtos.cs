using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.StudentEnrollments.Onboarding;

public class RequestStudentCredentialEmailDto
{
	[Required, RegularExpression("^(student|guardian)$")]
	public string AccountType { get; set; } = string.Empty;
	public string? ConcurrencyStamp { get; set; }
}

public class StudentCredentialEmailDto
{
	public Guid? Id { get; set; }
	public string AccountType { get; set; } = string.Empty;
	public string Email { get; set; } = string.Empty;
	public string Status { get; set; } = "NotRequested";
	public string? Message { get; set; }
	public DateTime? SentAt { get; set; }
	public int AttemptCount { get; set; }
	public bool EmailEnabled { get; set; }
	public bool CanSend { get; set; }
}

public interface IStudentCredentialEmailAppService : IApplicationService
{
	Task<IReadOnlyList<StudentCredentialEmailDto>> GetAsync(Guid caseId);
	Task<StudentCredentialEmailDto> RequestAsync(Guid caseId, RequestStudentCredentialEmailDto input);
}
