using ShikshaMitra.Academic.HomeworkAssignment;
using System;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Academic.Homework;
public class HomeworkAssignmentType : Entity<Guid>, IMultiTenant
{
	public Guid? TenantId { get; set; }
	public required string Name { get; set; }
	public WorkType Type { get; set; }

	// Private constructor for EF Core
	private HomeworkAssignmentType() { }

	// Public constructor
	public HomeworkAssignmentType(Guid id, string name, WorkType type, Guid? tenantId = null) : base(id)
	{
		Name = name;
		Type = type;
		TenantId = tenantId;
	}
}
