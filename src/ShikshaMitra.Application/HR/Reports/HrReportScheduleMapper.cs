using System;
using System.Linq;
using System.Text.Json;

namespace ShikshaMitra.HR.Reports;

internal static class HrReportScheduleMapper
{
	public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

	public static string NormalizeFrequency(string frequency) =>
		frequency is HrReportScheduleFrequency.Daily or HrReportScheduleFrequency.Weekly or HrReportScheduleFrequency.Monthly
			? frequency
			: HrReportScheduleFrequency.Monthly;

	public static string NormalizeDeliveryMode(string deliveryMode) =>
		deliveryMode == HrReportDeliveryMode.InAppAndEmail
			? HrReportDeliveryMode.InAppAndEmail
			: HrReportDeliveryMode.InApp;

	public static DateTime ComputeNextRun(string frequency, DateTime from)
	{
		var normalized = from.Date.AddHours(7);
		if (normalized <= from)
		{
			normalized = normalized.AddDays(1);
		}

		return frequency switch
		{
			HrReportScheduleFrequency.Daily => normalized,
			HrReportScheduleFrequency.Weekly => normalized.AddDays(7),
			HrReportScheduleFrequency.Monthly => normalized.AddMonths(1),
			_ => normalized.AddMonths(1)
		};
	}

	public static HrReportRequestDto DeserializeDefinition(HrReportSchedule schedule) =>
		JsonSerializer.Deserialize<HrReportRequestDto>(schedule.DefinitionJson, JsonOptions)
		?? new HrReportRequestDto { ReportType = schedule.Name };

	public static HrReportScheduleDto ToDto(HrReportSchedule schedule) => new()
	{
		Id = schedule.Id,
		Name = schedule.Name,
		TemplateId = schedule.TemplateId,
		Frequency = schedule.Frequency,
		DeliveryMode = schedule.DeliveryMode,
		Status = schedule.Status,
		RecipientUserIds = [.. schedule.GetRecipientUserIds().Where(userId => userId != schedule.OwnerUserId)],
		Definition = DeserializeDefinition(schedule),
		NextRunAt = schedule.NextRunAt,
		LastRunAt = schedule.LastRunAt,
		OwnerUserId = schedule.OwnerUserId,
		CreationTime = schedule.CreationTime,
		LastModificationTime = schedule.LastModificationTime
	};
}
