using System;
using System.Collections.Generic;
using System.Linq;
using ShikshaMitra.Common;
using ShikshaMitra.HR.Employees.Onboarding;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.HR.Employees.Onboarding;

public class EmployeeApplication : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public string ApplicationNumber { get; private set; } = string.Empty;
	public EmployeeApplicationStatus Status { get; private set; }

	public string? FirstName { get; private set; }
	public string? MiddleName { get; private set; }
	public string? LastName { get; private set; }
	public string? Email { get; private set; }
	public string? MobileNo { get; private set; }
	public string? SecondaryMobileNo { get; private set; }
	public string? PositionAppliedFor { get; private set; }
	public string? DepartmentAppliedFor { get; private set; }

	public DateOnly? DateOfBirth { get; private set; }
	public int? Gender { get; private set; }
	public string? Religion { get; private set; }
	public int? CasteType { get; private set; }
	public string? FamilyCaste { get; private set; }
	public int? MaritalStatus { get; private set; }
	public int? BloodGroup { get; private set; }
	public string? QualificationSummary { get; private set; }
	public string? SkillsSummary { get; private set; }

	public string? Address1 { get; private set; }
	public string? Address2 { get; private set; }
	public string? Address3 { get; private set; }
	public Country? Country { get; private set; }
	public State? State { get; private set; }
	public string? City { get; private set; }
	public string? PinCode { get; private set; }

	public List<WorkExperience> WorkExperiences { get; private set; } = [];

	public string TokenHash { get; private set; } = string.Empty;
	public string? EncryptedToken { get; private set; }
	public DateTime TokenExpiresAt { get; private set; }
	public DateTime LinkLastIssuedAt { get; private set; }
	public DateTime? SubmittedAt { get; private set; }
	public DateTime? FinalizedAt { get; private set; }
	public Guid? FinalizedEmployeeId { get; private set; }

	public string? ResumeBlobName { get; private set; }
	public string? ResumeFileName { get; private set; }
	public string? ResumeContentType { get; private set; }
	public long? ResumeSize { get; private set; }
	public string? PhotoBlobName { get; private set; }
	public string? PhotoFileName { get; private set; }
	public string? PhotoContentType { get; private set; }
	public long? PhotoSize { get; private set; }

	public string? StatusReason { get; private set; }
	public string? InternalNotes { get; private set; }

	private EmployeeApplication()
	{
	}

	public EmployeeApplication(
		Guid id,
		Guid? tenantId,
		string applicationNumber,
		string tokenHash,
		string? encryptedToken,
		DateTime tokenExpiresAt,
		DateTime issuedAt,
		string? firstName,
		string? middleName,
		string? lastName,
		string? email,
		string? mobileNo,
		string? positionAppliedFor,
		string? departmentAppliedFor) : base(id)
	{
		TenantId = tenantId;
		ApplicationNumber = Check.NotNullOrWhiteSpace(applicationNumber, nameof(applicationNumber));
		Status = EmployeeApplicationStatus.InvitationSent;
		SetCandidateIdentity(firstName, middleName, lastName, email, mobileNo, null, positionAppliedFor, departmentAppliedFor);
		SetToken(tokenHash, encryptedToken, tokenExpiresAt, issuedAt);
	}

	public string FullName => string.Join(" ", new[] { FirstName, MiddleName, LastName }.Where(x => !string.IsNullOrWhiteSpace(x)));
	public bool HasResume => !string.IsNullOrWhiteSpace(ResumeBlobName);
	public bool HasPhoto => !string.IsNullOrWhiteSpace(PhotoBlobName);

	public void SetToken(string tokenHash, string? encryptedToken, DateTime tokenExpiresAt, DateTime issuedAt)
	{
		EnsureNotFinalized();
		TokenHash = Check.NotNullOrWhiteSpace(tokenHash, nameof(tokenHash));
		EncryptedToken = string.IsNullOrWhiteSpace(encryptedToken) ? null : encryptedToken;
		TokenExpiresAt = tokenExpiresAt;
		LinkLastIssuedAt = issuedAt;
		SubmittedAt = null;
		if (Status != EmployeeApplicationStatus.Rejected && Status != EmployeeApplicationStatus.Withdrawn)
		{
			Status = EmployeeApplicationStatus.InvitationSent;
		}
	}

	public void Submit(
		DateTime submittedAt,
		string firstName,
		string? middleName,
		string lastName,
		string email,
		string mobileNo,
		string? secondaryMobileNo,
		string? positionAppliedFor,
		string? departmentAppliedFor,
		DateOnly? dateOfBirth,
		int? gender,
		string? religion,
		int? casteType,
		string? familyCaste,
		int? maritalStatus,
		int? bloodGroup,
		string? qualificationSummary,
		string? skillsSummary,
		string? address1,
		string? address2,
		string? address3,
		Country? country,
		State? state,
		string? city,
		string? pinCode,
		List<WorkExperience> workExperiences,
		EmployeeApplicationUploadMetadata? resume,
		EmployeeApplicationUploadMetadata? photo)
	{
		EnsureCanSubmit(submittedAt);

		SetCandidateIdentity(
			firstName,
			middleName,
			lastName,
			email,
			mobileNo,
			secondaryMobileNo,
			positionAppliedFor,
			departmentAppliedFor);

		DateOfBirth = dateOfBirth;
		Gender = gender;
		Religion = religion;
		CasteType = casteType;
		FamilyCaste = familyCaste;
		MaritalStatus = maritalStatus;
		BloodGroup = bloodGroup;
		QualificationSummary = qualificationSummary;
		SkillsSummary = skillsSummary;
		Address1 = address1;
		Address2 = address2;
		Address3 = address3;
		Country = Clone(country);
		State = Clone(state);
		City = city;
		PinCode = pinCode;
		WorkExperiences = workExperiences ?? [];

		if (resume != null)
		{
			ResumeBlobName = resume.BlobName;
			ResumeFileName = resume.FileName;
			ResumeContentType = resume.ContentType;
			ResumeSize = resume.Size;
		}
		else
		{
			ResumeBlobName = null;
			ResumeFileName = null;
			ResumeContentType = null;
			ResumeSize = null;
		}

		if (photo != null)
		{
			PhotoBlobName = photo.BlobName;
			PhotoFileName = photo.FileName;
			PhotoContentType = photo.ContentType;
			PhotoSize = photo.Size;
		}
		else
		{
			PhotoBlobName = null;
			PhotoFileName = null;
			PhotoContentType = null;
			PhotoSize = null;
		}

		SubmittedAt = submittedAt;
		Status = EmployeeApplicationStatus.Applied;
		StatusReason = null;
	}

	public void ChangeStatus(EmployeeApplicationStatus status, string? reason)
	{
		EnsureNotFinalized();
		if (status == EmployeeApplicationStatus.Finalized)
		{
			throw new BusinessException(HRErrorCodes.EmployeeApplicationInvalidStatusTransition);
		}

		if (!AllowedNextStatuses(Status).Contains(status))
		{
			throw new BusinessException(HRErrorCodes.EmployeeApplicationInvalidStatusTransition);
		}

		Status = status;
		StatusReason = reason;
	}

	public void UpdateInternalNotes(string? notes)
	{
		InternalNotes = notes;
	}

	public void MarkFinalized(Guid employeeId, DateTime finalizedAt)
	{
		EnsureNotFinalized();
		if (Status is EmployeeApplicationStatus.Rejected or EmployeeApplicationStatus.Withdrawn)
		{
			throw new BusinessException(HRErrorCodes.EmployeeApplicationInvalidStatusTransition);
		}

		FinalizedEmployeeId = employeeId;
		FinalizedAt = finalizedAt;
		Status = EmployeeApplicationStatus.Finalized;
	}

	public void EnsureCanSubmit(DateTime now)
	{
		EnsureNotFinalized();
		if (Status is EmployeeApplicationStatus.Rejected or EmployeeApplicationStatus.Withdrawn)
		{
			throw new BusinessException(HRErrorCodes.EmployeeApplicationInvalidStatusTransition);
		}

		if (SubmittedAt.HasValue)
		{
			throw new BusinessException(HRErrorCodes.EmployeeApplicationAlreadySubmitted);
		}

		if (TokenExpiresAt <= now)
		{
			throw new BusinessException(HRErrorCodes.EmployeeApplicationTokenExpired);
		}
	}

	private void EnsureNotFinalized()
	{
		if (Status == EmployeeApplicationStatus.Finalized || FinalizedEmployeeId.HasValue)
		{
			throw new BusinessException(HRErrorCodes.EmployeeApplicationAlreadyFinalized);
		}
	}

	private void SetCandidateIdentity(
		string? firstName,
		string? middleName,
		string? lastName,
		string? email,
		string? mobileNo,
		string? secondaryMobileNo,
		string? positionAppliedFor,
		string? departmentAppliedFor)
	{
		FirstName = firstName?.Trim();
		MiddleName = middleName?.Trim();
		LastName = lastName?.Trim();
		Email = email?.Trim();
		MobileNo = mobileNo?.Trim();
		SecondaryMobileNo = secondaryMobileNo?.Trim();
		PositionAppliedFor = positionAppliedFor?.Trim();
		DepartmentAppliedFor = departmentAppliedFor?.Trim();
	}

	private static EmployeeApplicationStatus[] AllowedNextStatuses(EmployeeApplicationStatus current)
	{
		return current switch
		{
			EmployeeApplicationStatus.InvitationSent => [
				EmployeeApplicationStatus.Screening,
				EmployeeApplicationStatus.Interviewing,
				EmployeeApplicationStatus.Offered,
				EmployeeApplicationStatus.ReadyForOnboarding,
				EmployeeApplicationStatus.Rejected,
				EmployeeApplicationStatus.Withdrawn
			],
			EmployeeApplicationStatus.Applied => [
				EmployeeApplicationStatus.Screening,
				EmployeeApplicationStatus.Interviewing,
				EmployeeApplicationStatus.Offered,
				EmployeeApplicationStatus.ReadyForOnboarding,
				EmployeeApplicationStatus.Rejected,
				EmployeeApplicationStatus.Withdrawn
			],
			EmployeeApplicationStatus.Screening => [
				EmployeeApplicationStatus.Applied,
				EmployeeApplicationStatus.Interviewing,
				EmployeeApplicationStatus.Offered,
				EmployeeApplicationStatus.ReadyForOnboarding,
				EmployeeApplicationStatus.Rejected,
				EmployeeApplicationStatus.Withdrawn
			],
			EmployeeApplicationStatus.Interviewing => [
				EmployeeApplicationStatus.Screening,
				EmployeeApplicationStatus.Offered,
				EmployeeApplicationStatus.ReadyForOnboarding,
				EmployeeApplicationStatus.Rejected,
				EmployeeApplicationStatus.Withdrawn
			],
			EmployeeApplicationStatus.Offered => [
				EmployeeApplicationStatus.Interviewing,
				EmployeeApplicationStatus.ReadyForOnboarding,
				EmployeeApplicationStatus.Rejected,
				EmployeeApplicationStatus.Withdrawn
			],
			EmployeeApplicationStatus.ReadyForOnboarding => [
				EmployeeApplicationStatus.Offered,
				EmployeeApplicationStatus.Rejected,
				EmployeeApplicationStatus.Withdrawn
			],
			EmployeeApplicationStatus.Rejected or EmployeeApplicationStatus.Withdrawn => [
				EmployeeApplicationStatus.Screening,
				EmployeeApplicationStatus.Interviewing,
				EmployeeApplicationStatus.Offered,
				EmployeeApplicationStatus.ReadyForOnboarding
			],
			_ => []
		};
	}

	private static State? Clone(State? state) =>
		state == null ? null : new State(state.Name, state.IsoCode);

	private static Country? Clone(Country? country) =>
		country == null ? null : new Country(country.Name, country.IsoCode);
}

public class EmployeeApplicationUploadMetadata
{
	public string BlobName { get; set; } = string.Empty;
	public string FileName { get; set; } = string.Empty;
	public string ContentType { get; set; } = string.Empty;
	public long Size { get; set; }
}
