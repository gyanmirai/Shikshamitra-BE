using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Common;
using ShikshaMitra.Common.Excel;
using ShikshaMitra.Common.PDF;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.HR.Employees;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.HR.Reports;

[RemoteService(false)]
[Authorize(ShikshaMitraSubmodulePermissions.HR.Reports.View)]
public class EmployeeReportAppService(
	IEmployeeRepository employeeRepository,
	HRPdfService hrPdfService,
	HRExcelService hrExcelService)
	: ApplicationService, IEmployeeReportAppService
{
	public async Task<IEnumerable<EmployeeDto>> GetEmployeeReport(EmployeeReportFilterDto input)
	{
		// Using GetQueryable() method to retrieve the queryable collection
		IQueryable<Employee> query = (await employeeRepository.GetQueryableAsync())
			.WhereIf(input.GenderType != null, e => e.GenderType == input.GenderType);


		// Execute the query asynchronously and return the results
		var employeeList = await AsyncExecuter.ToListAsync(query);

		return ObjectMapper.Map<List<Employee>, List<EmployeeDto>>(employeeList);
	}

	public async Task<IEnumerable<EmployeeContactDto>> GetEmployeeContactReport(EmployeeReportFilterDto input)
	{
		// Using GetQueryable() method to retrieve the queryable collection
		IQueryable<Employee> query = (await employeeRepository.GetQueryableAsync())
			.WhereIf(input.GenderType != null, e => e.GenderType == input.GenderType);

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
}