using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.EntityFrameworkCore;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace ShikshaMitra.HR.Employees;

/// <summary>
/// Repository implementation for EmployeeFamilyMember entity
/// </summary>
public class EmployeeFamilyMemberRepository : EfCoreRepository<ShikshaMitraDbContext, EmployeeFamilyMember, Guid>, IEmployeeFamilyMemberRepository
{
	public EmployeeFamilyMemberRepository(IDbContextProvider<ShikshaMitraDbContext> dbContextProvider)
		: base(dbContextProvider)
	{
	}

	public async Task<List<EmployeeFamilyMember>> GetByEmployeeIdAsync(Guid employeeId)
	{
		var dbSet = await GetDbSetAsync();

		// ABP applies the IMultiTenant filter; this query only scopes the employee aggregate child records.
		return await dbSet
			.Where(fm => fm.EmployeeId == employeeId)
			.OrderBy(fm => fm.RelationType)
			.ToListAsync();
	}

	public async Task<EmployeeFamilyMember?> GetByEmployeeIdAndRelationTypeAsync(Guid employeeId, FamilyRelationType relationType)
	{
		var dbSet = await GetDbSetAsync();

		// SECURITY: Parameterized query - SQL injection safe
		return await dbSet
			.FirstOrDefaultAsync(fm => fm.EmployeeId == employeeId && fm.RelationType == relationType);
	}

	public async Task DeleteByEmployeeIdAsync(Guid employeeId)
	{
		var dbSet = await GetDbSetAsync();

		// SECURITY: Parameterized query - SQL injection safe
		var familyMembers = await dbSet
			.Where(fm => fm.EmployeeId == employeeId)
			.ToListAsync();

		await DeleteManyAsync(familyMembers);
	}
}
