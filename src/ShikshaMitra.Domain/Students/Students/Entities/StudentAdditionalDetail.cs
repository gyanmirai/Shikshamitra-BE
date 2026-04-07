using System;
using ShikshaMitra.Students.Records.Settings;
using ShikshaMitra.Students.StudentEnrollments;
using ShikshaMitra.Students.StudentEnrollments.Registration;
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
	public DateTimeOffset? ExamDate { get; private set; }
	public TimeSpan? ExamStartTime { get; private set; }
	public TimeSpan? ExamEndTime { get; private set; }
	public int? ExamRoomNo { get; private set; }

	// Exam Results
	public decimal? ExamTotalMarks { get; private set; }
	public decimal? ExamPassingMarks { get; private set; }
	public decimal? ExamObtainedMarks { get; private set; }
	public decimal? ExamPercentage { get; private set; }
	public EntranceExamStatusEnum? ExamStatus { get; private set; }
	public DateTimeOffset? ExamResultDate { get; private set; }

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
		DateTimeOffset examDate,
		TimeSpan examStartTime,
		TimeSpan examEndTime,
		int examRoomNo)
	{
		EntranceExamSlotId = entranceExamSlotId;
		ExamDate = examDate;
		ExamStartTime = examStartTime;
		ExamEndTime = examEndTime;
		ExamRoomNo = examRoomNo;
		ExamStatus = EntranceExamStatusEnum.Pending;
	}

	/// <summary>
	/// Records entrance exam result
	/// </summary>
	public void RecordExamResult(
		decimal totalMarks,
		decimal passingMarks,
		decimal obtainedMarks)
	{
        // Validate inputs to guard against division by zero and negative values
        if (totalMarks <= 0)
        {
            throw new ArgumentException("Total marks must be greater than zero.", nameof(totalMarks));
        }

        if (passingMarks < 0)
        {
            throw new ArgumentException("Passing marks cannot be negative.", nameof(passingMarks));
        }

        if (obtainedMarks < 0)
        {
            throw new ArgumentException("Obtained marks cannot be negative.", nameof(obtainedMarks));
        }

		if (obtainedMarks > totalMarks)
		{
			throw new ArgumentException("Obtained marks cannot exceed total marks.");
		}

		if (passingMarks > totalMarks)
		{
			throw new ArgumentException("Passing marks cannot exceed total marks.");
		}

		ExamTotalMarks = totalMarks;
		ExamPassingMarks = passingMarks;
		ExamObtainedMarks = obtainedMarks;
        // Safe percentage calculation since totalMarks > 0 is validated
        ExamPercentage = (obtainedMarks / totalMarks) * 100;
		ExamStatus = obtainedMarks >= passingMarks
			? EntranceExamStatusEnum.Pass
			: EntranceExamStatusEnum.Fail;
		ExamResultDate = DateTimeOffset.UtcNow;
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
