using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.HR.Employees;
using System;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace ShikshaMitra.Employees;

public class EfCoreEmployeeRepository(IDbContextProvider<ShikshaMitraDbContext> dbContextProvider)
	: EfCoreRepository<ShikshaMitraDbContext, Employee, Guid>(dbContextProvider), IEmployeeRepository
{
}