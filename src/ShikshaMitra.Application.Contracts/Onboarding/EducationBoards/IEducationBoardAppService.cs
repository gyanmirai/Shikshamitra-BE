using System;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Onboarding.EducationBoards;

public interface IEducationBoardAppService : 
    ICrudAppService<
        EducationBoardDto, // The DTO to return entities
        Guid, // The primary key of the entity
        PagedAndSortedResultRequestDto, // Used for paging/sorting
        CreateUpdateEducationBoardDto> // The DTO to create/update an entity
{
}