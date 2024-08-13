using System;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.TenantSettings.EducationBoards;

public interface IEducationBoardRepository : IRepository<EducationBoard, Guid>
{
	Task<EducationBoard?> FindByNameAsync(string name);

	Task<EducationBoard?> FindByNameAndAcademicYearIdAsync(string name, Guid academicYearId);

}