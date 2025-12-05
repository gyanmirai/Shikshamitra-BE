using System;
using ShikshaMitra.Students.StudentEnrollments;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students;

public class Student : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public string? FirstName { get; private set; }
	public string? MiddleName { get; private set; }
	public string? LastName { get; private set; }
	public Guid EducationMediumId { get; private set; }
	public Guid? StandardId { get; private set; }
	public Guid? DivisionId { get; private set; }
	public int? GrNo { get; private set; }
	public int? RollNo { get; private set; }
	public StudentLifeCycleStatusEnum Status { get; private set; }

	// Navigation properties to aggregate parts (1:1 relationships)
	public StudentAdditionalDetail? AdditionalDetail { get; private set; }
	public StudentContactDetail? ContactDetail { get; private set; }

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
		Guid educationMediumId,
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
		EducationMediumId = educationMediumId;
		StandardId = standardId;
		DivisionId = divisionId;
		GrNo = grNo;
		RollNo = rollNo;
		Status = status;
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
		Guid educationMediumId,
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
		string? state,
		string? country,
		string? pinCode
	)
	{
		var student = new Student(
			id,
			tenantId,
			firstName,
			middleName,
			lastName,
			educationMediumId,
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
			inquiryPreferenceId
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
	/// Updates inquiry information - maintains aggregate consistency
	/// </summary>
	public void UpdateInquiry(
		string? firstName,
		string? middleName,
		string? lastName,
		Guid educationMediumId,
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
		string? state,
		string? country,
		string? pinCode
	)
	{
		// Update root
		UpdateDetails(firstName, middleName, lastName, educationMediumId, standardId, StudentLifeCycleStatusEnum.Inquiry);

		// Update aggregate parts
		AdditionalDetail?.UpdateDetails(dateOfBirth, gender, dateOfInquiry, inquiryPreferenceId);
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
		Guid academicMediumId,
		Guid? standardId = null,
		StudentLifeCycleStatusEnum status = StudentLifeCycleStatusEnum.New
	)
	{
		FirstName = firstName;
		MiddleName = middleName;
		LastName = lastName;
		EducationMediumId = academicMediumId;
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
	/// Schedules the student for entrance exam using a slot
	/// </summary>
	public void ScheduleEntranceExam(
		Guid entranceExamSlotId,
		DateTimeOffset slotStartDate,
		DateTimeOffset slotEndDate,
		int slotRoomNo)
	{
		if (Status != StudentLifeCycleStatusEnum.RegistrationFormIssued)
		{
			throw new InvalidOperationException(
				$"Cannot schedule entrance exam. Student must be in RegistrationFormIssued status. Current status: {Status}");
		}

		AdditionalDetail?.ScheduleEntranceExam(
			entranceExamSlotId,
			slotStartDate,
			slotStartDate.TimeOfDay,
			slotEndDate.TimeOfDay,
			slotRoomNo);

		ChangeStatus(StudentLifeCycleStatusEnum.EntranceExam);
	}

	/// <summary>
	/// Moves student to admission stage after passing entrance exam
	/// </summary>
	public void MoveToAdmission()
	{
		if (Status != StudentLifeCycleStatusEnum.EntranceExam)
		{
			throw new InvalidOperationException(
				$"Cannot move to admission. Student must be in EntranceExam status. Current status: {Status}");
		}

		if (AdditionalDetail?.ExamStatus != StudentEnrollments.EntranceExamStatusEnum.Pass)
		{
			throw new InvalidOperationException(
				"Cannot move to admission. Student must have passed the entrance exam.");
		}

		ChangeStatus(StudentLifeCycleStatusEnum.Admission);
	}
}
