using System;
using System.Collections.Generic;
using System.Linq;
using ShikshaMitra.TenantSettings.AcademicYears;
using Volo.Abp;

namespace ShikshaMitra.HR.Policies;

internal static class WorkCalendarHolidaySnapshotValidator
{
	public static void ValidateRows(List<WorkCalendarHolidayDto> holidays, AcademicYear academicYear)
	{
		foreach (var row in holidays)
		{
			ValidateHolidayRow(row, academicYear);
		}

		ValidateIncludedHolidayOverlaps(holidays);
	}

	private static void ValidateIncludedHolidayOverlaps(List<WorkCalendarHolidayDto> holidays)
	{
		var includedRows = holidays
			.Where(x => x.IsIncluded)
			.Select(x => new
			{
				Row = x,
				FromDate = WorkCalendarPolicyChangeDetector.ResolveHolidayFromDate(x),
				ToDate = WorkCalendarPolicyChangeDetector.ResolveHolidayToDate(x)
			})
			.OrderBy(x => x.FromDate)
			.ThenBy(x => x.ToDate)
			.ThenBy(x => x.Row.HolidayName)
			.ToList();

		for (var index = 1; index < includedRows.Count; index++)
		{
			var current = includedRows[index];
			for (var priorIndex = index - 1; priorIndex >= 0; priorIndex--)
			{
				var prior = includedRows[priorIndex];
				if (prior.ToDate < current.FromDate)
				{
					break;
				}

				if (RangesOverlap(prior.FromDate, prior.ToDate, current.FromDate, current.ToDate))
				{
					var overlapFrom = prior.FromDate > current.FromDate ? prior.FromDate : current.FromDate;
					var overlapTo = prior.ToDate < current.ToDate ? prior.ToDate : current.ToDate;
					throw new BusinessException(HRErrorCodes.WorkCalendarHolidayOverlap)
						.WithData("field", nameof(WorkCalendarHolidayDto.FromDate))
						.WithData("holidayName", current.Row.HolidayName)
						.WithData("conflictingHolidayName", prior.Row.HolidayName)
						.WithData("fromDate", overlapFrom.ToString("yyyy-MM-dd"))
						.WithData("toDate", overlapTo.ToString("yyyy-MM-dd"));
				}
			}
		}
	}

	private static bool RangesOverlap(DateOnly leftFrom, DateOnly leftTo, DateOnly rightFrom, DateOnly rightTo) =>
		leftFrom <= rightTo && rightFrom <= leftTo;

	private static void ValidateHolidayRow(WorkCalendarHolidayDto row, AcademicYear academicYear)
	{
		var fromDate = WorkCalendarPolicyChangeDetector.ResolveHolidayFromDate(row);
		var toDate = WorkCalendarPolicyChangeDetector.ResolveHolidayToDate(row);
		if (fromDate == default)
		{
			throw new BusinessException(HRErrorCodes.WorkCalendarHolidayDateRequired)
				.WithData("field", nameof(row.FromDate));
		}

		if (toDate == default)
		{
			throw new BusinessException(HRErrorCodes.WorkCalendarHolidayDateRequired)
				.WithData("field", nameof(row.ToDate));
		}

		if (toDate < fromDate)
		{
			throw new BusinessException(HRErrorCodes.WorkCalendarHolidayInvalidRange)
				.WithData("field", nameof(row.ToDate))
				.WithData("fromDate", fromDate.ToString("yyyy-MM-dd"))
				.WithData("toDate", toDate.ToString("yyyy-MM-dd"));
		}

		if (string.IsNullOrWhiteSpace(row.HolidayName))
		{
			throw new BusinessException(HRErrorCodes.WorkCalendarHolidayNameRequired)
				.WithData("field", nameof(row.HolidayName))
				.WithData("date", fromDate.ToString("yyyy-MM-dd"));
		}

		if (row.AcademicYearId != Guid.Empty && row.AcademicYearId != academicYear.Id)
		{
			throw new BusinessException(HRErrorCodes.WorkCalendarHolidayAcademicYearMismatch)
				.WithData("field", nameof(row.AcademicYearId))
				.WithData("date", fromDate.ToString("yyyy-MM-dd"));
		}

		var academicYearStart = DateOnly.FromDateTime(academicYear.StartDate);
		var academicYearEnd = DateOnly.FromDateTime(academicYear.EndDate);
		if (fromDate < academicYearStart || toDate > academicYearEnd)
		{
			throw new BusinessException(HRErrorCodes.WorkCalendarHolidayOutsideAcademicYear)
				.WithData("field", nameof(row.FromDate))
				.WithData("fromDate", fromDate.ToString("yyyy-MM-dd"))
				.WithData("toDate", toDate.ToString("yyyy-MM-dd"))
				.WithData("academicYear", academicYear.Name);
		}
	}
}
