using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace ShikshaMitra.HR.Reports;

public class HrReportingRunCancellation(IServiceScopeFactory services) : ITransientDependency
{
	public async Task EnsureActiveAsync(HrReportRun run)
	{
		// A separate read must observe cancellation outside the materialization snapshot.
		using var scope = services.CreateScope();
		using var tenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(run.TenantId);
		using var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: false);
		var current = await scope.ServiceProvider.GetRequiredService<IRepository<HrReportRun, Guid>>().FindAsync(run.Id);
		if (current == null || current.Status != "Running" || current.AttemptId != run.AttemptId) throw new BusinessException(HRErrorCodes.ReportRunTransitionInvalid);
		await uow.CompleteAsync();
	}
}