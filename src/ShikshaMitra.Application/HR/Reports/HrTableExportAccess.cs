using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.HR.AttendanceAndLeaves;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Employees.Search;
using Volo.Abp.Authorization;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Linq;

namespace ShikshaMitra.HR.Reports;

public class HrTableExportAccess(HrTableExportSources sources, HrReportExportScopeAuthorizationService scope,
	HrReportingDataReader reader, IUserScopeService scopes, IAsyncQueryableExecuter executor, LeaveActorAccessService leaveActors,
	EmployeeSearchVisibilityService directory, IEmployeeRepository staff, IEmployeeAuthorizationService employeeAccess) : ITransientDependency
{
	public async Task EnsureAsync(HrReportRun run)
	{
		var request = HrTableExportAppService.Request(run);
		HrTableExportSources.ValidateForExport(request);
		await sources.EnsureAccessAsync(request);
		await scope.EnsureRecipientCoversSnapshotAsync(JsonSerializer.Deserialize<HrReportExportScopeSnapshot>(run.ScopeJson)!, run.OwnerUserId);
		if (run.Status != "Completed") return;
		var captured = JsonSerializer.Deserialize<HrReportingResult>(run.ResultJson, HrReportingEngine.Json)!;
		if (request.Table == "hr-salary-documents")
		{
			var filters = request.Query.TryGetProperty("filters", out var nested) ? nested : request.Query;
			var includesDrafts = filters.TryGetProperty("includeDrafts", out var drafts) && bool.TryParse(drafts.ToString(), out var included) && included;
			PayrollIssuedReportBoundary.EnsureLegacyCapture(request.Query.GetProperty("dataSource").GetString()!, includesDrafts, PayrollIssuedReportBoundary.IsIssuedOnly(run));
			PayrollIssuedReportBoundary.EnsureCaptured(request.Query.GetProperty("dataSource").GetString()!, captured);
		}
		if (captured.EmployeeIds.Count == 0 && captured.EmployeeCodes.Count == 0) return;
		var ids = captured.EmployeeIds.ToArray();
		if (request.Table == "employee-table")
		{
			var authorized = await directory.ApplyVisibilityScopeAsync(await staff.GetQueryableAsync());
			if (await executor.CountAsync(authorized.Where(x => ids.Contains(x.Id))) != ids.Length) throw new AbpAuthorizationException();
			return;
		}
		if (request.Table == "hr-self-leave-history")
		{
			var actorEmployee = await leaveActors.ResolveCurrentActorEmployeeIdStrictAsync();
			if (ids.Any(x => x != actorEmployee)) throw new AbpAuthorizationException();
			return;
		}
		if (request.Table == "hr-employee-documents")
		{
			foreach (var id in ids) await employeeAccess.CheckEmployeeAccessAsync(id);
			return;
		}
		var dataset = request.Table == "hr-leave-requests" ? "leave" : request.Table == "hr-attendance-register" ? "attendance" : "employee";
		var employees = await reader.EmployeesAsync(dataset, new(), await scopes.GetUserScopeAsync(run.OwnerUserId));
		if (ids.Length > 0 && await executor.CountAsync(employees.Where(x => Enumerable.Contains(ids, x.Id))) != ids.Length) throw new AbpAuthorizationException("Current staff access does not cover this table export.");
		var codes = captured.EmployeeCodes.ToArray();
		if (codes.Length > 0 && await executor.CountAsync(employees.Where(x => x.EmployeeCode != null && Enumerable.Contains(codes, x.EmployeeCode))) != codes.Length) throw new AbpAuthorizationException("Current staff access does not cover this table export.");
	}
}
