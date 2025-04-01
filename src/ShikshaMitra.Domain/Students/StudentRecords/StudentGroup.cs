using System;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.StudentRecords;

public class StudentGroup : AggregateRoot<Guid>, IMultiTenant
{
	public string Name { get;set; }

	public Guid? TenantId { get; set; }
	private StudentGroup() { }

	public StudentGroup(Guid id, string name, Guid? tenantId = null) : base(id)
	{
		Name = name;
		TenantId = tenantId;
	}
}
