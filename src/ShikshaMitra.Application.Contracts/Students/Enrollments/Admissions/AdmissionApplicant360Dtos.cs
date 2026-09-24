using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentValidation;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionNoteDto { public Guid Id { get; set; } public Guid ApplicantId { get; set; } public string Text { get; set; } = string.Empty; public DateTime CreationTime { get; set; } public DateTime? LastEditedAt { get; set; } public Guid? CreatorId { get; set; } }
public class CreateAdmissionNoteDto { public string Text { get; set; } = string.Empty; }
public class UpdateAdmissionNoteDto { public string Text { get; set; } = string.Empty; }
public class UpdateAdmissionApplicantProfileDto { public string FirstName { get; set; } = string.Empty; public string? MiddleName { get; set; } public string? LastName { get; set; } public DateTime DateOfBirth { get; set; } public string Gender { get; set; } = string.Empty; public string? GuardianMobile { get; set; } public string? GuardianEmail { get; set; } }
public class UpdateAdmissionApplicantProfileDtoValidator : AbstractValidator<UpdateAdmissionApplicantProfileDto>
{
	public UpdateAdmissionApplicantProfileDtoValidator()
	{
		RuleFor(input => input.FirstName).NotEmpty().MaximumLength(100);
		RuleFor(input => input.MiddleName).MaximumLength(100);
		RuleFor(input => input.LastName).MaximumLength(100);
		RuleFor(input => input.DateOfBirth).NotEmpty().LessThanOrEqualTo(DateTime.UtcNow.Date);
		RuleFor(input => input.Gender).Must(value => value is "Male" or "Female")
			.WithMessage("Gender must be Male or Female.");
		RuleFor(input => input.GuardianMobile).Matches("^[6-9][0-9]{9}$")
			.When(input => input.GuardianMobile != null)
			.WithMessage("Guardian mobile must be a valid 10-digit Indian mobile number.");
		RuleFor(input => input.GuardianEmail).EmailAddress()
			.When(input => !string.IsNullOrWhiteSpace(input.GuardianEmail));
	}
}
public class AdmissionApplicantApplicationDto : AdmissionApplicationDto { public List<AdmissionApplicantStageProgressDto> Stages { get; set; } = []; }
public class AdmissionApplicantStageProgressDto { public string Code { get; set; } = string.Empty; public string Name { get; set; } = string.Empty; public AdmissionWorkflowStageType StageType { get; set; } public AdmissionRuntimeStageStatus Status { get; set; } public int DisplayOrder { get; set; } public DateTime? EnteredAt { get; set; } public DateTime? CompletedAt { get; set; } }
public class AdmissionApplicant360Dto { public AdmissionApplicantDto Applicant { get; set; } = new(); public AdmissionFamilyDto? Family { get; set; } public AdmissionGuardianDto? PrimaryGuardian { get; set; } public List<AdmissionApplicantApplicationDto> Applications { get; set; } = []; public List<AdmissionActivityDto> Activities { get; set; } = []; public List<AdmissionNoteDto> Notes { get; set; } = []; }
public interface IAdmissionApplicantAppService : IApplicationService { Task<AdmissionApplicant360Dto> GetAsync(Guid id); Task<AdmissionApplicantDto> UpdateProfileAsync(Guid id, UpdateAdmissionApplicantProfileDto input); Task<List<AdmissionActivityDto>> GetActivitiesAsync(Guid id); Task<List<AdmissionNoteDto>> GetNotesAsync(Guid id); Task<AdmissionNoteDto> CreateNoteAsync(Guid id, CreateAdmissionNoteDto input); Task<AdmissionNoteDto> UpdateNoteAsync(Guid id, Guid noteId, UpdateAdmissionNoteDto input); }
