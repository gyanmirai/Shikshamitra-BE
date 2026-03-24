using System.Collections.Generic;
using System.Threading.Tasks;
using ShikshaMitra.Common;
using ShikshaMitra.HR.Employees;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.HR.Reports;

public interface IEmployeeReportAppService : IApplicationService
{
    Task<IEnumerable<EmployeeDto>> GetEmployeeReport(EmployeeReportFilterDto input);
    Task<IEnumerable<EmployeeContactDto>> GetEmployeeContactReport(EmployeeReportFilterDto input);
    Task<byte[]> GetExportEmployeeReport(EmployeeReportFilterDto input, ExportOptionType exportOptionType);
}
