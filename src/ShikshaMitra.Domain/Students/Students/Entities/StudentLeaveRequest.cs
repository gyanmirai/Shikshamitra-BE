using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students;

public class StudentLeaveRequest : FullAuditedEntity<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid StudentId { get; private set; }
	public string? LeaveType { get; private set; }
	public bool IsHalfDay { get; private set; }
	public string? Description { get; private set; }
	public string? DocumentUri { get; private set; }
}
