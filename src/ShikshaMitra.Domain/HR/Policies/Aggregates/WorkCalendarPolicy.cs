using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.HR.Policies;

public class WorkCalendarPolicy : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; protected set; }
	public Guid AcademicYearId { get; protected set; }
	public string Name { get; protected set; } = string.Empty;
	public HrPolicyStatus Status { get; protected set; }
	public DateTime? EffectiveFrom { get; protected set; }
	public DateTime? EffectiveTo { get; protected set; }
	public AttendanceAndLeaves.Settings.AttendanceWeeklyOffPattern WeeklyOffPattern { get; protected set; }
	public string? CustomWeeklyOffsJson { get; protected set; }
	public WorkCalendarHolidaySyncMode HolidaySyncMode { get; protected set; }
	public int Version { get; protected set; }

	protected WorkCalendarPolicy()
	{
	}

	public WorkCalendarPolicy(
		Guid id,
		Guid? tenantId,
		Guid academicYearId,
		string name,
		HrPolicyStatus status,
		DateTime? effectiveFrom,
		DateTime? effectiveTo,
		AttendanceAndLeaves.Settings.AttendanceWeeklyOffPattern weeklyOffPattern,
		IReadOnlyList<int>? customWeeklyOffs,
		WorkCalendarHolidaySyncMode holidaySyncMode) : base(id)
	{
		TenantId = tenantId;
		AcademicYearId = academicYearId;
		Version = 0;
		Update(name, status, effectiveFrom, effectiveTo, weeklyOffPattern, customWeeklyOffs, holidaySyncMode);
	}

	public void Update(
		string name,
		HrPolicyStatus status,
		DateTime? effectiveFrom,
		DateTime? effectiveTo,
		AttendanceAndLeaves.Settings.AttendanceWeeklyOffPattern weeklyOffPattern,
		IReadOnlyList<int>? customWeeklyOffs,
		WorkCalendarHolidaySyncMode holidaySyncMode)
	{
		if (string.IsNullOrWhiteSpace(name))
		{
			throw new BusinessException("ShikshaMitra:WorkCalendarPolicyNameRequired");
		}

		if (status == HrPolicyStatus.Active && !effectiveFrom.HasValue)
		{
			throw new BusinessException("ShikshaMitra:PolicyEffectiveFromRequired");
		}

		if (effectiveFrom.HasValue && effectiveTo.HasValue && effectiveTo.Value.Date < effectiveFrom.Value.Date)
		{
			throw new BusinessException("ShikshaMitra:PolicyEffectiveRangeInvalid");
		}

		Name = name.Trim();
		Status = status;
		EffectiveFrom = effectiveFrom?.Date;
		EffectiveTo = effectiveTo?.Date;
		WeeklyOffPattern = weeklyOffPattern;
		CustomWeeklyOffsJson = NormalizeCustomWeeklyOffs(customWeeklyOffs);
		HolidaySyncMode = holidaySyncMode;
		Version++;
	}

	public void Archive()
	{
		Status = HrPolicyStatus.Archived;
		Version++;
	}

	public void Unarchive()
	{
		Status = HrPolicyStatus.Draft;
		Version++;
	}

	public IReadOnlyList<int> GetCustomWeeklyOffs()
	{
		if (string.IsNullOrWhiteSpace(CustomWeeklyOffsJson))
		{
			return Array.Empty<int>();
		}

		return JsonSerializer.Deserialize<List<int>>(CustomWeeklyOffsJson) ?? (IReadOnlyList<int>)Array.Empty<int>();
	}

	private static string? NormalizeCustomWeeklyOffs(IReadOnlyList<int>? customWeeklyOffs)
	{
		if (customWeeklyOffs is null || customWeeklyOffs.Count == 0)
		{
			return null;
		}

		var normalized = customWeeklyOffs
			.Distinct()
			.OrderBy(x => x)
			.ToList();

		if (normalized.Any(x => x is < 0 or > 6))
		{
			throw new BusinessException("ShikshaMitra:InvalidCustomWeeklyOff");
		}

		return JsonSerializer.Serialize(normalized);
	}
}
