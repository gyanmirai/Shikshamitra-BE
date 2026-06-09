using System;
using System.Threading.Tasks;
using ShikshaMitra.HR.Audit;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp;

namespace ShikshaMitra.HR.Policies;

[RemoteService(false)]
public class WorkCalendarPolicyWorkflow(
	IRepository<WorkCalendarPolicy, Guid> repository,
	WorkCalendarHolidaySnapshotService holidaySnapshotService,
	IHrPolicySafetyAppService policySafetyAppService,
	HrPolicyImpactConfirmationService confirmationService,
	HrPolicyVersionAppService versionAppService,
	HrAuditWriter auditWriter,
	WorkCalendarPolicyReader reader)
	: ApplicationService, ITransientDependency
{
	public async Task<WorkCalendarPolicyDto> CreateAsync(CreateUpdateWorkCalendarPolicyDto input)
	{
		var status = HrPolicyMapping.ParsePolicyStatus(input.Status);
		HrPolicyStatusGuard.EnsureArchiveUsesArchiveWorkflow(status);
		await holidaySnapshotService.ValidateHolidaysAsync(input.AcademicYearId, input.Holidays);

		var entity = new WorkCalendarPolicy(
			GuidGenerator.Create(),
			CurrentTenant.Id,
			input.AcademicYearId,
			input.Name,
			status,
			input.EffectiveFrom,
			input.EffectiveTo,
			HrPolicyMapping.ParseWeeklyOffPattern(input.WeeklyOffPattern),
			input.CustomWeeklyOffs,
			HrPolicyMapping.ParseHolidaySyncMode(input.HolidaySyncMode));

		await repository.InsertAsync(entity, autoSave: true);
		await holidaySnapshotService.ReplaceHolidaysAsync(entity.Id, input.AcademicYearId, input.Holidays);
		var dto = await BuildDtoAsync(entity);
		await versionAppService.CaptureAsync("work_calendar", dto, "created");
		await auditWriter.WriteAsync(dto.AcademicYearId, "work_calendar_policy", dto.Id, dto.Name, "created", $"Created work calendar policy {dto.Name}");
		return dto;
	}

	public async Task<WorkCalendarPolicyDto> UpdateAsync(Guid id, CreateUpdateWorkCalendarPolicyDto input)
	{
		var entity = await repository.GetAsync(id);
		HrPolicyStatusGuard.EnsureEditable(entity.Status);
		EnsureAcademicYearUnchanged(entity.AcademicYearId, input.AcademicYearId);
		var before = HrPolicyMapping.ToDto(entity);
		var status = HrPolicyMapping.ParsePolicyStatus(input.Status);
		HrPolicyStatusGuard.EnsureArchiveUsesArchiveWorkflow(status);
		var weeklyOffPattern = HrPolicyMapping.ParseWeeklyOffPattern(input.WeeklyOffPattern);
		var holidaySyncMode = HrPolicyMapping.ParseHolidaySyncMode(input.HolidaySyncMode);
		var existingHolidays = await reader.GetHolidaysAsync(entity.Id);
		if (WorkCalendarPolicyChangeDetector.IsUnchanged(entity, input, existingHolidays, status, weeklyOffPattern, holidaySyncMode))
		{
			before.Holidays = existingHolidays;
			before.HolidaySummary = HrPolicyMapping.BuildHolidaySummary(existingHolidays);
			return before;
		}

		await holidaySnapshotService.ValidateHolidaysAsync(input.AcademicYearId, input.Holidays);
		if (entity.Status != HrPolicyStatus.Draft || status != HrPolicyStatus.Draft)
		{
			var preview = await policySafetyAppService.PreviewPolicyImpactAsync(new HrPolicyImpactPreviewRequestDto
			{
				PolicyType = "work_calendar",
				PolicyId = id,
				AcademicYearId = entity.AcademicYearId,
				Action = "update",
				WorkCalendarPolicy = input
			});
			await confirmationService.EnsureConfirmedAsync(preview);
		}
		entity.Update(
			input.Name,
			status,
			input.EffectiveFrom,
			input.EffectiveTo,
			weeklyOffPattern,
			input.CustomWeeklyOffs,
			holidaySyncMode);

		await repository.UpdateAsync(entity, autoSave: true);
		await holidaySnapshotService.ReplaceHolidaysAsync(entity.Id, input.AcademicYearId, input.Holidays);
		var dto = await BuildDtoAsync(entity);
		await versionAppService.CaptureAsync("work_calendar", dto, "edited");
		await auditWriter.WriteAsync(dto.AcademicYearId, "work_calendar_policy", dto.Id, dto.Name, "edited", $"Edited work calendar policy {dto.Name}", changedFields: HrAuditWriter.Diff(before, dto, "lastModificationTime", "lastModifierId"));
		return dto;
	}

	public async Task<WorkCalendarPolicyDto> CloneAsync(Guid id, HrPolicyCloneRequestDto input)
	{
		var source = await repository.GetAsync(id);
		var entity = new WorkCalendarPolicy(
			GuidGenerator.Create(),
			CurrentTenant.Id,
			source.AcademicYearId,
			string.IsNullOrWhiteSpace(input.Name) ? $"{source.Name} Copy" : input.Name!,
			HrPolicyStatus.Draft,
			input.EffectiveFrom?.Date ?? source.EffectiveFrom,
			source.EffectiveTo,
			source.WeeklyOffPattern,
			source.GetCustomWeeklyOffs(),
			source.HolidaySyncMode);

		await repository.InsertAsync(entity, autoSave: true);
		var sourceHolidays = await reader.GetHolidaysAsync(source.Id);
		await holidaySnapshotService.ReplaceHolidaysAsync(entity.Id, source.AcademicYearId, sourceHolidays);
		var dto = await BuildDtoAsync(entity);
		await versionAppService.CaptureAsync("work_calendar", dto, "cloned");
		await auditWriter.WriteAsync(dto.AcademicYearId, "work_calendar_policy", dto.Id, dto.Name, "cloned", $"Cloned work calendar policy from {source.Name}", context: new { sourcePolicyId = source.Id });
		return dto;
	}

	public async Task DeleteAsync(Guid id)
	{
		var entity = await repository.GetAsync(id);
		if (entity.Status == HrPolicyStatus.Archived)
		{
			return;
		}

		var preview = await policySafetyAppService.PreviewPolicyImpactAsync(new HrPolicyImpactPreviewRequestDto
		{
			PolicyType = "work_calendar",
			PolicyId = id,
			AcademicYearId = entity.AcademicYearId,
			Action = "archive"
		});
		await confirmationService.EnsureConfirmedAsync(preview);
		entity.Archive();
		await repository.UpdateAsync(entity, autoSave: true);
		var dto = HrPolicyMapping.ToDto(entity);
		await versionAppService.CaptureAsync("work_calendar", dto, "archived");
		await auditWriter.WriteAsync(dto.AcademicYearId, "work_calendar_policy", dto.Id, dto.Name, "archived", $"Archived work calendar policy {dto.Name}");
	}

	public async Task<WorkCalendarPolicyDto> UnarchiveAsync(Guid id)
	{
		var entity = await repository.GetAsync(id);
		if (entity.Status != HrPolicyStatus.Archived)
		{
			return await BuildDtoAsync(entity);
		}
		entity.Unarchive();
		await repository.UpdateAsync(entity, autoSave: true);
		var dto = await BuildDtoAsync(entity);
		await versionAppService.CaptureAsync("work_calendar", dto, "unarchived");
		await auditWriter.WriteAsync(dto.AcademicYearId, "work_calendar_policy", dto.Id, dto.Name, "unarchived", $"Unarchived work calendar policy {dto.Name} as draft.");
		return dto;
	}

	private async Task<WorkCalendarPolicyDto> BuildDtoAsync(WorkCalendarPolicy entity)
	{
		var dto = HrPolicyMapping.ToDto(entity);
		dto.Holidays = await reader.GetHolidaysAsync(entity.Id);
		dto.HolidaySummary = HrPolicyMapping.BuildHolidaySummary(dto.Holidays);
		return dto;
	}

	private static void EnsureAcademicYearUnchanged(Guid entityAcademicYearId, Guid inputAcademicYearId)
	{
		if (entityAcademicYearId != inputAcademicYearId)
		{
			throw new BusinessException(HRErrorCodes.HrPolicyAssignmentAcademicYearMismatch);
		}
	}

}
