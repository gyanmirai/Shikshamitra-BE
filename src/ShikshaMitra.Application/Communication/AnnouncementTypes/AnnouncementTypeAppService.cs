using System;
using System.Threading.Tasks;
using ShikshaMitra.Application.Contracts.Communication.AnnouncementTypes;
using ShikshaMitra.Communication.AnnouncementTypes;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Application.Communication.AnnouncementTypes;

/// <summary>
/// Application service for managing announcement types
/// </summary>
public class AnnouncementTypeAppService : ApplicationService, IAnnouncementTypeAppService
{
    private readonly IAnnouncementTypeRepository _announcementTypeRepository;
    private readonly IRepository<AnnouncementType, Guid> _repository;

    public AnnouncementTypeAppService(
        IAnnouncementTypeRepository announcementTypeRepository,
        IRepository<AnnouncementType, Guid> repository)
    {
        _announcementTypeRepository = announcementTypeRepository;
        _repository = repository;
    }

    public async Task<PagedResultDto<AnnouncementTypeDto>> GetListAsync(GetAnnouncementTypeListRequestDto input)
    {
        var items = await _announcementTypeRepository.GetByAcademicMediumAsync(
            input.AcademicMediumId,
            input.Search,
            input.SkipCount,
            input.MaxResultCount);

        var totalCount = await _announcementTypeRepository.GetCountByAcademicMediumAsync(
            input.AcademicMediumId,
            input.Search);

        return new PagedResultDto<AnnouncementTypeDto>(
            totalCount,
            ObjectMapper.Map<System.Collections.Generic.List<AnnouncementType>, System.Collections.Generic.List<AnnouncementTypeDto>>(items)
        );
    }

    public async Task<AnnouncementTypeDto> GetAsync(Guid id)
    {
        var announcementType = await _repository.GetAsync(id);
        return ObjectMapper.Map<AnnouncementType, AnnouncementTypeDto>(announcementType);
    }

    public async Task<AnnouncementTypeDto> CreateAsync(Guid? academicMediumId, CreateUpdateAnnouncementTypeDto input)
    {
        // Check if announcement type name already exists
        var exists = await _announcementTypeRepository.ExistsByNameAsync(
            input.AnnouncementType,
            academicMediumId);

        if (exists)
        {
            throw new Volo.Abp.BusinessException("AnnouncementType:DuplicateName")
                .WithData("Name", input.AnnouncementType);
        }

        var announcementType = new AnnouncementType(
            GuidGenerator.Create(),
            CurrentTenant.Id,
            academicMediumId,
            input.AnnouncementType,
            input.Description,
            input.Color);

        await _repository.InsertAsync(announcementType, autoSave: true);

        return ObjectMapper.Map<AnnouncementType, AnnouncementTypeDto>(announcementType);
    }

    public async Task<AnnouncementTypeDto> UpdateAsync(Guid id, CreateUpdateAnnouncementTypeDto input)
    {
        var announcementType = await _repository.GetAsync(id);

        // Check if new name conflicts with existing announcement type
        var exists = await _announcementTypeRepository.ExistsByNameAsync(
            input.AnnouncementType,
            announcementType.AcademicMediumId,
            id);

        if (exists)
        {
            throw new Volo.Abp.BusinessException("AnnouncementType:DuplicateName")
                .WithData("Name", input.AnnouncementType);
        }

        announcementType.Update(
            input.AnnouncementType,
            input.Description,
            input.Color);

        await _repository.UpdateAsync(announcementType, autoSave: true);

        return ObjectMapper.Map<AnnouncementType, AnnouncementTypeDto>(announcementType);
    }

    public async Task DeleteAsync(Guid id)
    {
        await _repository.DeleteAsync(id);
    }
}
