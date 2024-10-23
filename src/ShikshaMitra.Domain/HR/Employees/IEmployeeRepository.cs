using System;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.HR.Employees;

public interface IEmployeeRepository : IRepository<Employee, Guid>
{
	public Task<Employee?> GetEmployeeByUserIdAsync(Guid userId);
}