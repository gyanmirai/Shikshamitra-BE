using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.HR.WorkShifts;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.MultiTenancy;
namespace ShikshaMitra.EntityFrameworkCore.HR.WorkShifts;
public class EmployeeShiftWriteFence(IDbContextProvider<ShikshaMitraDbContext> contexts, ICurrentTenant tenant) : IEmployeeShiftWriteFence, ITransientDependency
{
	public async Task LockAsync(Guid employeeId)
	{
		var db = await contexts.GetDbContextAsync();
		if (db.Database.IsNpgsql())
			await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"AppEmployees\" WHERE \"Id\"={employeeId} AND \"TenantId\"={tenant.Id} FOR UPDATE");
	}
}