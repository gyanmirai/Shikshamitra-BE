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

	public EmployeeApplicationPersonalInfoSnapshot? PersonalInfo { get; private set; }
	public EmployeeApplicationFamilyInfoSnapshot? FamilyInfo { get; private set; }
	public EmployeeApplicationAdditionalInfoSnapshot? AdditionalInfo { get; private set; }
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

	private void SetToken(string tokenHash, string? encryptedToken, DateTime tokenExpiresAt, DateTime issuedAt)
	{
		TokenHash = Check.NotNullOrWhiteSpace(tokenHash, nameof(tokenHash));
		EncryptedToken = string.IsNullOrWhiteSpace(encryptedToken) ? null : encryptedToken;
		TokenExpiresAt = tokenExpiresAt;
		LinkLastIssuedAt = issuedAt;
	}

	public void RegenerateInvitationToken(
		string tokenHash,
		string? encryptedToken,
		DateTime tokenExpiresAt,
		DateTime issuedAt)
	{
		if (Status != EmployeeApplicationStatus.InvitationSent || SubmittedAt.HasValue)
		{
			throw new BusinessException(HRErrorCodes.EmployeeApplicationInvalidStatusTransition);
		}

		SetToken(tokenHash, encryptedToken, tokenExpiresAt, issuedAt);
	}

	public void Submit(
		DateTime submittedAt,
		EmployeeApplicationPersonalInfoSnapshot personalInfo,
		EmployeeApplicationFamilyInfoSnapshot? familyInfo,
		EmployeeApplicationAdditionalInfoSnapshot? additionalInfo,
		List<WorkExperience> workExperiences,
		EmployeeApplicationUploadMetadata? resume,
		EmployeeApplicationUploadMetadata? photo)
	{
		EnsureCanSubmit(submittedAt);

		PersonalInfo = Clone(personalInfo);
		FamilyInfo = Clone(familyInfo);
		AdditionalInfo = Clone(additionalInfo);

		SetCandidateIdentity(
			personalInfo.FirstName,
			personalInfo.MiddleName,
			personalInfo.LastName,
			personalInfo.Email,
			personalInfo.MobileNo,
			personalInfo.SecondaryMobileNo,
			personalInfo.Designation,
			personalInfo.Department);

		DateOfBirth = personalInfo.DateOfBirth;
		Gender = personalInfo.Gender;
		Religion = personalInfo.Religion;
		CasteType = personalInfo.CasteType;
		FamilyCaste = personalInfo.FamilyCaste;
		MaritalStatus = personalInfo.MaritalStatus;
		BloodGroup = personalInfo.BloodGroup;
		QualificationSummary = personalInfo.Qualification;
		SkillsSummary = null;
		Address1 = personalInfo.Address1;
		Address2 = personalInfo.Address2;
		Address3 = personalInfo.Address3;
		Country = MapToCountry(personalInfo.Country);
		State = MapToState(personalInfo.State);
		City = personalInfo.City;
		PinCode = personalInfo.Pincode;
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

		if (!SubmittedAt.HasValue &&
		    status is not EmployeeApplicationStatus.Rejected and not EmployeeApplicationStatus.Withdrawn)
		{
			throw SubmissionRequired();
		}

		if (!EmployeeApplicationStatusTransitions.AllowedNextStatuses(Status).Contains(status))
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
		if (!SubmittedAt.HasValue)
		{
			throw SubmissionRequired();
		}

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

	private static BusinessException SubmissionRequired() =>
		new(
			HRErrorCodes.EmployeeApplicationSubmissionRequired,
			"The candidate must submit the onboarding application before HR can advance its status.");

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

	private static State? Clone(State? state) =>
		state == null ? null : new State(state.Name, state.IsoCode);

	private static Country? Clone(Country? country) =>
		country == null ? null : new Country(country.Name, country.IsoCode);

	private static EmployeeApplicationPersonalInfoSnapshot Clone(EmployeeApplicationPersonalInfoSnapshot value) =>
		new(
			value.FirstName,
			value.MiddleName,
			value.ShortName,
			value.LastName,
			value.Email,
			value.MobileNo,
			value.SecondaryMobileNo,
			value.DateOfBirth,
			value.Gender,
			value.Religion,
			value.CasteType,
			value.FamilyCaste,
			value.MaritalStatus,
			value.BloodGroup,
			value.IsTeaching,
			value.Department,
			value.Designation,
			value.Qualification,
			value.IdentificationMark1,
			value.Address1,
			value.Address2,
			value.Address3,
			Clone(value.Country),
			Clone(value.State),
			value.City,
			value.Pincode);

	private static EmployeeApplicationFamilyInfoSnapshot? Clone(EmployeeApplicationFamilyInfoSnapshot? value) =>
		value == null
			? null
			: new EmployeeApplicationFamilyInfoSnapshot(
				Clone(value.FatherDetails),
				Clone(value.MotherDetails),
				Clone(value.SpouseDetails));

	private static EmployeeApplicationFamilyMemberSnapshot? Clone(EmployeeApplicationFamilyMemberSnapshot? value) =>
		value == null
			? null
			: new EmployeeApplicationFamilyMemberSnapshot(
				value.Name,
				value.Occupation,
				value.MobileNo,
				value.Address1,
				value.Address2,
				value.Address3,
				Clone(value.Country),
				Clone(value.State),
				value.City,
				value.Pincode);

	private static EmployeeApplicationAdditionalInfoSnapshot? Clone(EmployeeApplicationAdditionalInfoSnapshot? value) =>
		value == null
			? null
			: new EmployeeApplicationAdditionalInfoSnapshot(
				value.PersonWithDisability,
				value.SecondLanguage,
				value.IsLibraryMember,
				value.IsTransportMember);

	private static EmployeeApplicationLocationSnapshot? Clone(EmployeeApplicationLocationSnapshot? value) =>
		value == null ? null : new EmployeeApplicationLocationSnapshot(value.Name, value.IsoCode);

	private static Country? MapToCountry(EmployeeApplicationLocationSnapshot? value) =>
		value == null || string.IsNullOrWhiteSpace(value.Name)
			? null
			: new Country(value.Name, value.IsoCode);

	private static State? MapToState(EmployeeApplicationLocationSnapshot? value) =>
		value == null || string.IsNullOrWhiteSpace(value.Name)
			? null
			: new State(value.Name, value.IsoCode);
}

public class EmployeeApplicationPersonalInfoSnapshot
{
	public string FirstName { get; private set; } = string.Empty;
	public string? MiddleName { get; private set; }
	public string? ShortName { get; private set; }
	public string LastName { get; private set; } = string.Empty;
	public string Email { get; private set; } = string.Empty;
	public string MobileNo { get; private set; } = string.Empty;
	public string? SecondaryMobileNo { get; private set; }
	public DateOnly? DateOfBirth { get; private set; }
	public int? Gender { get; private set; }
	public string? Religion { get; private set; }
	public int? CasteType { get; private set; }
	public string? FamilyCaste { get; private set; }
	public int? MaritalStatus { get; private set; }
	public int? BloodGroup { get; private set; }
	public bool IsTeaching { get; private set; } = true;
	public string? Department { get; private set; }
	public string? Designation { get; private set; }
	public string? Qualification { get; private set; }
	public string? IdentificationMark1 { get; private set; }
	public string? Address1 { get; private set; }
	public string? Address2 { get; private set; }
	public string? Address3 { get; private set; }
	public EmployeeApplicationLocationSnapshot? Country { get; private set; }
	public EmployeeApplicationLocationSnapshot? State { get; private set; }
	public string? City { get; private set; }
	public string? Pincode { get; private set; }

	private EmployeeApplicationPersonalInfoSnapshot()
	{
	}

	public EmployeeApplicationPersonalInfoSnapshot(
		string firstName,
		string? middleName,
		string? shortName,
		string lastName,
		string email,
		string mobileNo,
		string? secondaryMobileNo,
		DateOnly? dateOfBirth,
		int? gender,
		string? religion,
		int? casteType,
		string? familyCaste,
		int? maritalStatus,
		int? bloodGroup,
		bool isTeaching,
		string? department,
		string? designation,
		string? qualification,
		string? identificationMark1,
		string? address1,
		string? address2,
		string? address3,
		EmployeeApplicationLocationSnapshot? country,
		EmployeeApplicationLocationSnapshot? state,
		string? city,
		string? pincode)
	{
		FirstName = firstName.Trim();
		MiddleName = middleName?.Trim();
		ShortName = shortName?.Trim();
		LastName = lastName.Trim();
		Email = email.Trim();
		MobileNo = mobileNo.Trim();
		SecondaryMobileNo = secondaryMobileNo?.Trim();
		DateOfBirth = dateOfBirth;
		Gender = gender;
		Religion = religion?.Trim();
		CasteType = casteType;
		FamilyCaste = familyCaste?.Trim();
		MaritalStatus = maritalStatus;
		BloodGroup = bloodGroup;
		IsTeaching = isTeaching;
		Department = department?.Trim();
		Designation = designation?.Trim();
		Qualification = qualification?.Trim();
		IdentificationMark1 = identificationMark1?.Trim();
		Address1 = address1?.Trim();
		Address2 = address2?.Trim();
		Address3 = address3?.Trim();
		Country = country;
		State = state;
		City = city?.Trim();
		Pincode = pincode?.Trim();
	}
}

public class EmployeeApplicationFamilyInfoSnapshot
{
	public EmployeeApplicationFamilyMemberSnapshot? FatherDetails { get; private set; }
	public EmployeeApplicationFamilyMemberSnapshot? MotherDetails { get; private set; }
	public EmployeeApplicationFamilyMemberSnapshot? SpouseDetails { get; private set; }

	private EmployeeApplicationFamilyInfoSnapshot()
	{
	}

	public EmployeeApplicationFamilyInfoSnapshot(
		EmployeeApplicationFamilyMemberSnapshot? fatherDetails,
		EmployeeApplicationFamilyMemberSnapshot? motherDetails,
		EmployeeApplicationFamilyMemberSnapshot? spouseDetails)
	{
		FatherDetails = fatherDetails;
		MotherDetails = motherDetails;
		SpouseDetails = spouseDetails;
	}
}

public class EmployeeApplicationFamilyMemberSnapshot
{
	public string? Name { get; private set; }
	public string? Occupation { get; private set; }
	public string? MobileNo { get; private set; }
	public string? Address1 { get; private set; }
	public string? Address2 { get; private set; }
	public string? Address3 { get; private set; }
	public EmployeeApplicationLocationSnapshot? Country { get; private set; }
	public EmployeeApplicationLocationSnapshot? State { get; private set; }
	public string? City { get; private set; }
	public string? Pincode { get; private set; }

	private EmployeeApplicationFamilyMemberSnapshot()
	{
	}

	public EmployeeApplicationFamilyMemberSnapshot(
		string? name,
		string? occupation,
		string? mobileNo,
		string? address1,
		string? address2,
		string? address3,
		EmployeeApplicationLocationSnapshot? country,
		EmployeeApplicationLocationSnapshot? state,
		string? city,
		string? pincode)
	{
		Name = name?.Trim();
		Occupation = occupation?.Trim();
		MobileNo = mobileNo?.Trim();
		Address1 = address1?.Trim();
		Address2 = address2?.Trim();
		Address3 = address3?.Trim();
		Country = country;
		State = state;
		City = city?.Trim();
		Pincode = pincode?.Trim();
	}
}

public class EmployeeApplicationAdditionalInfoSnapshot
{
	public bool PersonWithDisability { get; private set; }
	public string? SecondLanguage { get; private set; }
	public bool IsLibraryMember { get; private set; }
	public bool IsTransportMember { get; private set; }

	private EmployeeApplicationAdditionalInfoSnapshot()
	{
	}

	public EmployeeApplicationAdditionalInfoSnapshot(
		bool personWithDisability,
		string? secondLanguage,
		bool isLibraryMember,
		bool isTransportMember)
	{
		PersonWithDisability = personWithDisability;
		SecondLanguage = secondLanguage?.Trim();
		IsLibraryMember = isLibraryMember;
		IsTransportMember = isTransportMember;
	}
}

public class EmployeeApplicationLocationSnapshot
{
	public string Name { get; private set; } = string.Empty;
	public string IsoCode { get; private set; } = string.Empty;

	private EmployeeApplicationLocationSnapshot()
	{
	}

	public EmployeeApplicationLocationSnapshot(string name, string isoCode)
	{
		Name = name.Trim();
		IsoCode = isoCode.Trim();
	}
}

public class EmployeeApplicationUploadMetadata
{
	public string BlobName { get; set; } = string.Empty;
	public string FileName { get; set; } = string.Empty;
	public string ContentType { get; set; } = string.Empty;
	public long Size { get; set; }
}
