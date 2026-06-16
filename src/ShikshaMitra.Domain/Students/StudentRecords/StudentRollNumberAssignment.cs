using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students;

public class StudentRollNumberAssignment : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid StudentId { get; private set; }
	public Guid DivisionId { get; private set; }
	public Guid AcademicTermId { get; private set; }
	public int? RollNo { get; private set; }

	private StudentRollNumberAssignment()
	{
		/* This constructor is for deserialization / ORM purpose */
	}

	public StudentRollNumberAssignment(
		Guid id,
		Guid? tenantId,
		Guid studentId,
		Guid divisionId,
		Guid academicTermId,
		int? rollNo)
		: base(id)
	{
		TenantId = tenantId;
		StudentId = studentId;
		DivisionId = divisionId;
		AcademicTermId = academicTermId;
		RollNo = rollNo;
	}

	public void Update(Guid divisionId, int? rollNo)
	{
		DivisionId = divisionId;
		RollNo = rollNo;
	}
}
