using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students;

public class StudentDocument : FullAuditedEntity<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid StudentId { get; private set; }
	public Guid DocumentId { get; private set; }
	public string? DocumentUri { get; private set; }
}
