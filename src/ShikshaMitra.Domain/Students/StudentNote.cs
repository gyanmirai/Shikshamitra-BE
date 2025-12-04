using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students;

public class StudentNote : FullAuditedEntity<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid StudentId { get; private set; }
	public string? Note { get; private set; }
	public DateTimeOffset NextReminderDate { get; private set; }
	public NoteStatusEnum Status { get; private set; }
}
