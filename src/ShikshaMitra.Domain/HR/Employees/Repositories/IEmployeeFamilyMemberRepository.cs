using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.HR.Employees;

/// <summary>
/// Repository interface for EmployeeFamilyMember entity
/// </summary>
public interface IEmployeeFamilyMemberRepository : IRepository<EmployeeFamilyMember, Guid>
{
	/// <summary>
	/// Gets all family members for a specific employee
	/// </summary>
	/// <param name="employeeId">The employee ID</param>
	/// <returns>List of family members</returns>
	Task<List<EmployeeFamilyMember>> GetByEmployeeIdAsync(Guid employeeId);

	/// <summary>
	/// Gets a specific family member by employee and relation type
	/// </summary>
	/// <param name="employeeId">The employee ID</param>
	/// <param name="relationType">The family relation type</param>
	/// <returns>The family member or null</returns>
	Task<EmployeeFamilyMember?> GetByEmployeeIdAndRelationTypeAsync(Guid employeeId, FamilyRelationType relationType);

	/// <summary>
	/// Deletes all family members for a specific employee
	/// </summary>
	/// <param name="employeeId">The employee ID</param>
	Task DeleteByEmployeeIdAsync(Guid employeeId);
}
