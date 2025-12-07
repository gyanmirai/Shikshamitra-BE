using System;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.HR.Payroll.Settings;

/// <summary>
/// Repository interface for DeductionComponent entity
/// </summary>
public interface IDeductionComponentRepository : IRepository<DeductionComponent, Guid>
{
	/// <summary>
	/// Check if a deduction component with the given name already exists (case-insensitive)
	/// </summary>
	/// <param name="name">Component name</param>
	/// <param name="excludeId">ID to exclude from check (for update scenarios)</param>
	/// <returns>True if exists, false otherwise</returns>
	Task<bool> IsNameExistsAsync(string name, Guid? excludeId = null);
	
	/// <summary>
	/// Get deduction component by name (case-insensitive)
	/// </summary>
	Task<DeductionComponent?> FindByNameAsync(string name);
}
