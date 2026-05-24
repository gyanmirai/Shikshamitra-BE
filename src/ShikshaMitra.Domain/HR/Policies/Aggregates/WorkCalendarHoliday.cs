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
	public DateTime Date { get; protected set; }
	public DateTime FromDate { get; protected set; }
	public DateTime ToDate { get; protected set; }
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
		DateTime date,
		DateTime? toDate,
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
		DateTime date,
		DateTime? toDate,
		string holidayName,
		WorkCalendarHolidaySource source,
		Guid? calendarHolidayId,
		bool isIncluded,
		string? notes)
	{
		if (string.IsNullOrWhiteSpace(holidayName))
		{
			throw new BusinessException("ShikshaMitra:WorkCalendarHolidayNameRequired");
		}

		AcademicYearId = academicYearId;
		WorkCalendarPolicyId = workCalendarPolicyId;
		Date = date.Date;
		FromDate = date.Date;
		ToDate = (toDate ?? date).Date;
		if (ToDate < FromDate)
		{
			throw new BusinessException("ShikshaMitra:WorkCalendarHolidayInvalidRange");
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
		var fromDate = new DateTimeOffset(FromDate.Year, FromDate.Month, FromDate.Day, 0, 0, 0, TimeSpan.Zero);
		var toDate = new DateTimeOffset(ToDate.Year, ToDate.Month, ToDate.Day, 0, 0, 0, TimeSpan.Zero);
		return new Holiday(id, tenantId, AcademicYearId, HolidayName, Guid.Empty, fromDate, toDate, Notes);
	}
}
