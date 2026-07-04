using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Application.Contracts.HR.Calendar.CalendarView;
using ShikshaMitra.Application.HR.Calendar.CalendarView;
using ShikshaMitra.HR.Calendar;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.DependencyInjection;
using static ShikshaMitra.Dashboard.DashboardSummaryMutator;
using static ShikshaMitra.Dashboard.DashboardWidgetCatalog;

namespace ShikshaMitra.Dashboard;

public class DashboardCalendarSliceProvider(
	CalendarViewReadService calendarViewReadService,
	IPermissionChecker permissionChecker)
	: IDashboardSummarySliceProvider, ITransientDependency
{
	public async Task ApplyAsync(DashboardSummarySliceContext context)
	{
		var summary = context.Summary;
		if (!Guid.TryParse(summary.AcademicYearId, out var academicYearId))
		{
			return;
		}

		if (!await HasAnyPermissionAsync(WidgetDefinitions["calendar-safety-drill"].RequiredPermissions))
		{
			return;
		}

		try
		{
			var items = await calendarViewReadService.GetViewAsync(new GetCalendarViewRequestDto
			{
				AcademicYearId = academicYearId,
				Statuses = [0, 1],
				SkipCount = 0,
				MaxResultCount = 20
			});

			var nextItem = items.Items
				.OrderBy(i => i.StartDate)
				.FirstOrDefault();

			if (nextItem == null)
			{
				UpdateWidget(
					summary.CalendarItems,
					"calendar-safety-drill",
					0,
					null,
					"No visible events or holidays in the active academic year",
					"info",
					title: "Calendar readiness",
					dueLabel: "Open");
				return;
			}

			var toDate = ParseNormalizedDate(context.DateRange.To);
			UpdateWidget(
				summary.CalendarItems,
				"calendar-safety-drill",
				null,
				null,
				BuildCalendarSubtitle(nextItem),
				nextItem.CalendarItemType == CalendarItemType.Holiday ? "info" : "primary",
				title: nextItem.Title,
				dueLabel: BuildCalendarDueLabel(nextItem.StartDate, toDate));
		}
		catch
		{
			SetZoneError(summary, DashboardZones.Calendar, "Calendar snapshot could not be loaded.");
		}
	}

	private async Task<bool> HasAnyPermissionAsync(string[] permissionNames)
	{
		foreach (var permissionName in permissionNames)
		{
			if (await permissionChecker.IsGrantedAsync(permissionName))
			{
				return true;
			}
		}

		return false;
	}

	private static string BuildCalendarSubtitle(CalendarViewItemDto item)
	{
		var itemType = item.CalendarItemType == CalendarItemType.Holiday ? "Holiday" : "Event";
		var detail = !string.IsNullOrWhiteSpace(item.Venue)
			? item.Venue
			: !string.IsNullOrWhiteSpace(item.TypeName)
				? item.TypeName
				: "Calendar";
		var time = !string.IsNullOrWhiteSpace(item.StartTime)
			? $", {item.StartTime}"
			: string.Empty;

		return $"{itemType}: {detail}{time}";
	}

	private static string BuildCalendarDueLabel(DateTimeOffset startDate, DateTime referenceDate)
	{
		var itemDate = startDate.Date;
		var today = referenceDate.Date;
		if (itemDate == today)
		{
			return "Today";
		}

		if (itemDate == today.AddDays(1))
		{
			return "Tomorrow";
		}

		return itemDate.ToString("dd MMM", CultureInfo.InvariantCulture);
	}
}
