using ShikshaMitra.Students.Records.Settings;
using ShikshaMitra.Students.StudentEnrollments;
using ShikshaMitra.Students.StudentEnrollments.Registration;
using ShikshaMitra.Students.StudentEnrollments.Settings;
using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students;

public class StudentAdditionalDetail : FullAuditedEntity<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public DateTimeOffset? DateOfBirth { get; private set; }
	public GenderEnum? Gender { get; private set; }
	public string? ProfilePictureUrl { get; private set; }

	public DateTimeOffset? DateOfInquiry { get; private set; }
	public string InquiryNo { get; private set; }
	public Guid? InquiryPreferenceId { get; private set; }
	public string? RegistrationFormNo { get; private set; }
	public double? RegistrationAmount { get; private set; }

	// Entrance Exam Information
	public Guid? EntranceExamSlotId { get; private set; }
	public DateTimeOffset? EntranceExamStartDateTime { get; private set; }
	public DateTimeOffset? EntranceExamEndDateTime { get; private set; }
	public int? EntranceExamRoomNo { get; private set; }

	// Exam Results
	public double? EntranceExamTotalMarks { get; private set; }
	public double? EntranceExamPassingMarks { get; private set; }
	public double? EntranceExamObtainedMarks { get; private set; }
	public double? EntranceExamPercentage { get; private set; }
	public EntranceExamStatusEnum? EntranceExamStatus { get; private set; }
	public DateTimeOffset? EntranceExamResultDate { get; private set; }

	public string? AdharNo { get; private set; }
	public string? ApparId { get; private set; }
	public string? PanNo { get; private set; }
	public string? PennNo { get; private set; }
	public ReligionEnum? Religion { get; private set; }
	public string FamilyCast { get; private set; }
	public CastCategoryEnum? CastCategory { get; private set; }
	public string? Nationality { get; private set; }
	public string? BirthPlace { get; private set; }
	public MotherTongueEnum? MotherTongue { get; private set; }
	public Category Category { get; private set; } = Category.General;

	public bool? IsBPLStudent { get; private set; }
	public string? BPLCardNo { get; private set; }
	public bool? HaveDisability { get; private set; }
	public string? DisabilityDetails { get; private set; }
	public string? SecondLanguage { get; private set; }
	public string? EmergencyContactNo { get; private set; }
	public string? EmergencyRelationName { get; private set; }
	public string? EmergencyRelationType { get; private set; }
	public bool? IsSingleParentChild { get; private set; }
	public bool? IsSingleChild { get; private set; }
	public bool? IsLibraryMember { get; set; } = false;
	public bool? IsTransportMember { get; set; } = false;

	public string? PreviousSchool { get; private set; }
	public string? LastClassPassed { get; private set; }

	public Guid? HouseId { get; private set; }
	public HouseSettings? House { get; private set; }
	public bool? IsGlobalHouseLeader { get; private set; }
	public bool? IsDivisionHouseLeader { get; private set; }

	public InquiryPreference? InquiryPreference { get; private set; }

	public StudentRegistrationStateEnum StudentRegistrationState { get; private set; }

	private StudentAdditionalDetail()
	{
		/* This constructor is for deserialization / ORM purpose */
	}

	public StudentAdditionalDetail(
		Guid id,
		Guid? tenantId,
		DateTimeOffset? dateOfBirthday,
		GenderEnum? gender,
		DateTimeOffset? dateOfInquiry,
		string inquiryNo,
		Guid? inquiryPreferenceId,
		string? previousSchool
	)
		: base(id)
	{
		TenantId = tenantId;
		DateOfBirth = dateOfBirthday;
		Gender = gender;
		DateOfInquiry = dateOfInquiry;
		InquiryNo = inquiryNo;
		InquiryPreferenceId = inquiryPreferenceId;
		// Ensure non-null default to satisfy DB not-null constraint until General Info is provided
		FamilyCast = string.Empty;
		PreviousSchool = previousSchool;
		StudentRegistrationState = StudentRegistrationStateEnum.Default;
	}

	public void UpdateDetails(
	DateTimeOffset? dateOfBirth,
	GenderEnum? gender,
	DateTimeOffset? dateOfInquiry,
	Guid? inquiryPreferenceId,
	string? previousSchool)
	{
		DateOfBirth = dateOfBirth;
		Gender = gender;
		DateOfInquiry = dateOfInquiry;
		InquiryPreferenceId = inquiryPreferenceId;
		PreviousSchool = previousSchool;
	}

	public void UpdateGeneralInformation(
		DateTimeOffset dateOfBirth,
		GenderEnum gender,
		string? profilePictureUrl,
		string? adharNo,
		string? apparId,
		string? panNo,
		string? penNo,
		ReligionEnum? religion,
		string familyCast,
		CastCategoryEnum? castCategory,
		string? nationality,
		string? birthPlace,
		MotherTongueEnum? motherTongue,
		string? previousSchool,
		string? lastClassPassed,
        StudentRegistrationStateEnum state,
		Guid? inquiryPreferenceId,
		string registrationFormNo
		)
	{
		DateOfBirth = dateOfBirth;
		Gender = gender;
		ProfilePictureUrl = profilePictureUrl;
		AdharNo = adharNo;
		ApparId = apparId;
		PanNo = panNo;
		PennNo = penNo;
		Religion = religion;
		FamilyCast = familyCast;
		CastCategory = castCategory;
		Nationality = nationality;
		BirthPlace = birthPlace;
		MotherTongue = motherTongue;
		PreviousSchool = previousSchool;
		LastClassPassed = lastClassPassed;
        StudentRegistrationState = state;
		InquiryPreferenceId = inquiryPreferenceId;
		RegistrationFormNo = registrationFormNo;
    }

	public void IssueRegistrationForm(
		string formNumber,
		double amount)
	{
		RegistrationFormNo = formNumber;
		RegistrationAmount = amount;
	}

	/// <summary>
	/// Updates additional student details during registration process
	/// </summary>
	public void UpdateAdditionalDetails(
		bool isBPLStudent,
		string? bplCardNo,
		string? secondLanguage,
		bool haveDisability,
		string? disabilityDetails,
		string? emergencyRelativeName,
		string? emergencyRelation,
		string? emergencyContactNo,
		bool isSingleChild,
		bool isSingleParentChild,
		Category category,
		bool isLibraryMember,
		bool isTransportMember)
	{
		IsBPLStudent = isBPLStudent;
		BPLCardNo = bplCardNo;
		SecondLanguage = secondLanguage;
		HaveDisability = haveDisability;
		DisabilityDetails = haveDisability ? disabilityDetails : null;
		EmergencyRelationName = emergencyRelativeName;
		EmergencyRelationType = emergencyRelation;
		EmergencyContactNo = emergencyContactNo;
		IsSingleChild = isSingleChild;
		IsSingleParentChild = isSingleParentChild;
		Category = category;
		IsLibraryMember = isLibraryMember;
		IsTransportMember = isTransportMember;

		AdvanceRegistrationState(StudentRegistrationStateEnum.AdditionalDetails);
	}

	public void UpdateDisabilityInfo(bool haveDisability, string? disabilityDetails)
	{
		HaveDisability = haveDisability;
		DisabilityDetails = haveDisability ? disabilityDetails : null;
	}

	public void ApplyAdmissionIdentityAndFamilyConditions(string? panNumber, bool? isSingleChild,
		bool? isSingleParentChild, bool? isBplStudent, string? bplCardNumber)
	{
		PanNo = panNumber ?? PanNo;
		IsSingleChild = isSingleChild ?? IsSingleChild;
		IsSingleParentChild = isSingleParentChild ?? IsSingleParentChild;
		IsBPLStudent = isBplStudent ?? IsBPLStudent;
		BPLCardNo = bplCardNumber ?? BPLCardNo;
	}

	public void ApplyAdmissionSupportDetails(string? secondLanguage, bool? haveDisability, string? disabilityDetails,
		string? emergencyContactName, string? emergencyRelation, string? emergencyContactNo)
	{
		SecondLanguage = secondLanguage ?? SecondLanguage;
		HaveDisability = haveDisability ?? HaveDisability;
		DisabilityDetails = HaveDisability == false ? null : disabilityDetails ?? DisabilityDetails;
		EmergencyRelationName = emergencyContactName ?? EmergencyRelationName;
		EmergencyRelationType = emergencyRelation ?? EmergencyRelationType;
		EmergencyContactNo = emergencyContactNo ?? EmergencyContactNo;
	}

	public void UpdatePreviousSchoolSummary(string? previousSchool, string? lastClassPassed)
	{
		PreviousSchool = previousSchool;
		LastClassPassed = lastClassPassed;
	}

	public void AdvanceRegistrationState(StudentRegistrationStateEnum newState)
	{
		if (StudentRegistrationState < newState)
			StudentRegistrationState = newState;
	}

	/// <summary>
	/// Schedules entrance exam for the student
	/// </summary>
	public void ScheduleEntranceExam(
		Guid entranceExamSlotId,
		DateTimeOffset examStartTime,
		DateTimeOffset examEndTime,
		int examRoomNo)
	{
		EntranceExamSlotId = entranceExamSlotId;
		EntranceExamStartDateTime = examStartTime;
		EntranceExamEndDateTime = examEndTime;
		EntranceExamRoomNo = examRoomNo;
		EntranceExamStatus = EntranceExamStatusEnum.Pending;
	}

	/// <summary>
	/// Records entrance exam result
	/// </summary>
	public void RecordExamResult(
		double totalMarks,
		double passingMarks,
		double obtainedMarks)
	{
		EntranceExamTotalMarks = totalMarks;
		EntranceExamPassingMarks = passingMarks;
		EntranceExamObtainedMarks = obtainedMarks;
        // Safe percentage calculation since totalMarks > 0 is validated
        EntranceExamPercentage = (obtainedMarks / totalMarks) * 100;
		EntranceExamStatus = obtainedMarks >= passingMarks
			? EntranceExamStatusEnum.Pass
			: EntranceExamStatusEnum.Fail;
		EntranceExamResultDate = DateTimeOffset.UtcNow;
	}

	public void AssignHouse(Guid houseId)
	{
		HouseId = houseId;
		IsGlobalHouseLeader = false;
		IsDivisionHouseLeader = false;
	}

	public void DeassignHouse()
	{
		HouseId = null;
		IsGlobalHouseLeader = false;
		IsDivisionHouseLeader = false;
	}

	public void SetGlobalLeader(bool isLeader)
	{
		if (HouseId == null)
		{
			throw new InvalidOperationException("Cannot assign leader role. Student is not assigned to any house.");
		}
		IsGlobalHouseLeader = isLeader;
	}

	public void SetDivisionLeader(bool isLeader)
	{
		if (HouseId == null)
		{
			throw new InvalidOperationException("Cannot assign leader role. Student is not assigned to any house.");
		}
		IsDivisionHouseLeader = isLeader;
	}
}
