using System;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.HR.Calendar.HolidayTypes;

/// <summary>
/// Repository interface for HolidayType
/// </summary>
public interface IHolidayTypeRepository : IRepository<HolidayType, Guid>
{
    /// <summary>
    /// Get HolidayType by name
    /// </summary>
    Task<HolidayType?> GetByNameAsync(string name);

    /// <summary>
    /// Check if HolidayType with given name exists
    /// </summary>
    Task<bool> ExistsByNameAsync(string name);
}
