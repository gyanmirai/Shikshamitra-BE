using System;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.TenantSettings.AcademicYears;

public interface IAcademicYearRepository : IRepository<AcademicYear, Guid>
{
	Task<AcademicYear?> FindByNameAsync(string name);
}