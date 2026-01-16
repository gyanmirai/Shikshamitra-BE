using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using ShikshaMitra.Groups.Dtos;
using Volo.Abp.ObjectMapping;
using Volo.Abp;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationMediums;
using ShikshaMitra.TenantSettings.AcademicSetups.Standards;
using ShikshaMitra.TenantSettings.AcademicSetups.Divisions;
using Volo.Abp.Identity;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.Standards;
using DivisionDto = ShikshaMitra.Groups.Dtos.DivisionDto;

namespace ShikshaMitra.Groups;

[Authorize]
[RemoteService(IsEnabled = false)]
public class GroupAppService : ApplicationService, IGroupAppService
{
    private readonly IRepository<Group, Guid> _groupRepository;
    private readonly GroupManager _groupManager;
    private readonly IRepository<EducationBoard, Guid> _boardRepository;
    private readonly IRepository<EducationMedium, Guid> _mediumRepository;
    private readonly IRepository<EducationMediumStage, Guid> _stageRepository;
    private readonly IRepository<Standard, Guid> _classRepository;
    private readonly IRepository<Division, Guid> _divisionRepository;
    private readonly IRepository<IdentityRole, Guid> _roleRepository;

    public GroupAppService(
        IRepository<Group, Guid> groupRepository, 
        GroupManager groupManager,
        IRepository<EducationBoard, Guid> boardRepository,
        IRepository<EducationMedium, Guid> mediumRepository,
        IRepository<EducationMediumStage, Guid> stageRepository,
        IRepository<Standard, Guid> classRepository,
        IRepository<Division, Guid> divisionRepository,
        IRepository<IdentityRole, Guid> roleRepository)
    {
        _groupRepository = groupRepository;
        _groupManager = groupManager;
        _boardRepository = boardRepository;
        _mediumRepository = mediumRepository;
        _stageRepository = stageRepository;
        _classRepository = classRepository;
        _divisionRepository = divisionRepository;
        _roleRepository = roleRepository;
    }

    public async Task<GroupDto> CreateAsync(CreateGroupDto input)
    {
        var group = await _groupManager.CreateAsync(input.Name, input.Description, input.AudienceType);
        
        // Map additional properties
        ObjectMapper.Map(input, group);
        
        await _groupRepository.InsertAsync(group);

        return ObjectMapper.Map<Group, GroupDto>(group);
    }

    public async Task DeleteAsync(Guid id)
    {
        await _groupRepository.DeleteAsync(id);
    }

    public async Task<GroupDetailsDto> GetAsync(Guid id)
    {
        var group = await _groupRepository.GetAsync(id);
        return ObjectMapper.Map<Group, GroupDetailsDto>(group);
    }

    public async Task<PagedResultDto<GroupDto>> GetListAsync(GetGroupsInputDto input)
    {
        var queryable = await _groupRepository.GetQueryableAsync();
        
        if (!string.IsNullOrWhiteSpace(input.Search))
        {
            queryable = queryable.Where(x => x.Name.Contains(input.Search));
        }

        var totalCount = await AsyncExecuter.CountAsync(queryable);
        
        queryable = queryable.OrderBy(x => x.Name) // Default sort
                             .PageBy(input.SkipCount, input.MaxResultCount);

        var groups = await AsyncExecuter.ToListAsync(queryable);

        return new PagedResultDto<GroupDto>(
            totalCount,
            ObjectMapper.Map<List<Group>, List<GroupDto>>(groups)
        );
    }

    public async Task<GroupDto> UpdateAsync(Guid id, UpdateGroupDto input)
    {
        var group = await _groupRepository.GetAsync(id);

        if (group.Name != input.Name)
        {
            group.SetName(input.Name);
        }
        
        group.SetDescription(input.Description);
        group.SetAudienceType(input.AudienceType);

        // Map list properties - this might need handling dependent on mapped collection behavior
        // Since we are using basic lists in the entity (mapped to JSON), the mapper should replace the list content.
        // Or we can manually set them to be safe.
        group.AcademicRules = input.AcademicRules;
        group.Roles = input.Roles;
        group.StudentFilters = input.StudentFilters;
        group.EmployeeFilters = input.EmployeeFilters;
        group.SelectedUserIds = input.SelectedUserIds;

        await _groupRepository.UpdateAsync(group);

        return ObjectMapper.Map<Group, GroupDto>(group);
    }

    public async Task<GroupPreviewResultDto> PreviewAsync(GroupPreviewInputDto input)
    {
        var result = await _groupManager.GetAudienceCountAsync(
            input.AcademicRules, 
            input.StudentFilters, 
            input.EmployeeFilters, 
            input.Roles);

        return new GroupPreviewResultDto
        {
            TotalUsers = result.Total,
            Students = result.Students,
            Employees = result.Employees
        };
    }

    public async Task<GroupDetailsResponseDto> GetDetailedAsync(Guid id)
    {
        var group = await _groupRepository.GetAsync(id);
        
        // Calculate audience counts
        var audienceCounts = await _groupManager.GetAudienceCountAsync(
            group.AcademicRules,
            group.StudentFilters,
            group.EmployeeFilters,
            group.Roles);
        
        // Basic Mapping
        var response = new GroupDetailsResponseDto
        {
            Id = group.Id,
            Name = group.Name,
            Description = group.Description,
            TotalAudience = audienceCounts.Total,
            Students = audienceCounts.Students,
            Employees = audienceCounts.Employees,
            CreatedAt = group.CreationTime,
            // StudentFilters and others mapped directly for now
            StudentFilters = group.StudentFilters,
            EmployeeFilters = group.EmployeeFilters
        };

        // Enrich Academic Rules
        foreach (var rule in group.AcademicRules)
        {
             var ruleResponse = new AcademicRuleResponseDto();
             
             if (rule.Boards != null && rule.Boards.Any())
             {
                 var entities = await _boardRepository.GetListAsync(x => rule.Boards.Contains(x.Id));
                 ruleResponse.Boards = entities.Select(e => new IdNameDto { Id = e.Id, Name = e.Type.ToString() }).ToList();
             }
             if (rule.Mediums != null && rule.Mediums.Any())
             {
                 var entities = await _mediumRepository.GetListAsync(x => rule.Mediums.Contains(x.Id));
                 ruleResponse.Mediums = entities.Select(e => new MediumDto { Id = e.Id, Name = e.Name, BoardId = e.EducationBoardId }).ToList();
             }
             if (rule.Stages != null && rule.Stages.Any())
             {
                 var entities = await _stageRepository.GetListAsync(x => rule.Stages.Contains(x.Id));
                 ruleResponse.Stages = entities.Select(e => new StageDto { Id = e.Id, Name = e.Name, MediumId = e.EducationMediumId }).ToList();
             }
             if (rule.Classes != null && rule.Classes.Any())
             {
                 var entities = await _classRepository.GetListAsync(x => rule.Classes.Contains(x.Id));
                 ruleResponse.Classes = entities.Select(e => new ClassDto { Id = e.Id, Name = e.Name, StageId = e.EducationMediumStageId }).ToList();
             }
             if (rule.Divisions != null && rule.Divisions.Any())
             {
                 var entities = await _divisionRepository.GetListAsync(x => rule.Divisions.Contains(x.Id));
                 ruleResponse.Divisions = entities.Select(e => new DivisionDto { Id = e.Id, Name = e.Name, ClassId = e.StandardId }).ToList();
             }
             
             response.AcademicRules.Add(ruleResponse);
        }

        // Enrich Roles
        if (group.Roles != null && group.Roles.Any())
        {
            var roles = await _roleRepository.GetListAsync(x => group.Roles.Contains(x.Id));
            response.Roles = roles.Select(r => new RoleDto { Id = r.Id, Name = r.Name, Code = r.Name.ToUpper() }).ToList();
        }

        return response;
    }
}
