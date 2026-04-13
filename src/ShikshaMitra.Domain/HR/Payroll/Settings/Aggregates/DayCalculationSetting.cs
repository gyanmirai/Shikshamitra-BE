using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.HR.Payroll.Settings;

public class DayCalculationSetting : AuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; protected set; }

	public DayCalculationType DayCalculationType { get; protected set; }

	public int? ManualDays { get; protected set; }

	protected DayCalculationSetting()
	{
	}

	public DayCalculationSetting(
		Guid id,
		Guid? tenantId,
		DayCalculationType dayCalculationType,
		int? manualDays) : base(id)
	{
		TenantId = tenantId;
		SetFields(dayCalculationType, manualDays);
	}

	public void Update(DayCalculationType dayCalculationType, int? manualDays)
	{
		SetFields(dayCalculationType, manualDays);
	}

	private void SetFields(DayCalculationType dayCalculationType, int? manualDays)
	{
		DayCalculationType = dayCalculationType;

		if (dayCalculationType == DayCalculationType.ManualDays)
		{
			if (!manualDays.HasValue || manualDays.Value < 1 || manualDays.Value > 31)
			{
				throw new BusinessException("ShikshaMitra:InvalidManualDays")
					.WithData("message", "manualDays must be between 1 and 31 when dayCalculationType is manual-days.");
			}

			ManualDays = manualDays.Value;
		}
		else
		{
			ManualDays = null;
		}
	}
}
