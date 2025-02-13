using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.StudentEnrollments.Settings;

public class EntranceExamSlot : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public DateTimeOffset StartDate { get; private set; }
	public DateTimeOffset EndDate { get; private set; }

	public int StudentLimit { get; private set; }

	public int RoomNo { get; private set; }

	protected EntranceExamSlot() { } // For ORM

	internal EntranceExamSlot(
		Guid id,
		Guid? tenantId,
		DateTimeOffset startDate,
		DateTimeOffset endDate,
		int studentLimit,
		int roomNo) : base(id)
	{
		TenantId = tenantId ?? throw new ArgumentNullException(nameof(tenantId));
		StartDate = startDate;
		EndDate = endDate;
		StudentLimit = studentLimit;
		RoomNo = roomNo;
	}
}
