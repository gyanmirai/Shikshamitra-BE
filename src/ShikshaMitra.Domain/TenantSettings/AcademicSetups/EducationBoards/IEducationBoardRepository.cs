using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using System;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.TenantSettings.EducationBoards;

public interface IEducationBoardRepository : IRepository<EducationBoard, Guid>
{
	Task<EducationBoard?> FindByNameAsync(EducationBoardType educationBoardType);

	Task<EducationBoard?> FindByNameAndAcademicYearIdAsync(EducationBoardType educationBoardType, Guid academicYearId);

}