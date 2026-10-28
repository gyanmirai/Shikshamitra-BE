using System;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;

public interface IEducationBoardAppService :
	ICrudAppService<
		EducationBoardDto, // The DTO to return entities
		Guid, // The primary key of the entity
		PagedAndSortedResultRequestDto, // Used for paging/sorting
		CreateUpdateEducationBoardDto> // The DTO to create/update an entity
{
	System.Threading.Tasks.Task<System.Collections.Generic.List<EducationBoardOptionDto>> GetOptionsAsync();
	System.Threading.Tasks.Task<ListResultDto<EducationBoardDto>> GetHierarchyAsync(Guid academicYearId);
}
