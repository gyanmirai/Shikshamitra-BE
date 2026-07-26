using System;
using System.Threading.Tasks;
using ShikshaMitra.HR.Audit;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.HR.Policies;

public class PayrollPolicyCloneService(
	IRepository<PayrollPolicy, Guid> repository,
	HrPolicyWorkCalendarValidator workCalendarValidator,
	HrPolicyVersionAppService versionAppService,
	HrAuditWriter auditWriter,
	HrPolicyNameUniquenessService nameUniquenessService)
	: ApplicationService, ITransientDependency
{
	public async Task<PayrollPolicyDto> CloneAsync(
		Guid id,
		HrPolicyCloneRequestDto input)
	{
		var source = await repository.GetAsync(id);
		await workCalendarValidator.EnsureUsableAsync(
			source.WorkCalendarPolicyId,
			source.AcademicYearId,
			HrPolicyStatus.Draft);
		var cloneName = string.IsNullOrWhiteSpace(input.Name)
			? $"{source.Name} Copy"
			: input.Name!;
		await nameUniquenessService.EnsurePayrollAvailableAsync(
			source.AcademicYearId,
			cloneName);
		var entity = new PayrollPolicy(
			GuidGenerator.Create(),
			CurrentTenant.Id,
			source.AcademicYearId,
			source.WorkCalendarPolicyId,
			cloneName,
			HrPolicyStatus.Draft,
			false,
			source.DayCalculationType,
			source.ManualDays,
			source.GetManualMonthDays(),
			source.SaturdayWorkPolicy,
			source.GetHolidayOverrides(),
			source.GetLockedMonthSettings());

		await nameUniquenessService.ExecutePayrollWriteAsync(
			entity.Name,
			() => repository.InsertAsync(entity, autoSave: true));
		var dto = HrPolicyMapping.ToDto(entity);
		await versionAppService.CaptureAsync("payroll", dto, "cloned");
		await auditWriter.WriteAsync(
			dto.AcademicYearId,
			"payroll_policy",
			dto.Id,
			dto.Name,
			"cloned",
			$"Cloned payroll policy from {source.Name}",
			context: new { sourcePolicyId = source.Id });
		return dto;
	}
}
