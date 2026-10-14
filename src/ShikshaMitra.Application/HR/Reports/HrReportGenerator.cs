using ShikshaMitra.Common;
using ShikshaMitra.Security.UserScope;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Timing;

namespace ShikshaMitra.HR.Reports;

public class HrReportGenerator(
	IUserScopeService userScopeService,
	HrUserScopeAccessService hrScopeAccessService,
	HrReportEmployeeQueryService employeeQueryService,
	HrReportRowSourceReader rowSourceReader,
	HrReportRequestValidationService requestValidationService,
	ICurrentTenant currentTenant,
	IAsyncQueryableExecuter asyncExecuter,
	IClock clock, Volo.Abp.Domain.Repositories.IRepository<ShikshaMitra.HR.Payroll.SalarySlips.PayslipSettings, Guid> payslipSettings, ShikshaMitra.HR.Payroll.SalarySlips.PayslipLogoService logos) : ITransientDependency
{
	public async Task<HrReportExportScopeSnapshot?> ResolveExportScopeAsync(
		Guid userId,
		Guid? tenantId)
	{
		using (currentTenant.Change(tenantId))
		{
			var scope = await userScopeService.GetUserScopeAsync(userId);
			if (scope.TenantId != tenantId)
			{
				return null;
			}

			if (HrUserScopeAccess.HasHrWideAccess(scope))
			{
				return new HrReportExportScopeSnapshot
				{
					TenantId = tenantId,
					HasTenantWideAccess = true
				};
			}

			if (!HrUserScopeAccess.HasAnyConcreteHrStageAccess(scope))
			{
				return new HrReportExportScopeSnapshot
				{
					TenantId = tenantId
				};
			}

			var mediumStageIds = await hrScopeAccessService.ResolveAccessibleStageIdsAsync(scope);
			return new HrReportExportScopeSnapshot
			{
				TenantId = tenantId,
				MediumStageIds = mediumStageIds.Distinct().OrderBy(x => x).ToList()
			};
		}
	}

	public Task<HrReportSourcesDto> GetSourcesAsync()
	{
		return Task.FromResult(new HrReportSourcesDto
		{
			Sources = HrReportCatalog.BuildSources(),
			Operators = HrReportCatalog.BuildOperators(),
			Formats = HrReportCatalog.BuildFormats()
		});
	}

	public async Task<HrReportPreviewDto> GenerateAsync(
		HrReportRequestDto input,
		Guid userId,
		Guid? tenantId,
		bool exportAllRows)
	{
		requestValidationService.Validate(input);
		HrReportRequestProcessor.Normalize(input, exportAllRows);
		using (currentTenant.Change(tenantId))
		{
			var scope = await userScopeService.GetUserScopeAsync(userId);
			return await GenerateForScopeAsync(input, scope, exportAllRows);
		}
	}

	public async Task<HrReportPreviewDto> GenerateExportAsync(
		HrReportRequestDto input,
		Guid userId,
		Guid? tenantId,
		HrReportExportScopeSnapshot scopeSnapshot)
	{
		if (scopeSnapshot.TenantId != tenantId)
		{
			throw new InvalidOperationException("The HR report export scope tenant does not match the job tenant.");
		}

		requestValidationService.Validate(input);
		HrReportRequestProcessor.Normalize(input, exportAllRows: true);
		using (currentTenant.Change(tenantId))
		{
			var scope = new UserScopeContext
			{
				UserId = userId,
				TenantId = tenantId,
				IsAdmin = scopeSnapshot.HasTenantWideAccess,
				HasWildcardAcademicYear = scopeSnapshot.HasTenantWideAccess,
				MediumStageIds = [.. scopeSnapshot.MediumStageIds],
				CalculatedAt = clock.Now
			};
			return await GenerateForScopeAsync(input, scope, exportAllRows: true);
		}
	}

	public async Task<HrReportOptionsDto> GetOptionsAsync(Guid userId, Guid? tenantId)
	{
		using (currentTenant.Change(tenantId))
		{
			var scope = await userScopeService.GetUserScopeAsync(userId);
			var employeeQuery = await employeeQueryService.BuildScopedEmployeeQueryAsync(scope);
			var departments = await asyncExecuter.ToListAsync(
				employeeQuery
					.Where(e => e.Department != null && e.Department != string.Empty)
					.Select(e => e.Department!)
					.Distinct()
					.OrderBy(x => x));

			return new HrReportOptionsDto
			{
				ReportTypes = (await GetSourcesAsync()).Sources.Select(x => new HrReportOptionDto { Id = x.Id, Label = x.Label }).ToList(),
				Departments = departments.Select(x => new HrReportOptionDto { Id = x, Label = x }).ToList(),
				Statuses = Enum.GetValues<EmployeeStatusType>().Select(x => new HrReportOptionDto { Id = ((int) x).ToString(), Label = x.ToString() }).ToList()
			};
		}
	}

	private static string ResolveSource(HrReportRequestDto input)
	{
		if (!string.IsNullOrWhiteSpace(input.DataSource))
		{
			return HrReportCatalog.GetSource(input.DataSource).Id;
		}

		return input.ReportType switch
		{
			HrReportTypes.SalaryRegister or HrReportTypes.EmployeePayslip or HrReportTypes.MonthlyPayrollSummary
				or HrReportTypes.AllowanceReport or HrReportTypes.DeductionReport or HrReportTypes.PayrollReconciliationReport
				or HrReportTypes.LeaveEncashmentReport or HrReportTypes.FinalSettlementReport => HrReportDataSources.Payroll,
			_ => HrReportDataSources.Employee
		};
	}

	private async Task<HrReportPreviewDto> GenerateForScopeAsync(
		HrReportRequestDto input,
		UserScopeContext scope,
		bool exportAllRows)
	{
		var source = ResolveSource(input);
		var rowResult = await rowSourceReader.BuildRowsResultAsync(source, input, scope, exportAllRows);
		var rows = rowResult.IsDatabaseBacked
			? rowResult.Rows
			: HrReportRequestProcessor.ShapeRows(rowResult.Rows, input);

		var totalCount = rowResult.IsDatabaseBacked ? rowResult.TotalCount : rows.Count;
		var columns = HrReportCatalog.ResolveColumns(source, input, rows);
		var pageRows = exportAllRows
			? rows.Take(HrReportRequestProcessor.MaxExportRows).ToList()
			: rowResult.IsDatabaseBacked
				? rows
				: rows.Skip((input.Page - 1) * input.PageSize).Take(input.PageSize).ToList();
		var warnings = HrReportPreviewPresentation.BuildWarnings(
			source,
			input,
			exportAllRows,
			totalCount,
			rowResult.IsDatabaseBacked);

		warnings.AddRange(rowResult.Warnings);
		var salaryDocument = source is HrReportDataSources.PayrollStatement or HrReportDataSources.PayrollRegister;
		var settings = salaryDocument ? await payslipSettings.FindAsync(x => x.TenantId == currentTenant.Id) : null;
		var options = settings?.GetPresentation();
		var logo = options?.LogoBlobName == null ? null : await logos.GetAsync(options.LogoBlobName);
		return new HrReportPreviewDto
		{
			Branding = settings == null ? null : new(settings.PresentationVersionId, settings.SchoolName, settings.SchoolAddress, options!,
				logo == null ? null : Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(logo))),
			LogoBytes = logo,
			TotalsLabel = exportAllRows ? "Totals for exported rows" : "Totals for all available payroll in the selected period",
			Totals = salaryDocument ? exportAllRows ? PayrollSalaryDocumentTotals.Sum(pageRows) : rowResult.Totals ?? PayrollSalaryDocumentTotals.Sum(pageRows) : new(),
			DataSource = source,
			ReportType = string.IsNullOrWhiteSpace(input.ReportType) ? HrReportCatalog.GetSource(source).Label : input.ReportType,
			TotalCount = totalCount,
			Page = input.Page,
			PageSize = input.PageSize,
			Columns = columns,
			Rows = pageRows.Select(row => columns.ToDictionary(column => column.Id, column => row.GetValueOrDefault(column.Id))).ToList(),
			NormalizedFilters = input.Filters,
			QueryWarnings = warnings,
			Chart = HrReportPreviewPresentation.BuildChartPreview(input, rows)
		};
	}

}
