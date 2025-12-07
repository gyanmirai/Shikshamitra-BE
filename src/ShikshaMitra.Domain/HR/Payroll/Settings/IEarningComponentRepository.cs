using System;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.HR.Payroll.Settings;

/// <summary>
/// Repository interface for EarningComponent entity
/// </summary>
public interface IEarningComponentRepository : IRepository<EarningComponent, Guid>
{
	/// <summary>
	/// Check if an earning component with the given name already exists (case-insensitive)
	/// </summary>
	/// <param name="name">Component name</param>
	/// <param name="excludeId">ID to exclude from check (for update scenarios)</param>
	/// <returns>True if exists, false otherwise</returns>
	Task<bool> IsNameExistsAsync(string name, Guid? excludeId = null);
	
	/// <summary>
	/// Get earning component by name (case-insensitive)
	/// </summary>
	Task<EarningComponent?> FindByNameAsync(string name);
}
