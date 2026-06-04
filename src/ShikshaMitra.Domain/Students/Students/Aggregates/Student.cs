using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using ShikshaMitra.Common;
using ShikshaMitra.Students.StudentEnrollments;
using ShikshaMitra.Students.StudentEnrollments.Settings;
using ShikshaMitra.Students.ValueObjects;
using ShikshaMitra.Students.StudentRecords;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.Domain.Finance.Fees.Settings;

namespace ShikshaMitra.Students;

public class Student : FullAuditedAggregateRoot<Guid>, IMultiTenant, IUserScopedEntity
{
	public Guid? TenantId { get; private set; }
	public string? FirstName { get; private set; }
	public string? MiddleName { get; private set; }
	public string? LastName { get; private set; }
	public Guid? AcademicYearId { get; private set; }
	public Guid? AcademicBoardId { get; private set; }
	public Guid AcademicMediumId { get; private set; }
	public Guid? AcademicMediumStageId { get; private set; }
	public Guid? StandardId { get; private set; }
	public Guid? DivisionId { get; private set; }
	public int? GrNo { get; private set; }
	public int? RollNo { get; private set; }
	public StudentLifeCycleStatusEnum Status { get; private set; }
	public StudentEnrollmentApprovalState AdmissionApprovalStatus { get; private set; }
	public string? AdmissionApprovalRemarks { get; private set; }
	public Guid? FeeStructureId { get; set; }

	// Navigation properties to aggregate parts (1:1 relationships)
	public StudentAdditionalDetail? AdditionalDetail { get; private set; }
	public StudentContactDetail? ContactDetail { get; private set; }
	public StudentFamilyDetail? FamilyDetail { get; private set; }
	public StudentCommunicationDetail? CommunicationDetail { get; private set; }
	public StudentMedicalRecord? MedicalRecord { get; private set; }
	public StudentPreviousSchoolDetail? PreviousSchoolDetail { get; private set; }
	public StudentBankDetail? BankDetail { get; private set; }

	// Navigation for inquiry preference
	public AcademicYear? AcademicYear { get; private set; }
	public EducationMedium? AcademicMedium { get; private set; }
	public EducationBoard? AcademicBoard { get; private set; }
	public Standard? Standard { get; private set; }
	public Division? Division { get; private set; }

	public FeeStructure? FeeStructure { get; private set; }

	public ICollection<StudentNote> Notes { get; private set; } = new List<StudentNote>();

	/// <summary>
	/// Ordered log of every operational status transition for this student.
	/// Each entry is append-only and carries the creator / timestamp via ABP auditing.
	/// </summary>
	public ICollection<StudentStatusHistory> StatusHistories { get; private set; } = new List<StudentStatusHistory>();

	private Student()
	{
		/* This constructor is for deserialization / ORM purpose */
	}

	public Student(
		Guid id,
		Guid? tenantId,
		string? firstName,
		string? middleName,
		string? lastName,
		Guid educationYearId,
		Guid educationBoardId,
		Guid educationMediumId,
		Guid educationMediumStageId,
		Guid? standardId = null,
		Guid? divisionId = null,
		int? grNo = null,
		int? rollNo = null,
		StudentLifeCycleStatusEnum status = StudentLifeCycleStatusEnum.New
	)
		: base(id)
	{
		TenantId = tenantId;
		FirstName = firstName;
		MiddleName = middleName;
		LastName = lastName;
		AcademicYearId = educationYearId;
		AcademicBoardId = educationBoardId;
		AcademicMediumId = educationMediumId;
		AcademicMediumStageId = educationMediumStageId;
		StandardId = standardId;
		DivisionId = divisionId;

		GrNo = grNo;
		RollNo = rollNo;
		Status = status;
		Notes = new List<StudentNote>();
		StatusHistories = new List<StudentStatusHistory>();
	}

	/// <summary>
	/// Factory method to create a complete student inquiry with all required details.
	/// This ensures the aggregate is created in a valid state.
	/// </summary>
	public static Student CreateInquiry(
		Guid id,
		Guid? tenantId,
		string? firstName,
		string? middleName,
		string? lastName,
		Guid educationYearId,
		Guid educationBoardId,
		Guid educationMediumId,
		Guid educationMediumStageId,
		Guid? standardId,
		DateTimeOffset? dateOfBirth,
		GenderEnum? gender,
		DateTimeOffset? dateOfInquiry,
		string inquiryNo,
		Guid? inquiryPreferenceId,
		string primaryContactNo,
		string? secondaryContactNo,
		string? email,
		string? address1,
		string? address2,
		string? address3,
		string? city,
		State? state,
		Country? country,
		string? pinCode,
		string? previousSchool
	)
	{
		var student = new Student(
			id,
			tenantId,
			firstName,
			middleName,
			lastName,
			educationYearId,
			educationBoardId,
			educationMediumId,
			educationMediumStageId,
			standardId,
			status: StudentLifeCycleStatusEnum.Inquiry
		);

		// Create aggregate parts - they share the same ID as the root
		student.AdditionalDetail = new StudentAdditionalDetail(
			id,
			tenantId,
			dateOfBirth,
			gender,
			dateOfInquiry,
			inquiryNo,
			inquiryPreferenceId,
			previousSchool
		);

		student.ContactDetail = new StudentContactDetail(
			id,
			tenantId,
			primaryContactNo,
			secondaryContactNo,
			email,
			address1,
			address2,
			address3,
			city,
			state,
			country,
			pinCode
		);

		return student;
	}

	/// <summary>
	/// Factory method to create a student directly in Registration status.
	/// This bypasses the inquiry and registration form stages.
	/// </summary>
	public static Student CreateDirectRegistration(
		Guid id,
		Guid? tenantId,
		string? firstName,
		string? middleName,
		string? lastName,
		Guid educationYearId,
		Guid educationBoardId,
		Guid educationMediumId,
		Guid educationMediumStageId,
		Guid? standardId,
		Guid? divisionId,
		DateTimeOffset dateOfBirth,
		GenderEnum gender,
		string primaryContactNo,
		string? secondaryContactNo = null,
		string? email = null,
		string? address1 = null,
		string? address2 = null,
		string? address3 = null,
		string? city = null,
		State? state = null,
		Country? country = null,
		string? pincode = null
	)
	{
		var student = new Student(
			id,
			tenantId,
			firstName,
			middleName,
			lastName,
			educationYearId,
			educationBoardId,
			educationMediumId,
			educationMediumStageId,
			standardId,
			divisionId,
			status: StudentLifeCycleStatusEnum.Registration
		);

		// Create aggregate parts - they share the same ID as the root
		// For direct registration, inquiry-related fields are not applicable
		student.AdditionalDetail = new StudentAdditionalDetail(
			id,
			tenantId,
			dateOfBirth,
			gender,
			dateOfInquiry: null,
			inquiryNo: string.Empty,
			inquiryPreferenceId: null,
			previousSchool: null
		);

		student.ContactDetail = new StudentContactDetail(
			id,
			tenantId,
			primaryContactNo,
			secondaryContactNo,
			email,
			address1,
			address2,
			address3,
			city,
			state,
			country,
			pincode
		);

		return student;
	}

	/// <summary>
	/// Updates inquiry information - maintains aggregate consistency
	/// </summary>
	public void UpdateInquiry(
		string? firstName,
		string? middleName,
		string? lastName,
		Guid educationYearId,
		Guid educationBoardId,
		Guid educationMediumId,
		Guid educationMediumStageId,
		Guid? standardId,
		DateTimeOffset? dateOfBirth,
		GenderEnum? gender,
		DateTimeOffset? dateOfInquiry,
		Guid? inquiryPreferenceId,
		string primaryContactNo,
		string? secondaryContactNo,
		string? email,
		string? address1,
		string? address2,
		string? address3,
		string? city,
		State? state,
		Country? country,
		string? pinCode,
		string? previousSchool
	)
	{
		// Update root
		UpdateDetails(firstName, middleName, lastName, educationYearId, educationBoardId, educationMediumId, educationMediumStageId, standardId, StudentLifeCycleStatusEnum.Inquiry);

		// Update aggregate parts
		AdditionalDetail?.UpdateDetails(dateOfBirth, gender, dateOfInquiry, inquiryPreferenceId, previousSchool);
		ContactDetail?.UpdateContactInfo(primaryContactNo, secondaryContactNo, email, address1, address2, address3, city, state, country, pinCode);
	}

	/// <summary>
	/// Issues registration form to the student
	/// </summary>
	public void IssueRegistrationForm(string formNumber, double amount)
	{
		if (Status != StudentLifeCycleStatusEnum.Inquiry)
		{
			throw new InvalidOperationException($"Cannot issue registration form. Student must be in Inquiry status. Current status: {Status}");
		}

		AdditionalDetail?.IssueRegistrationForm(formNumber, amount);
		ChangeStatus(StudentLifeCycleStatusEnum.RegistrationFormIssued);
	}

	public void UpdateDetails(
		string? firstName,
		string? middleName,
		string? lastName,
		Guid educationYearId,
		Guid educationBoardId,
		Guid academicMediumId,
		Guid educationMediumStageId,
		Guid? standardId = null,
		StudentLifeCycleStatusEnum status = StudentLifeCycleStatusEnum.New
	)
	{
		FirstName = firstName;
		MiddleName = middleName;
		LastName = lastName;
		AcademicYearId = educationYearId;
		AcademicBoardId = educationBoardId;
		AcademicMediumId = academicMediumId;
		AcademicMediumStageId = educationMediumStageId;
		StandardId = standardId;
		Status = status;
	}

	public void UpdateGeneralInformation(
		string? firstName,
		string? middleName,
		string? lastName
	)
	{
		FirstName = firstName;
		MiddleName = middleName;
		LastName = lastName;
	}

	public void ChangeStatus(StudentLifeCycleStatusEnum status)
	{
		Status = status;
	}

	/// <summary>
	/// Changes the student's operational status (Active / Inactive / Left) and appends an
	/// immutable audit entry with the optional remarks.
	/// </summary>
	/// <remarks>
	/// Allowed only once the student has reached the operational lifecycle
	/// (Enrolled, InActive, or Left). Earlier stages such as Inquiry or Registration
	/// must follow the structured enrollment workflow.
	/// <para>
	/// Mapping: <see cref="StudentStatus.Active"/> → Enrolled,
	///          <see cref="StudentStatus.Inactive"/> → InActive,
	///          <see cref="StudentStatus.Left"/> → Left.
	/// </para>
	/// </remarks>
	public void ChangeOperationalStatus(StudentStatus newStatus, string? remarks)
	{
		var allowedFromStatuses = new[]
		{
			StudentLifeCycleStatusEnum.Enrolled,
			StudentLifeCycleStatusEnum.InActive,
			StudentLifeCycleStatusEnum.Left,
		};

		if (!Array.Exists(allowedFromStatuses, s => s == Status))
		{
			throw new InvalidOperationException(
				$"Operational status can only be changed for enrolled students. " +
				$"Current lifecycle status '{Status}' is not eligible.");
		}

		var targetLifecycleStatus = newStatus switch
		{
			StudentStatus.Active   => StudentLifeCycleStatusEnum.Enrolled,
			StudentStatus.Inactive => StudentLifeCycleStatusEnum.InActive,
			StudentStatus.Left     => StudentLifeCycleStatusEnum.Left,
			_ => throw new ArgumentOutOfRangeException(nameof(newStatus), newStatus, "Unknown student status.")
		};

		if (Status == targetLifecycleStatus)
		{
			// No-op: guard against redundant writes that would still create a history row.
			return;
		}

		var history = new StudentStatusHistory(
			id: Guid.NewGuid(),
			tenantId: TenantId,
			studentId: Id,
			fromStatus: Status,
			toStatus: targetLifecycleStatus,
			remarks: remarks);

		StatusHistories.Add(history);
		ChangeStatus(targetLifecycleStatus);
	}

	/// <summary>
	/// Schedules the student for entrance exam using a slot.
	/// Allowed from <see cref="StudentLifeCycleStatusEnum.RegistrationFormIssued"/> (inquiry path)
	/// and <see cref="StudentLifeCycleStatusEnum.Registration"/> (direct registration path).
	/// </summary>
	public void ScheduleEntranceExam(
		Guid entranceExamSlotId,
		DateTimeOffset slotStartDate,
		DateTimeOffset slotEndDate,
		int slotRoomNo)
	{
		if (Status != StudentLifeCycleStatusEnum.RegistrationFormIssued &&
		    Status != StudentLifeCycleStatusEnum.Registration)
		{
			throw new InvalidOperationException(
				$"Cannot schedule entrance exam. Student must be in RegistrationFormIssued or Registration status. Current status: {Status}");
		}

		AdditionalDetail?.ScheduleEntranceExam(
			entranceExamSlotId,
			slotStartDate,
			slotEndDate,
			slotRoomNo);

		ChangeStatus(StudentLifeCycleStatusEnum.EntranceExam);
	}

	/// <summary>
	/// Moves student to admission stage after passing entrance exam
	/// </summary>
	public void MoveToAdmission()
	{
		ChangeStatus(StudentLifeCycleStatusEnum.Admission);
		AdmissionApprovalStatus = StudentEnrollmentApprovalState.Pending;
	}

	/// <summary>
	/// Assigns or changes division for the student
	/// </summary>
	public void AssignDivision(Guid divisionId)
	{
		DivisionId = divisionId;
	}

	/// <summary>
	/// Approves the student admission
	/// </summary>
	public void ApproveAdmission()
	{
		if (Status != StudentLifeCycleStatusEnum.Admission)
		{
			throw new InvalidOperationException(
				$"Cannot approve admission. Student must be in Admission status. Current status: {Status}");
		}

		AdmissionApprovalStatus = StudentEnrollmentApprovalState.Approved;
		AdmissionApprovalRemarks = null; // Clear any previous rejection remarks
		Status = StudentLifeCycleStatusEnum.Enrolled;
	}

	/// <summary>
	/// Rejects the student admission with remarks
	/// </summary>
	public void RejectAdmission(string remarks)
	{
		if (Status != StudentLifeCycleStatusEnum.Admission)
		{
			throw new InvalidOperationException(
				$"Cannot reject admission. Student must be in Admission status. Current status: {Status}");
		}

		AdmissionApprovalStatus = StudentEnrollmentApprovalState.Rejected;
		AdmissionApprovalRemarks = remarks;
		Status = StudentLifeCycleStatusEnum.Admission; // Move back to admission for potential re-application
	}

	public void MoveToPendingAdmission(string remarks)
	{
		if (Status != StudentLifeCycleStatusEnum.Admission)
		{
			throw new InvalidOperationException(
				$"Cannot move to pending admission. Student must be in Admission status. Current status: {Status}");
		}
		AdmissionApprovalStatus = StudentEnrollmentApprovalState.Pending;
		AdmissionApprovalRemarks = remarks;
		Status = StudentLifeCycleStatusEnum.Admission; // Keep in admission status but mark as pending for re-evaluation
	}

	public void AssignHouse(Guid houseId)
	{
		EnsureAdditionalDetailExists();
		AdditionalDetail!.AssignHouse(houseId);
	}

	public void DeassignHouse()
	{
		EnsureAdditionalDetailExists();
		AdditionalDetail!.DeassignHouse();
	}

	public void AssignGlobalLeader()
	{
		EnsureAdditionalDetailExists();
		AdditionalDetail!.SetGlobalLeader(true);
	}

	public void DeassignGlobalLeader()
	{
		EnsureAdditionalDetailExists();
		AdditionalDetail!.SetGlobalLeader(false);
	}

	public void AssignDivisionLeader()
	{
		EnsureAdditionalDetailExists();
		AdditionalDetail!.SetDivisionLeader(true);
	}

	public void DeassignDivisionLeader()
	{
		EnsureAdditionalDetailExists();
		AdditionalDetail!.SetDivisionLeader(false);
	}

	public void AssignRollNo(int? rollNo)
	{
		RollNo = rollNo;
	}

	public void AssignGrNo(int? grNo)
	{
		GrNo = grNo;
	}

	public void UpdateFamilyInfo(
		ParentGuardianDetails? fatherDetails,
		ParentGuardianDetails? motherDetails,
		ParentGuardianDetails? guardianDetails,
		List<Guid>? siblingStudentIds)
	{
		FamilyDetail ??= new StudentFamilyDetail(Id, TenantId);
		FamilyDetail.UpdateFamilyInformation(fatherDetails, motherDetails, guardianDetails, siblingStudentIds);
		AdditionalDetail?.AdvanceRegistrationState(StudentRegistrationStateEnum.Family);
	}

	public void AssignFeeStructure(Guid feeStructureId)
	{
		FeeStructureId = feeStructureId;
	}

	private void EnsureAdditionalDetailExists()
	{
		if (AdditionalDetail == null)
		{
			throw new InvalidOperationException("Student additional details are missing.");
		}
	}

	Guid? IUserScopedEntity.AcademicYearId => AcademicYearId;
	Guid? IUserScopedEntity.EducationBoardId => AcademicBoardId;
	Guid? IUserScopedEntity.EducationMediumId => AcademicMediumId;
	Guid? IUserScopedEntity.EducationMediumStageId => AcademicMediumStageId;
	Guid? IUserScopedEntity.StandardId => StandardId;
	Guid? IUserScopedEntity.DivisionId => DivisionId;
}
