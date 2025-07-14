using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students;

public class StudentDocument : FullAuditedEntity<Guid>, IMultiTenant
{
	public Guid? TenantId { get; set; }
	public Guid StudentId { get; set; }
	public Guid DocumentId { get; set; }
	public string? DocumentUri { get; set; }
}
