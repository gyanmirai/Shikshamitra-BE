using System;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.HR.Employees;

public interface IEmployeeRepository : IRepository<Employee, Guid>;