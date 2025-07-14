using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students;

public class StudentNote : FullAuditedEntity<Guid>, IMultiTenant
{
	public Guid? TenantId { get; set; }
	public Guid StudentId { get; set; }
	public string? Note { get; set; }
	public DateTimeOffset NextReminderDate { get; set; }
	public NoteStatusEnum Status { get; set; }
}
