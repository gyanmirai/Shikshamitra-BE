using System;
using ShikshaMitra.HR.AttendanceAndLeaves.LeaveManagementInfo;
using ShikshaMitra.TenantSettings.AcademicYears;
using Volo.Abp;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

internal static class LeaveManagementRequestRules
{
	public static void ValidateListPaging(LeaveManagementGetListInput input)
	{
		if (input.Page < 1)
		{
			throw new BusinessException(HRErrorCodes.LeaveInvalidPage);
		}

		if (input.UseExportBatching)
		{
			if (input.PageSize < 1 || input.PageSize > 10_000)
			{
				throw new BusinessException(HRErrorCodes.LeaveInvalidPageSize);
			}

			return;
		}

		if (input.PageSize < 10 || input.PageSize > 100)
		{
			throw new BusinessException(HRErrorCodes.LeaveInvalidPageSize);
		}
	}

	public static void ValidateCalendarPaging(LeaveManagementCalendarInput input)
	{
		if (input.Page < 1)
		{
			throw new BusinessException(HRErrorCodes.LeaveInvalidPage);
		}

		if (input.PageSize < 1 || input.PageSize > 500)
		{
			throw new BusinessException(HRErrorCodes.LeaveInvalidPageSize);
		}
	}

	public static LeaveManagementDateRange ResolveListDateRange(
		LeaveManagementGetListInput input,
		AcademicYear academicYear)
	{
		var ayStart = academicYear.StartDate.Date;
		var ayEnd = academicYear.EndDate.Date;

		if (input.MonthYearAnchor is not { } anchor)
		{
			return new LeaveManagementDateRange(ayStart, ayEnd);
		}

		var year = anchor.Year;
		var month = anchor.Month;
		if (month is < 1 or > 12)
		{
			throw new BusinessException(HRErrorCodes.LeaveInvalidMonth);
		}

		if (year is < 1900 or > 2100)
		{
			throw new BusinessException(HRErrorCodes.LeaveInvalidYear);
		}

		var monthStart = new DateTime(year, month, 1).Date;
		var monthEnd = monthStart.AddMonths(1).AddDays(-1);
		var rangeStart = monthStart > ayStart ? monthStart : ayStart;
		var rangeEnd = monthEnd < ayEnd ? monthEnd : ayEnd;
		EnsureRangeInsideAcademicYear(rangeStart, rangeEnd, anchor.ToString("yyyy-MM"), academicYear, ayStart, ayEnd);
		return new LeaveManagementDateRange(rangeStart, rangeEnd);
	}

	public static LeaveManagementDateRange ResolveCalendarDateRange(
		LeaveManagementCalendarInput input,
		AcademicYear academicYear)
	{
		var requestedStart = input.RangeStart.Date;
		var requestedEnd = input.RangeEnd.Date;
		if (requestedStart > requestedEnd)
		{
			throw new BusinessException(HRErrorCodes.LeaveStartAfterEnd);
		}

		var ayStart = academicYear.StartDate.Date;
		var ayEnd = academicYear.EndDate.Date;
		var rangeStart = requestedStart > ayStart ? requestedStart : ayStart;
		var rangeEnd = requestedEnd < ayEnd ? requestedEnd : ayEnd;
		EnsureRangeInsideAcademicYear(rangeStart, rangeEnd, requestedStart.ToString("yyyy-MM"), academicYear, ayStart, ayEnd);
		return new LeaveManagementDateRange(rangeStart, rangeEnd);
	}

	private static void EnsureRangeInsideAcademicYear(
		DateTime rangeStart,
		DateTime rangeEnd,
		string requestedMonth,
		AcademicYear academicYear,
		DateTime academicYearStart,
		DateTime academicYearEnd)
	{
		if (rangeStart <= rangeEnd)
		{
			return;
		}

		throw new BusinessException(HRErrorCodes.LeaveInvalidMonthForAcademicYear)
			.WithData("requestedMonth", requestedMonth)
			.WithData("academicYearName", academicYear.Name ?? string.Empty)
			.WithData("academicYearStart", academicYearStart.ToString("yyyy-MM-dd"))
			.WithData("academicYearEnd", academicYearEnd.ToString("yyyy-MM-dd"));
	}
}

internal sealed record LeaveManagementDateRange(DateTime Start, DateTime End);
