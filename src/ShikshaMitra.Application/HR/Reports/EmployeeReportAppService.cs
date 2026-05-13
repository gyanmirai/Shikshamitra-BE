using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Common;
using ShikshaMitra.Common.Excel;
using ShikshaMitra.Common.PDF;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.HR.AttendanceAndLeaves;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Security.UserScope;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.HR.Reports;

[RemoteService(false)]
[Authorize(ShikshaMitraSubmodulePermissions.HR.Reports.View)]
public class EmployeeReportAppService(
	IEmployeeRepository employeeRepository,
	IRepository<EmployeeAttendance, Guid> employeeAttendanceRepository,
	IUserScopeService userScopeService,
	HrUserScopeAccessService hrScopeAccessService,
	HRPdfService hrPdfService,
	HRExcelService hrExcelService)
	: ApplicationService, IEmployeeReportAppService
{
	public async Task<IEnumerable<EmployeeDto>> GetEmployeeReport(EmployeeReportFilterDto input)
	{
		var query = await GetScopedEmployeeQueryAsync(input.GenderType);
		var employeeList = await AsyncExecuter.ToListAsync(query);
		return ObjectMapper.Map<List<Employee>, List<EmployeeDto>>(employeeList);
	}

	public async Task<IEnumerable<EmployeeContactDto>> GetEmployeeContactReport(EmployeeReportFilterDto input)
	{
		var query = await GetScopedEmployeeQueryAsync(input.GenderType);

		// select only the required properties
		var employeeContactQuery = query.Select(e => new EmployeeContactDto
		{
			EmployeeCode = e.EmployeeCode,
			EmployeeName = e.EmployeeName,
			MobileNo = e.MobileNo,
			Email = e.Email,
			DateOfBirth = e.DateOfBirth,
		});

		// Execute the query asynchronously and return the results
		var employeeContactList = await AsyncExecuter.ToListAsync(employeeContactQuery);

		return employeeContactList;
	}

	public async Task<byte[]> GetExportEmployeeReport(EmployeeReportFilterDto input,
		ExportOptionType exportOptionType)
	{
		var employeeReport = await GetEmployeeContactReport(input);

		return exportOptionType == ExportOptionType.PDF
			? await hrPdfService.GenerateEmployeeContactReport(employeeReport)
			: hrExcelService.GenerateEmployeeContactExcel(employeeReport);
	}

	private async Task<IQueryable<Employee>> GetScopedEmployeeQueryAsync(GenderType? genderType)
	{
		var scope = await EnsureCanReadScopedEmployeesAsync();

		IQueryable<Employee> query = await employeeRepository.GetQueryableAsync();

		if (CurrentTenant.Id.HasValue)
		{
			query = query.Where(e => e.TenantId == CurrentTenant.Id);
		}

		query = query.WhereIf(genderType != null, e => e.GenderType == genderType);

		if (HrUserScopeAccess.HasHrWideAccess(scope))
		{
			return query;
		}

		var attendanceQuery = await employeeAttendanceRepository.GetQueryableAsync();
		if (CurrentTenant.Id.HasValue)
		{
			attendanceQuery = attendanceQuery.Where(a => a.TenantId == CurrentTenant.Id);
		}

		var employeeStageRows = await AsyncExecuter.ToListAsync(
			attendanceQuery
				.Where(a => a.EducationMediumStageId.HasValue)
				.Select(a => new { a.EmployeeId, a.EducationMediumStageId }));
		var employeeStageMap = employeeStageRows
			.GroupBy(x => x.EmployeeId)
			.ToDictionary(
				x => x.Key,
				x => (IReadOnlyCollection<Guid>)x
					.Select(row => row.EducationMediumStageId!.Value)
					.Distinct()
					.ToList());
		var scopedStageEmployeeIds = await hrScopeAccessService.FilterEmployeeIdsByStagesAsync(
			employeeStageMap.Keys.ToList(),
			employeeStageMap,
			scope,
			includeUnknownStageEmployees: false);

		// Deny unknown-stage employees for scoped users to avoid cross-scope exposure.
		return query.Where(e => scopedStageEmployeeIds.Contains(e.Id));
	}

	private async Task<UserScopeContext> EnsureCanReadScopedEmployeesAsync()
	{
		var uid = CurrentUser.Id;
		if (!uid.HasValue)
		{
			throw new BusinessException(HRErrorCodes.AttendanceImportPreviewInvalid);
		}

		var scope = await userScopeService.GetUserScopeAsync(uid.Value);
		if (HrUserScopeAccess.HasHrWideAccess(scope))
		{
			return scope;
		}

		if (!HrUserScopeAccess.HasAnyConcreteHrStageAccess(scope))
		{
			throw new BusinessException(HRErrorCodes.TakeAttendanceMediumStageOutOfScope);
		}

		return scope;
	}
}
