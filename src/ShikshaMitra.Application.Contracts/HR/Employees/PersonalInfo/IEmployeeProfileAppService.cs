using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.HR.Employees.PersonalInfo;

public interface IEmployeeProfileAppService : IApplicationService
{
	Task<EmployeeDetails> GetAsync(Guid employeeId);
}
