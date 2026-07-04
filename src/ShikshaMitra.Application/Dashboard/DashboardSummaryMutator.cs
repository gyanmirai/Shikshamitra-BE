using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ShikshaMitra.Dashboard;

internal static class DashboardSummaryMutator
{
	public static void Add(ICollection<DashboardWidgetDto> widgets, DashboardWidgetDto widget) =>
		widgets.Add(widget);

	public static void UpdateWidget(
		IEnumerable<DashboardWidgetDto> widgets,
		string id,
		int? count,
		object? value,
		string subtitle,
		string status,
		string? title = null,
		string? dueLabel = null,
		string? health = null)
	{
		var widget = widgets.FirstOrDefault(w => w.Id == id);
		if (widget == null)
		{
			return;
		}

		widget.Title = title ?? widget.Title;
		widget.Count = count;
		widget.Value = value ?? widget.Value;
		widget.Subtitle = subtitle;
		widget.Status = status;
		widget.DueLabel = dueLabel ?? widget.DueLabel;
		widget.Health = health ?? widget.Health;
	}

	public static string BuildCountSubtitle(int count, string singular, string plural) =>
		count == 0
			? "No pending action in scope"
			: $"{count} {(count == 1 ? singular : plural)}";

	public static string CountStatus(int count) =>
		count == 0 ? "success" : count < 5 ? "info" : "warning";

	public static string FormatIndianAmount(double amount)
	{
		if (amount >= 10000000d)
		{
			return $"INR {amount / 10000000d:0.##}Cr";
		}

		if (amount >= 100000d)
		{
			return $"INR {amount / 100000d:0.##}L";
		}

		return $"INR {amount:0}";
	}

	public static DateTime ParseNormalizedDate(string? value) =>
		DateTime.ParseExact(value ?? DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), "yyyy-MM-dd", CultureInfo.InvariantCulture);

	public static void SetZoneError(DashboardSummaryDto summary, string zone, string message)
	{
		if (!summary.ZoneErrors.ContainsKey(zone))
		{
			summary.ZoneErrors[zone] = message;
		}
	}

	public static DashboardWidgetDto Widget(
		string id,
		string title,
		object? value,
		string? subtitle,
		string status,
		int? count = null,
		string? health = null) =>
		new()
		{
			Id = id,
			Title = title,
			Value = value,
			Count = count,
			Subtitle = subtitle,
			Status = status,
			Health = health
		};
}
