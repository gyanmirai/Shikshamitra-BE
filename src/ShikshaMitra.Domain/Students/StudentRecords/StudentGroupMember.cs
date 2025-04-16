using System;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.StudentRecords;

public class StudentGroupMember : Entity<Guid>, IMultiTenant
{
	public Guid StudentId { get; private set; }
	public Guid StudentGroupId { get; private set; }
	public bool IsLeader { get; private set; }
	public Guid? TenantId { get; set; }

	public virtual Student Student { get; private set; }
	public virtual StudentGroup StudentGroup { get; private set; }

	protected StudentGroupMember() { }

	public StudentGroupMember(
		Guid id,
		Guid studentId,
		Guid studentGroupId,
		bool isLeader = false,
		Guid? tenantId = null) : base(id)
	{
		StudentId = studentId;
		StudentGroupId = studentGroupId;
		IsLeader = isLeader;
		TenantId = tenantId;
	}

	public void SetAsLeader()
	{
		IsLeader = true;
	}

	public void RemoveAsLeader()
	{
		IsLeader = false;
	}
}
