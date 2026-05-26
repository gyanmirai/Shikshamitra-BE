using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.HR.Employees;

public interface ICurrentEmployeeAppService : IApplicationService
{
	Task<EmployeeDto> GetCurrentAsync();
}
