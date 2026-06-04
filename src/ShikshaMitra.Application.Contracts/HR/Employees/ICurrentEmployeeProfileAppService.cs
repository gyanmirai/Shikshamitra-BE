using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.HR.Employees;

public interface ICurrentEmployeeProfileAppService : IApplicationService
{
	Task<EmployeeDto> GetCurrentAsync();
}
