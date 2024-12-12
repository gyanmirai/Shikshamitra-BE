using ShikshaMitra.TenantSettings.EducationBoards;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.TenantSettings.AcademicSetups.EducationMediums;

public interface IEducationMediumStageRepository : IRepository<EducationMediumStage, Guid>
{
	Task<EducationMediumStage?> FindByNameAndEducationMediumIdAsync(string name, Guid educationMediumId);
	Task<List<EducationMediumStage>> GetEducationMediumStagesByEducationMediumIdAsync(Guid educationMediumId);
}