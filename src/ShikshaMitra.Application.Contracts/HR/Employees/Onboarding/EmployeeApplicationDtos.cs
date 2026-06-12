using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using ShikshaMitra.HR.Employees.Onboarding;
using ShikshaMitra.HR.Employees.PersonalInfo;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.HR.Employees.Onboarding;

public class GetEmployeeApplicationListInput : PagedAndSortedResultRequestDto
{
	public EmployeeApplicationStatus? Status { get; set; }
	public string? Search { get; set; }
}

public class CreateEmployeeApplicationInviteDto
{
	public string? FirstName { get; set; }
	public string? MiddleName { get; set; }
	public string? LastName { get; set; }

	[EmailAddress]
	public string? Email { get; set; }

	public string? MobileNo { get; set; }
	public string? PositionAppliedFor { get; set; }
	public string? DepartmentAppliedFor { get; set; }
}

public class EmployeeApplicationStatusUpdateDto
{
	public EmployeeApplicationStatus Status { get; set; }
	public string? Reason { get; set; }
}

public class EmployeeApplicationInviteDto
{
	public EmployeeApplicationDto Application { get; set; } = new();
	public string Token { get; set; } = string.Empty;
	public string PublicPath { get; set; } = string.Empty;
	public DateTime TokenExpiresAt { get; set; }
}

public class EmployeeApplicationListItemDto : EntityDto<Guid>
{
	public string ApplicationNumber { get; set; } = string.Empty;
	public EmployeeApplicationStatus Status { get; set; }
	public string FullName { get; set; } = string.Empty;
	public string? Email { get; set; }
	public string? MobileNo { get; set; }
	public string? PositionAppliedFor { get; set; }
	public string? DepartmentAppliedFor { get; set; }
	public DateTime TokenExpiresAt { get; set; }
	public DateTime LinkLastIssuedAt { get; set; }
	public DateTime? SubmittedAt { get; set; }
	public DateTime? FinalizedAt { get; set; }
	public Guid? FinalizedEmployeeId { get; set; }
	public bool HasResume { get; set; }
	public bool HasPhoto { get; set; }
	public DateTime CreationTime { get; set; }
}

public class EmployeeApplicationDto : EmployeeApplicationListItemDto
{
	public string? FirstName { get; set; }
	public string? MiddleName { get; set; }
	public string? LastName { get; set; }
	public string? SecondaryMobileNo { get; set; }
	public DateTime? DateOfBirth { get; set; }
	public int? Gender { get; set; }
	public string? Religion { get; set; }
	public int? CasteType { get; set; }
	public string? FamilyCaste { get; set; }
	public int? MaritalStatus { get; set; }
	public int? BloodGroup { get; set; }
	public string? QualificationSummary { get; set; }
	public string? SkillsSummary { get; set; }
	public string? Address1 { get; set; }
	public string? Address2 { get; set; }
	public string? Address3 { get; set; }
	public EmployeeApplicationLocationInfo? Country { get; set; }
	public EmployeeApplicationLocationInfo? State { get; set; }
	public string? City { get; set; }
	public string? PinCode { get; set; }
	public List<EmployeeApplicationWorkExperienceDto> WorkExperiences { get; set; } = [];
	public string? ResumeFileName { get; set; }
	public string? ResumeContentType { get; set; }
	public long? ResumeSize { get; set; }
	public string? PhotoFileName { get; set; }
	public string? PhotoContentType { get; set; }
	public long? PhotoSize { get; set; }
	public string? StatusReason { get; set; }
	public string? InternalNotes { get; set; }
}

public class PublicEmployeeApplicationDto
{
	public Guid Id { get; set; }
	public string ApplicationNumber { get; set; } = string.Empty;
	public EmployeeApplicationStatus Status { get; set; }
	public string FullName { get; set; } = string.Empty;
	public string? FirstName { get; set; }
	public string? MiddleName { get; set; }
	public string? LastName { get; set; }
	public string? Email { get; set; }
	public string? MobileNo { get; set; }
	public string? PositionAppliedFor { get; set; }
	public string? DepartmentAppliedFor { get; set; }
	public DateTime TokenExpiresAt { get; set; }
	public bool CanSubmit { get; set; }
	public bool IsExpired { get; set; }
	public bool IsSubmitted { get; set; }
}

public class PublicEmployeeApplicationSubmitDto
{
	[Required]
	public string FirstName { get; set; } = string.Empty;

	public string? MiddleName { get; set; }

	[Required]
	public string LastName { get; set; } = string.Empty;

	[Required]
	[EmailAddress]
	public string Email { get; set; } = string.Empty;

	[Required]
	public string MobileNo { get; set; } = string.Empty;

	public string? SecondaryMobileNo { get; set; }
	public string? PositionAppliedFor { get; set; }
	public string? DepartmentAppliedFor { get; set; }
	public DateTime? DateOfBirth { get; set; }
	public int? Gender { get; set; }
	public string? Religion { get; set; }
	public int? CasteType { get; set; }
	public string? FamilyCaste { get; set; }
	public int? MaritalStatus { get; set; }
	public int? BloodGroup { get; set; }
	public string? QualificationSummary { get; set; }
	public string? SkillsSummary { get; set; }
	public string? Address1 { get; set; }
	public string? Address2 { get; set; }
	public string? Address3 { get; set; }
	public EmployeeApplicationLocationInfo? Country { get; set; }
	public EmployeeApplicationLocationInfo? State { get; set; }
	public string? City { get; set; }
	public string? PinCode { get; set; }
	public List<EmployeeApplicationWorkExperienceDto> WorkExperiences { get; set; } = [];
}

public class FinalizeEmployeeApplicationDto
{
	[Required]
	public CreateEmployeePersonalInfoDto PersonalInfo { get; set; } = new();

	public List<EmployeeApplicationWorkExperienceDto> WorkExperiences { get; set; } = [];
	public bool UseSubmittedPhoto { get; set; } = true;
	public bool AttachResumeAsDocument { get; set; } = true;
}

public class EmployeeApplicationFinalizeResultDto
{
	public EmployeeApplicationDto Application { get; set; } = new();
	public EmployeePersonalInfoDto Employee { get; set; } = new();
}

public class EmployeeApplicationFileInput
{
	public string FileName { get; set; } = string.Empty;
	public string ContentType { get; set; } = string.Empty;
	public byte[] Content { get; set; } = [];
	public long Size { get; set; }
}

public class EmployeeApplicationFileDto
{
	public string FileName { get; set; } = string.Empty;
	public string ContentType { get; set; } = string.Empty;
	public byte[] Content { get; set; } = [];
}

public class EmployeeApplicationLocationInfo
{
	public string Name { get; set; } = string.Empty;
	public string IsoCode { get; set; } = string.Empty;
}

public class EmployeeApplicationWorkExperienceDto
{
	public string? MediumStageName { get; set; }
	public string? Designation { get; set; }
	public string? Department { get; set; }
	public EmployeeApplicationLocationInfo? Country { get; set; }
	public EmployeeApplicationLocationInfo? State { get; set; }
	public string? City { get; set; }
	public DateTime? StartDate { get; set; }
	public DateTime? EndDate { get; set; }
}
