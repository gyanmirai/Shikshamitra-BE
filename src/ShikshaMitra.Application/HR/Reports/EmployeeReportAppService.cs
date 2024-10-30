using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using ShikshaMitra.Common;
using ShikshaMitra.Common.PDF;
using ShikshaMitra.HR.Employees;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.HR.Reports;

public class EmployeeReportAppService(IEmployeeRepository employeeRepository, IPdfService pdfService)
	: ApplicationService
{
	public async Task<IEnumerable<EmployeeDto>> GetEmployeeReport(EmployeeReportFilterDto input)
	{
		// Using GetQueryable() method to retrieve the queryable collection
		IQueryable<Employee> query = (await employeeRepository.GetQueryableAsync())
			.WhereIf(input.Gender != null, e => e.Gender == input.Gender);


		// Execute the query asynchronously and return the results
		var employeeList = await AsyncExecuter.ToListAsync(query);

		return ObjectMapper.Map<List<Employee>, List<EmployeeDto>>(employeeList);
	}

	public async Task<IEnumerable<EmployeeContactDto>> GetEmployeeContactReport(EmployeeReportFilterDto input)
	{
		// Using GetQueryable() method to retrieve the queryable collection
		IQueryable<Employee> query = (await employeeRepository.GetQueryableAsync())
			.WhereIf(input.Gender != null, e => e.Gender == input.Gender);

		// select only the required properties
		var employeeContactQuery = query.Select(e => new EmployeeContactDto
		{
			Code = e.Code,
			EmployeeName = e.EmployeeName,
			MobileNo = e.MobileNo,
			Email = e.Email,
			DateOfBirth = e.DateOfBirth,
		});

		// Execute the query asynchronously and return the results
		var employeeContactList = await AsyncExecuter.ToListAsync(employeeContactQuery);

		return employeeContactList;
	}

	public async Task<byte[]> GetExportEmployeeReportToPdf(EmployeeReportFilterDto input,
		ExportOptionType exportOptionType)
	{
		IEnumerable<EmployeeContactDto> employeeReport = await GetEmployeeContactReport(input);

		HRPdfService hrPdfService = new HRPdfService(pdfService);

		return hrPdfService.GenerateEmployeeContactReport(employeeReport);
	}
}