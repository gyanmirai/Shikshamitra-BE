using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Library.Policies;

public class LibraryCalendarClosure : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public DateOnly FromDate { get; private set; }
	public DateOnly ToDate { get; private set; }
	public Guid? SectionId { get; private set; }
	public string Reason { get; private set; } = string.Empty;
	protected LibraryCalendarClosure() { }
	public LibraryCalendarClosure(Guid id, Guid? tenantId, DateOnly from, DateOnly to, Guid? sectionId, string reason) : base(id)
	{
		TenantId = tenantId;
		Set(from, to, sectionId, reason);
	}
	public void Update(DateOnly from, DateOnly to, Guid? sectionId, string reason, string? expectedStamp)
	{
		if (string.IsNullOrWhiteSpace(expectedStamp) || expectedStamp != ConcurrencyStamp)
			throw new BusinessException(LibraryErrorCodes.PolicyConflict);
		Set(from, to, sectionId, reason);
	}
	private void Set(DateOnly from, DateOnly to, Guid? sectionId, string reason)
	{
		if (from == default || to < from || sectionId == Guid.Empty || string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 500)
			throw new BusinessException(LibraryErrorCodes.CalendarInvalid, "A closure needs a valid inclusive date range and a reason.");
		FromDate = from; ToDate = to; SectionId = sectionId; Reason = reason.Trim();
	}
}
