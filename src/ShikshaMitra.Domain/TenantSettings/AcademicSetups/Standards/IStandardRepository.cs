using ShikshaMitra.TenantSettings.Standards;
using System;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.TenantSettings.AcademicSetups.Standards;

public interface IStandardRepository : IRepository<Standard, Guid>
{
	Task<Standard?> FindByNameAndEducationMediumStageIdAsync(string name, Guid educationMediumStageId);
}