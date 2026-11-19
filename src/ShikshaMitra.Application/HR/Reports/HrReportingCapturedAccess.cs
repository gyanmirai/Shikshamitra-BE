using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.HR.ProfessionalManagement;
using ShikshaMitra.Security.UserScope;
using Volo.Abp.Authorization;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;

namespace ShikshaMitra.HR.Reports;

public class HrReportingCapturedAccess(HrReportingAccess access, HrReportExportScopeAuthorizationService scopeAuthorization,
	HrReportingDataReader reader, IUserScopeService scopes, IAsyncQueryableExecuter executor,
	ProfessionalManagementReadScopeService reviewScope, IRepository<PerformanceReviewAssignment, Guid> assignments) : ITransientDependency
{
	public async Task EnsureAsync(HrReportRun run)
	{
		await access.EnsureDesignAsync(JsonSerializer.Deserialize<HrReportDesignDto>(run.DefinitionJson, HrReportingEngine.Json)!, run.OwnerUserId);
		await scopeAuthorization.EnsureRecipientCoversSnapshotAsync(JsonSerializer.Deserialize<HrReportExportScopeSnapshot>(run.ScopeJson)!, run.OwnerUserId);
		PayrollIssuedReportBoundary.EnsureLegacyPayslipRequest(new() { ReportType = run.Name, DataSource = run.DataSource });
		var parameters = JsonSerializer.Deserialize<HrReportParametersDto>(run.ParametersJson, HrReportingEngine.Json)!;
		PayrollIssuedReportBoundary.EnsureLegacyCapture(run.DataSource, parameters.IncludeDrafts, PayrollIssuedReportBoundary.IsIssuedOnly(run));
		var captured = JsonSerializer.Deserialize<HrReportingResult>(run.ResultJson, HrReportingEngine.Json)!;
		PayrollIssuedReportBoundary.EnsureCaptured(run.DataSource, captured);
		if (captured.EmployeeIds.Count > 0)
		{
			var ids = captured.EmployeeIds.ToArray();
			var population = await reader.EmployeesAsync(run.DataSource, new HrReportParametersDto(), await scopes.GetUserScopeAsync(run.OwnerUserId));
			if (await executor.CountAsync(population.Where(x => Enumerable.Contains(ids, x.Id))) != ids.Length) throw new AbpAuthorizationException("Current staff access does not cover this captured report.");
		}
		if (captured.AssignmentIds.Count > 0)
		{
			var ids = captured.AssignmentIds.ToArray();
			var query = await reviewScope.ApplyAsync(await assignments.GetQueryableAsync());
			if (await executor.CountAsync(query.Where(x => Enumerable.Contains(ids, x.Id))) != ids.Length) throw new AbpAuthorizationException("Current review access does not cover this captured report.");
		}
	}
}
