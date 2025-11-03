using System;
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
}
