using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.HR.Payroll.Settings;

public class DayCalculationSetting : AuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; protected set; }

	public Guid? AcademicYearId { get; protected set; }

	public DayCalculationType DayCalculationType { get; protected set; }

	public int? ManualDays { get; protected set; }

	public string? ManualMonthDaysJson { get; protected set; }

	protected DayCalculationSetting()
	{
	}

	public DayCalculationSetting(
		Guid id,
		Guid? tenantId,
		DayCalculationType dayCalculationType,
		int? manualDays,
		Guid? academicYearId = null,
		IReadOnlyList<DayCalculationManualMonth>? manualMonthDays = null) : base(id)
	{
		TenantId = tenantId;
		AcademicYearId = academicYearId;
		SetFields(dayCalculationType, manualDays, manualMonthDays);
	}

	public void Update(DayCalculationType dayCalculationType, int? manualDays, IReadOnlyList<DayCalculationManualMonth>? manualMonthDays = null)
	{
		SetFields(dayCalculationType, manualDays, manualMonthDays);
	}

	public IReadOnlyList<DayCalculationManualMonth> GetManualMonthDays()
	{
		if (string.IsNullOrWhiteSpace(ManualMonthDaysJson))
		{
			return Array.Empty<DayCalculationManualMonth>();
		}

		return JsonSerializer.Deserialize<List<DayCalculationManualMonth>>(ManualMonthDaysJson)
			?? (IReadOnlyList<DayCalculationManualMonth>)Array.Empty<DayCalculationManualMonth>();
	}

	public int? GetManualDaysForMonth(int year, int month)
	{
		var manualMonth = GetManualMonthDays().FirstOrDefault(x => x.Year == year && x.Month == month);
		return manualMonth?.ManualDays ?? ManualDays;
	}

	private void SetFields(DayCalculationType dayCalculationType, int? manualDays, IReadOnlyList<DayCalculationManualMonth>? manualMonthDays)
	{
		DayCalculationType = dayCalculationType;

		if (dayCalculationType == DayCalculationType.ManualDays)
		{
			var normalizedMonths = NormalizeManualMonthDays(manualMonthDays);
			if (normalizedMonths.Count > 0)
			{
				ManualDays = null;
				ManualMonthDaysJson = JsonSerializer.Serialize(normalizedMonths);
				return;
			}

			if (manualDays.HasValue && manualDays.Value >= 1 && manualDays.Value <= 31)
			{
				ManualDays = manualDays.Value;
				ManualMonthDaysJson = null;
				return;
			}

			throw new BusinessException("ShikshaMitra:InvalidManualDays")
				.WithData("message", "manualDays or manualMonthDays must be provided when dayCalculationType is manual-days.");
		}

		ManualDays = null;
		ManualMonthDaysJson = null;
	}

	private static List<DayCalculationManualMonth> NormalizeManualMonthDays(IReadOnlyList<DayCalculationManualMonth>? manualMonthDays)
	{
		if (manualMonthDays is null || manualMonthDays.Count == 0)
		{
			return new List<DayCalculationManualMonth>();
		}

		var duplicate = manualMonthDays
			.GroupBy(x => new { x.Year, x.Month })
			.FirstOrDefault(g => g.Count() > 1);
		if (duplicate != null)
		{
			throw new BusinessException("ShikshaMitra:DuplicateManualMonthDays")
				.WithData("message", "manualMonthDays cannot contain duplicate year/month entries.");
		}

		return manualMonthDays
			.Select(x =>
			{
				if (x.Month is < 1 or > 12)
				{
					throw new BusinessException("ShikshaMitra:InvalidManualMonth")
						.WithData("message", "manualMonthDays month must be between 1 and 12.");
				}

				var calendarDays = DateTime.DaysInMonth(x.Year, x.Month);
				if (x.ManualDays < 1 || x.ManualDays > calendarDays)
				{
					throw new BusinessException("ShikshaMitra:InvalidManualDays")
						.WithData("message", $"manualDays for {x.Year}-{x.Month:00} must be between 1 and {calendarDays}.");
				}

				return new DayCalculationManualMonth(x.Year, x.Month, x.ManualDays);
			})
			.OrderBy(x => x.Year)
			.ThenBy(x => x.Month)
			.ToList();
	}
}

public sealed record DayCalculationManualMonth(int Year, int Month, int ManualDays);
