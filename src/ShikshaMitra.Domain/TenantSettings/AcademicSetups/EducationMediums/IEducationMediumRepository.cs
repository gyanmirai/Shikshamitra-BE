using ShikshaMitra.TenantSettings.EducationMediums;
using System;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.TenantSettings.AcademicSetups.EducationMediums;

public interface IEducationMediumRepository :  IRepository<EducationMedium, Guid>
{
	Task<EducationMedium?> FindByNameAndEducationBoardIdAsync(string name, Guid educationBoardId);
}