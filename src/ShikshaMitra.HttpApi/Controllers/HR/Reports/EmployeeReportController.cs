using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Common;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Reports;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.HR.Reports;

/// <summary>
/// API Controller for employee reports and exports.
/// </summary>
[Area("app")]
[RemoteService(Name = "Default")]
[Route("api/hr/reports/employees")]
[Authorize]
public class EmployeeReportController : AbpControllerBase
{
    private readonly IEmployeeReportAppService _employeeReportAppService;

    public EmployeeReportController(IEmployeeReportAppService employeeReportAppService)
    {
        _employeeReportAppService = employeeReportAppService;
    }

    /// <summary>
    /// Get employee report with optional gender filter.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<EmployeeDto>), StatusCodes.Status200OK)]
    public virtual async Task<IEnumerable<EmployeeDto>> GetEmployeeReportAsync([FromQuery] EmployeeReportFilterDto input)
    {
        return await _employeeReportAppService.GetEmployeeReport(input);
    }

    /// <summary>
    /// Get employee contact report with optional gender filter.
    /// </summary>
    [HttpGet("contacts")]
    [ProducesResponseType(typeof(IEnumerable<EmployeeContactDto>), StatusCodes.Status200OK)]
    public virtual async Task<IEnumerable<EmployeeContactDto>> GetEmployeeContactReportAsync([FromQuery] EmployeeReportFilterDto input)
    {
        return await _employeeReportAppService.GetEmployeeContactReport(input);
    }

    /// <summary>
    /// Export employee report as PDF or Excel.
    /// </summary>
    [HttpGet("export")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public virtual async Task<IActionResult> ExportAsync(
        [FromQuery] EmployeeReportFilterDto input,
        [FromQuery] ExportOptionType exportOptionType = ExportOptionType.PDF)
    {
        var fileBytes = await _employeeReportAppService.GetExportEmployeeReport(input, exportOptionType);

        var contentType = exportOptionType == ExportOptionType.PDF
            ? "application/pdf"
            : "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

        var extension = exportOptionType == ExportOptionType.PDF ? "pdf" : "xlsx";

        return File(fileBytes, contentType, $"employee-report.{extension}");
    }
}
