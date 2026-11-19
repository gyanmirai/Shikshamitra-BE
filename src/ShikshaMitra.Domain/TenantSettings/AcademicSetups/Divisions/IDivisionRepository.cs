using ShikshaMitra.TenantSettings.Divisions;
using System;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.TenantSettings.AcademicSetups.Divisions;

public interface IDivisionRepository : IRepository<Division, Guid>
{
	Task<Division?> FindByNameAndStandardIdAsync(string name, Guid standardId, Guid? excludedId = null);
}
