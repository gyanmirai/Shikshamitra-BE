using ShikshaMitra.Finance.Collections;
using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Finance.Fees.FeeCollection;

public class DueFee : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid StudentId { get; private set; }
	public Guid FeeHeadId { get; private set; }
	public MonthEnum Month { get; private set; }
	public int Year { get; private set; }
	public bool IsOneTime { get; private set; }
	public double OriginalAmount { get; private set; }
	public double NetAmount { get; private set; }
	public double PaidAmount { get; private set; }
	public double OutstandingAmount { get; private set; }
	public FeeStatus Status { get; set; }

	private DueFee() { /* For ORM */ }

	public DueFee(
		Guid id,
		Guid? tenantId,
		Guid studentId,
		Guid feeHeadId,
		MonthEnum month,
		int year,
		bool isOneTime,
		double amount)
		: base(id)
	{
		TenantId = tenantId;
		StudentId = studentId;
		FeeHeadId = feeHeadId;
		Month = month;
		Year = year;
		IsOneTime = isOneTime;
		OriginalAmount = amount;
		NetAmount = 0;
		PaidAmount = 0;
		OutstandingAmount = amount;
		Status = FeeStatus.Pending;
	}
}
