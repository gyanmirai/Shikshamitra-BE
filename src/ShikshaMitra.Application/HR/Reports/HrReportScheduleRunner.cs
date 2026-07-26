using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.HR.Audit;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.HR.Reports;

public class HrReportScheduleRunner(
	IRepository<HrReportSchedule, Guid> scheduleRepository,
	HrReportExportService exportService,
	HrReportRecipientLookupService recipientLookupService,
	HrReportScheduleDeliveryService deliveryService,
	HrReportSchedulePostCommitDispatcher postCommitDispatcher,
	HrAuditWriter auditWriter) : ApplicationService, ITransientDependency
{
	public async Task<HrReportExportCreateResponseDto> RunScheduleAsync(
		HrReportSchedule schedule,
		string auditAction,
		bool advanceNextRun,
		DateTime? runAt = null)
	{
		var executedAt = runAt ?? Clock.Now;
		if (!await recipientLookupService.HasReportViewPermissionAsync(schedule.OwnerUserId))
		{
			throw new BusinessException(HRErrorCodes.ReportAccessDenied)
				.WithData("ownerUserId", schedule.OwnerUserId)
				.WithData("reason", "The schedule owner no longer has HR report view permission.");
		}

		var request = HrReportScheduleMapper.DeserializeDefinition(schedule);
		request.Format = string.IsNullOrWhiteSpace(request.Format) ? HrReportFormat.Excel : request.Format;
		request.SourceAction = "scheduleRun";
		request.ParentJobId = null;
		request.TemplateId ??= schedule.TemplateId;
		var recipients = schedule.GetRecipientUserIds();
		var export = await exportService.CreateExportForUserAsync(
			request,
			schedule.OwnerUserId,
			schedule.TenantId,
			recipients,
			sourceAction: "scheduleRun");
		var nextRunAt = advanceNextRun
			? HrReportScheduleMapper.ComputeNextRun(schedule.Frequency, executedAt)
			: schedule.NextRunAt;
		schedule.MarkRun(executedAt, nextRunAt);
		await scheduleRepository.UpdateAsync(schedule, autoSave: true);
		await auditWriter.WriteAsync(
			null,
			"hr_report_schedule",
			schedule.Id,
			"HR Report Schedule",
			auditAction,
			$"Ran HR report schedule {schedule.Name}",
			context: new
			{
				schedule.Name,
				export.JobId,
				schedule.DeliveryMode,
				RecipientUserIds = recipients,
				request.DataSource,
				request.Columns
			});

		if (schedule.DeliveryMode == HrReportDeliveryMode.InAppAndEmail)
		{
			var scheduleId = schedule.Id;
			var scheduleName = schedule.Name;
			var tenantId = schedule.TenantId;
			var deliveryRecipients = recipients.ToArray();
			await postCommitDispatcher.DispatchAsync(
				() => deliveryService.SendDeliveryEmailsAsync(
					scheduleId,
					scheduleName,
					tenantId,
					export.JobId,
					deliveryRecipients),
				scheduleId);
		}

		return export;
	}
}
