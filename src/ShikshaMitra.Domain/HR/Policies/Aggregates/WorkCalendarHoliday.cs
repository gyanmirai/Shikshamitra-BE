using System;
using ShikshaMitra.HR.Calendar.Holidays;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.HR.Policies;

public class WorkCalendarHoliday : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; protected set; }
	public Guid AcademicYearId { get; protected set; }
	public Guid WorkCalendarPolicyId { get; protected set; }
	public DateOnly Date { get; protected set; }
	public DateOnly FromDate { get; protected set; }
	public DateOnly ToDate { get; protected set; }
	public string HolidayName { get; protected set; } = string.Empty;
	public WorkCalendarHolidaySource Source { get; protected set; }
	public Guid? CalendarHolidayId { get; protected set; }
	public bool IsIncluded { get; protected set; }
	public string? Notes { get; protected set; }

	protected WorkCalendarHoliday()
	{
	}

	public WorkCalendarHoliday(
		Guid id,
		Guid? tenantId,
		Guid academicYearId,
		Guid workCalendarPolicyId,
		DateOnly date,
		DateOnly? toDate,
		string holidayName,
		WorkCalendarHolidaySource source,
		Guid? calendarHolidayId,
		bool isIncluded,
		string? notes) : base(id)
	{
		TenantId = tenantId;
		Update(academicYearId, workCalendarPolicyId, date, toDate, holidayName, source, calendarHolidayId, isIncluded, notes);
	}

	public void Update(
		Guid academicYearId,
		Guid workCalendarPolicyId,
		DateOnly date,
		DateOnly? toDate,
		string holidayName,
		WorkCalendarHolidaySource source,
		Guid? calendarHolidayId,
		bool isIncluded,
		string? notes)
	{
		if (academicYearId == Guid.Empty)
		{
			throw new BusinessException(HRErrorCodes.AcademicYearRequired);
		}

		if (workCalendarPolicyId == Guid.Empty)
		{
			throw new BusinessException(HRErrorCodes.WorkCalendarPolicyRequired);
		}

		if (string.IsNullOrWhiteSpace(holidayName))
		{
			throw new BusinessException(HRErrorCodes.WorkCalendarHolidayNameRequired);
		}

		AcademicYearId = academicYearId;
		WorkCalendarPolicyId = workCalendarPolicyId;
		Date = date;
		FromDate = date;
		ToDate = toDate ?? date;
		if (ToDate < FromDate)
		{
			throw new BusinessException(HRErrorCodes.WorkCalendarHolidayInvalidRange);
		}
		HolidayName = holidayName.Trim();
		Source = source;
		CalendarHolidayId = calendarHolidayId;
		IsIncluded = isIncluded;
		Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
	}

	public Holiday ToCalendarHoliday(Guid? tenantId)
	{
		var id = CalendarHolidayId ?? Id;
		return new Holiday(id, tenantId, AcademicYearId, HolidayName, Guid.Empty, FromDate, ToDate, Notes);
	}
}
