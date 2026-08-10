using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmReportScheduleWorkflow(
	IRepository<AdmissionCrmReportSchedule, Guid> scheduleRepository,
	IRepository<AdmissionCrmExportRequest, Guid> exportRepository,
	AdmissionCrmExportContentBuilder contentBuilder,
	AdmissionCrmGovernanceAccess access,
	AdmissionCrmStaffEligibilityService staffEligibilityService,
	AdmissionCrmReportScheduleDeliveryService reportScheduleDeliveryService)
	: ApplicationService, ITransientDependency
{
	public async Task<AdmissionCrmReportScheduleDto> CreateAsync(Guid academicYearId, CreateAdmissionCrmReportScheduleDto input)
	{
		await access.EnsureReportScheduleAccessAsync(academicYearId, "CreateReportScheduleAsync");
		if (TryParseRecipient(input.Destination, out var recipientUserId))
		{
			var eligibleStaff = await staffEligibilityService.GetEligibleEmployeesByUserIdAsync();
			if (!eligibleStaff.ContainsKey(recipientUserId))
				throw new BusinessException("AdmissionCrm:ScheduledReportRecipientMustBeAdmissionsStaff").WithData("UserId", recipientUserId);
			await reportScheduleDeliveryService.EnsureRecipientAsync(recipientUserId);
		}
		else if (!input.Destination.StartsWith("manual:", StringComparison.OrdinalIgnoreCase))
			throw new BusinessException("AdmissionCrm:ApprovedReportDestinationRequired");
		var entity = new AdmissionCrmReportSchedule(GuidGenerator.Create(), CurrentTenant.Id, academicYearId,
			input.Name, input.ReportType, input.Frequency, input.Destination, AdmissionCrmGovernanceMapper.Serialize(input.Filters),
			input.NextRunAt, UserId());
		await scheduleRepository.InsertAsync(entity, autoSave: true);
		return AdmissionCrmGovernanceMapper.Schedule(entity);
	}

	public async Task<IReadOnlyList<AdmissionCrmReportScheduleDto>> GetAsync(Guid academicYearId)
	{
		await access.EnsureReportScheduleAccessAsync(academicYearId, "GetReportSchedulesAsync");
		var queryable = await scheduleRepository.GetQueryableAsync();
		var items = await AsyncExecuter.ToListAsync(queryable.Where(x => x.TenantId == CurrentTenant.Id && x.AcademicYearId == academicYearId));
		return items.OrderBy(x => x.NextRunAt).Select(AdmissionCrmGovernanceMapper.Schedule).ToList();
	}

	public async Task<AdmissionCrmReportScheduleDto> SetActiveAsync(Guid id, bool active)
	{
		var entity = await scheduleRepository.GetAsync(id);
		await access.EnsureReportScheduleAccessAsync(entity.AcademicYearId, "SetReportScheduleActiveAsync");
		entity.SetActive(active);
		await scheduleRepository.UpdateAsync(entity, autoSave: true);
		return AdmissionCrmGovernanceMapper.Schedule(entity);
	}

	public async Task<AdmissionCrmReportScheduleRunResultDto> RunDueAsync(Guid academicYearId, bool enforceUserScope)
	{
		if (enforceUserScope) await access.EnsureReportScheduleAccessAsync(academicYearId, "RunDueReportSchedulesCoreAsync");
		else await access.EnsureAutomationYearAsync(academicYearId, "RunDueReportSchedulesCoreAsync");
		var now = Clock.Now;
		var queryable = await scheduleRepository.GetQueryableAsync();
		var due = await AsyncExecuter.ToListAsync(queryable.Where(x => x.TenantId == CurrentTenant.Id &&
			x.AcademicYearId == academicYearId && x.IsActive && x.NextRunAt <= now));
		var exportIds = new List<Guid>();
		var failedIds = new List<Guid>();
		var failureDetails = new Dictionary<Guid, string>();
		foreach (var schedule in due)
		{
			try
			{
				var request = new RequestAdmissionCrmExportDto
				{
					ReportType = schedule.ReportType,
					Filters = AdmissionCrmGovernanceMapper.Deserialize<Dictionary<string, string?>>(schedule.FiltersJson) ?? new(),
					ExpiresInHours = 24
				};
				var content = await contentBuilder.BuildAsync(academicYearId, request, now, enforceUserScope);
				var exportId = GuidGenerator.Create();
				await exportRepository.InsertAsync(new AdmissionCrmExportRequest(exportId, CurrentTenant.Id, academicYearId,
					request.ReportType, schedule.FiltersJson, $"admissions-{request.ReportType.ToString().ToLowerInvariant()}-{now:yyyyMMddHHmm}-{exportId:N}.csv",
					content, schedule.CreatedByUserId, now, now.AddHours(24)), autoSave: true);
				schedule.MarkRun(now);
				await scheduleRepository.UpdateAsync(schedule, autoSave: true);
				exportIds.Add(exportId);
				if (TryParseRecipient(schedule.Destination, out var recipientUserId) &&
				    !await reportScheduleDeliveryService.SendAsync(schedule.Id, schedule.Name, academicYearId, exportId, recipientUserId))
				{
					failedIds.Add(schedule.Id);
					failureDetails[schedule.Id] = "AdmissionCrm:ScheduledReportDeliveryFailed";
				}
			}
			catch (Exception exception)
			{
				failedIds.Add(schedule.Id);
				failureDetails[schedule.Id] = exception is BusinessException businessException
					? businessException.Code ?? businessException.Message : $"{exception.GetType().Name}: {exception.Message}";
				Logger.LogError(exception, "Failed to generate scheduled admissions report {ScheduleId}.", schedule.Id);
			}
		}
		return new AdmissionCrmReportScheduleRunResultDto
		{
			AcademicYearId = academicYearId, EvaluatedCount = due.Count, GeneratedCount = exportIds.Count,
			FailedCount = failedIds.Count, ExportIds = exportIds, FailedScheduleIds = failedIds, FailureDetails = failureDetails
		};
	}

	private static bool TryParseRecipient(string destination, out Guid userId)
	{
		const string prefix = "user:";
		if (destination.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
		    Guid.TryParse(destination[prefix.Length..], out userId) && userId != Guid.Empty) return true;
		userId = Guid.Empty;
		return false;
	}

	private Guid UserId() => CurrentUser.Id ?? throw new AbpAuthorizationException("Authenticated user required.");
}
