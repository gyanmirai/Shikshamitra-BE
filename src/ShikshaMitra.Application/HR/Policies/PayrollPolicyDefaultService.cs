using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.HR.Audit;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.HR.Policies;

[RemoteService(false)]
public class PayrollPolicyDefaultService(
	IRepository<PayrollPolicy, Guid> repository,
	HrPolicyVersionAppService versionAppService,
	HrAuditWriter auditWriter) : ApplicationService, ITransientDependency
{
	public async Task ClearDefaultsAsync(Guid academicYearId, Guid? excludeId = null)
	{
		var query = await repository.GetQueryableAsync();
		var defaults = await AsyncExecuter.ToListAsync(query.Where(x =>
			x.AcademicYearId == academicYearId &&
			x.IsDefault &&
			(!excludeId.HasValue || x.Id != excludeId.Value)));

		foreach (var item in defaults)
		{
			item.SetDefault(false);
			await repository.UpdateAsync(item, autoSave: false);
			var dto = HrPolicyMapping.ToDto(item);
			await versionAppService.CaptureAsync("payroll", dto, "set-default-cleared");
			await auditWriter.WriteAsync(dto.AcademicYearId, "payroll_policy", dto.Id, dto.Name, "set_default", $"Cleared default payroll policy {dto.Name}");
		}
	}

	public async Task EnsureDefaultRemainsAsync(
		PayrollPolicy entity,
		bool isDefaultAfterUpdate,
		HrPolicyStatus statusAfterUpdate)
	{
		if (!entity.IsDefault || (isDefaultAfterUpdate && statusAfterUpdate == HrPolicyStatus.Active))
		{
			return;
		}

		var query = await repository.GetQueryableAsync();
		var hasOtherActiveDefault = await AsyncExecuter.AnyAsync(query.Where(x =>
			x.AcademicYearId == entity.AcademicYearId &&
			x.Id != entity.Id &&
			x.IsDefault &&
			x.Status == HrPolicyStatus.Active));

		if (!hasOtherActiveDefault)
		{
			throw new BusinessException(HRErrorCodes.DefaultPayrollPolicyRequired);
		}
	}
}
