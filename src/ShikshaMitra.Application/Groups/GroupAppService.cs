using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Application.Contracts.Groups;
using ShikshaMitra.Groups;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Students;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;

namespace ShikshaMitra.Application.Groups;

/// <summary>
/// Application service for Group management including CRUD and member resolution
/// </summary>
[RemoteService(false)]
public class GroupAppService : ApplicationService, IGroupAppService
{
    private readonly IRepository<Group, Guid> _repository;
    private readonly IGroupRepository _groupRepository;
    private readonly IRepository<Student, Guid> _studentRepository;
    private readonly IRepository<Employee, Guid> _employeeRepository;
    private readonly IRepository<IdentityRole, Guid> _roleRepository;
    private readonly IRepository<Standard, Guid> _standardRepository;
    private readonly IRepository<Division, Guid> _divisionRepository;
    private readonly IRepository<EducationMediumStage, Guid> _stageRepository;
    private readonly IRepository<EducationMedium, Guid> _mediumRepository;
    private readonly IRepository<EducationBoard, Guid> _boardRepository;

    public GroupAppService(
        IRepository<Group, Guid> repository,
        IGroupRepository groupRepository,
        IRepository<Student, Guid> studentRepository,
        IRepository<Employee, Guid> employeeRepository,
        IRepository<IdentityRole, Guid> roleRepository,
        IRepository<Standard, Guid> standardRepository,
        IRepository<Division, Guid> divisionRepository,
        IRepository<EducationMediumStage, Guid> stageRepository,
        IRepository<EducationMedium, Guid> mediumRepository,
        IRepository<EducationBoard, Guid> boardRepository)
    {
        _repository = repository;
        _groupRepository = groupRepository;
        _studentRepository = studentRepository;
        _employeeRepository = employeeRepository;
        _roleRepository = roleRepository;
        _standardRepository = standardRepository;
        _divisionRepository = divisionRepository;
        _stageRepository = stageRepository;
        _mediumRepository = mediumRepository;
        _boardRepository = boardRepository;
    }

    #region CRUD Operations

    public async Task<GroupDto> GetAsync(Guid id)
    {
        var group = await _groupRepository.GetWithDetailsAsync(id);
        if (group == null)
            throw new UserFriendlyException($"Group with ID {id} not found.");

        var dto = await MapToDtoAsync(group);
        dto.MemberCount = await GetGroupMemberCountAsync(id);
        return dto;
    }

    public async Task<PagedResultDto<GroupDto>> GetListAsync(GetGroupListRequestDto input)
    {
        var totalCount = await _groupRepository.GetCountAsync(
            input.AcademicYearId,
            input.Filter);

        var groups = await _groupRepository.GetListWithDetailsAsync(
            input.AcademicYearId,
            input.Filter,
            input.SkipCount,
            input.MaxResultCount);

        var dtos = new List<GroupDto>();
        foreach (var group in groups)
        {
            var dto = await MapToDtoAsync(group);
            dto.MemberCount = await GetGroupMemberCountInternalAsync(group);
            dtos.Add(dto);
        }

        return new PagedResultDto<GroupDto>(totalCount, dtos);
    }

    public async Task<GroupDto> CreateAsync(Guid academicYearId, CreateUpdateGroupDto input)
    {
        // Check for duplicate name
        if (await _groupRepository.ExistsByNameAsync(input.Name))
            throw new UserFriendlyException($"A group with name '{input.Name}' already exists.");

        // Extract division and role IDs for each selection type
        var ruleBasedDivisionIds = input.RuleBased?.DivisionIds ?? new List<Guid>();
        var ruleBasedRoleIds = input.RuleBased?.RoleIds ?? new List<Guid>();
        var nameBasedDivisionIds = input.NameBased?.DivisionIds ?? new List<Guid>();
        var nameBasedRoleIds = input.NameBased?.RoleIds ?? new List<Guid>();

        var group = Group.Create(
            GuidGenerator.Create(),
            CurrentTenant.Id,
            academicYearId,
            input.Name,
            input.Description,
            ruleBasedDivisionIds,
            ruleBasedRoleIds,
            nameBasedDivisionIds,
            nameBasedRoleIds);

        // Add rule-based student rules
        if (input.RuleBased?.StudentFilters != null)
        {
            foreach (var rule in input.RuleBased.StudentFilters)
            {
                group.AddStudentRule(new GroupStudentRule(
                    GuidGenerator.Create(),
                    group.Id,
                    GroupSelectionType.RuleBased,
                    rule.Field,
                    rule.Operator,
                    rule.Value));
            }
        }

        // Add rule-based employee rules
        if (input.RuleBased?.EmployeeFilters != null)
        {
            foreach (var rule in input.RuleBased.EmployeeFilters)
            {
                group.AddEmployeeRule(new GroupEmployeeRule(
                    GuidGenerator.Create(),
                    group.Id,
                    GroupSelectionType.RuleBased,
                    rule.Field,
                    rule.Operator,
                    rule.Value));
            }
        }

        // Add name-based student rules (for filtering context)
        if (input.NameBased?.StudentFilters != null)
        {
            foreach (var rule in input.NameBased.StudentFilters)
            {
                group.AddStudentRule(new GroupStudentRule(
                    GuidGenerator.Create(),
                    group.Id,
                    GroupSelectionType.NameBased,
                    rule.Field,
                    rule.Operator,
                    rule.Value));
            }
        }

        // Add name-based employee rules (for filtering context)
        if (input.NameBased?.EmployeeFilters != null)
        {
            foreach (var rule in input.NameBased.EmployeeFilters)
            {
                group.AddEmployeeRule(new GroupEmployeeRule(
                    GuidGenerator.Create(),
                    group.Id,
                    GroupSelectionType.NameBased,
                    rule.Field,
                    rule.Operator,
                    rule.Value));
            }
        }

        // Add manually selected members (from name-based selection)
        if (input.NameBased?.SelectedMembers != null)
        {
            foreach (var member in input.NameBased.SelectedMembers)
            {
                var userId = member.UserId ?? await ResolveUserIdAsync(member.MemberType, member.MemberId);
                group.AddMember(new GroupMember(
                    GuidGenerator.Create(),
                    group.Id,
                    member.MemberType,
                    member.MemberId,
                    userId));
            }
        }

        await _repository.InsertAsync(group, autoSave: true);

        return await GetAsync(group.Id);
    }

    public async Task<GroupDto> UpdateAsync(Guid id, Guid academicYearId, CreateUpdateGroupDto input)
    {
        var group = await _groupRepository.GetWithDetailsAsync(id);
        if (group == null)
            throw new UserFriendlyException($"Group with ID {id} not found.");

        // Check for duplicate name (excluding current group)
        if (await _groupRepository.ExistsByNameAsync(input.Name, id))
            throw new UserFriendlyException($"A group with name '{input.Name}' already exists.");

        // Update basic info
        group.Update(input.Name, input.Description);

        // Update division and role IDs
        var ruleBasedDivisionIds = input.RuleBased?.DivisionIds ?? new List<Guid>();
        var ruleBasedRoleIds = input.RuleBased?.RoleIds ?? new List<Guid>();
        var nameBasedDivisionIds = input.NameBased?.DivisionIds ?? new List<Guid>();
        var nameBasedRoleIds = input.NameBased?.RoleIds ?? new List<Guid>();

        group.UpdateRuleBasedSelection(ruleBasedDivisionIds, ruleBasedRoleIds);
        group.UpdateNameBasedSelection(nameBasedDivisionIds, nameBasedRoleIds);

        // Update rule-based student rules
        group.ClearStudentRules(GroupSelectionType.RuleBased);
        if (input.RuleBased?.StudentFilters != null)
        {
            foreach (var rule in input.RuleBased.StudentFilters)
            {
                group.AddStudentRule(new GroupStudentRule(
                    GuidGenerator.Create(),
                    group.Id,
                    GroupSelectionType.RuleBased,
                    rule.Field,
                    rule.Operator,
                    rule.Value));
            }
        }

        // Update rule-based employee rules
        group.ClearEmployeeRules(GroupSelectionType.RuleBased);
        if (input.RuleBased?.EmployeeFilters != null)
        {
            foreach (var rule in input.RuleBased.EmployeeFilters)
            {
                group.AddEmployeeRule(new GroupEmployeeRule(
                    GuidGenerator.Create(),
                    group.Id,
                    GroupSelectionType.RuleBased,
                    rule.Field,
                    rule.Operator,
                    rule.Value));
            }
        }

        // Update name-based student rules
        group.ClearStudentRules(GroupSelectionType.NameBased);
        if (input.NameBased?.StudentFilters != null)
        {
            foreach (var rule in input.NameBased.StudentFilters)
            {
                group.AddStudentRule(new GroupStudentRule(
                    GuidGenerator.Create(),
                    group.Id,
                    GroupSelectionType.NameBased,
                    rule.Field,
                    rule.Operator,
                    rule.Value));
            }
        }

        // Update name-based employee rules
        group.ClearEmployeeRules(GroupSelectionType.NameBased);
        if (input.NameBased?.EmployeeFilters != null)
        {
            foreach (var rule in input.NameBased.EmployeeFilters)
            {
                group.AddEmployeeRule(new GroupEmployeeRule(
                    GuidGenerator.Create(),
                    group.Id,
                    GroupSelectionType.NameBased,
                    rule.Field,
                    rule.Operator,
                    rule.Value));
            }
        }

        // Update members (only from name-based selection)
        group.ClearMembers();
        if (input.NameBased?.SelectedMembers != null)
        {
            foreach (var member in input.NameBased.SelectedMembers)
            {
                var userId = member.UserId ?? await ResolveUserIdAsync(member.MemberType, member.MemberId);
                group.AddMember(new GroupMember(
                    GuidGenerator.Create(),
                    group.Id,
                    member.MemberType,
                    member.MemberId,
                    userId));
            }
        }

        await _repository.UpdateAsync(group, autoSave: true);

        return await GetAsync(id);
    }

    public async Task DeleteAsync(Guid id)
    {
        await _repository.DeleteAsync(id);
    }

    #endregion

    #region User Selection for Name-Based Groups

    public async Task<PagedResultDto<UserSelectionDto>> GetUsersForSelectionAsync(GetUsersForSelectionDto input)
    {
        // Fetch both students and employees, then combine results
        var studentResults = await GetStudentsForSelectionAsync(input);
        var employeeResults = await GetEmployeesForSelectionAsync(input);

        // Combine and sort results
        var combinedItems = studentResults.Items
            .Concat(employeeResults.Items)
            .OrderBy(u => u.DisplayName)
            .ToList();

        var totalCount = studentResults.TotalCount + employeeResults.TotalCount;

        // Apply pagination to combined results
        var pagedItems = combinedItems
            .Skip(input.SkipCount)
            .Take(input.MaxResultCount)
            .ToList();

        return new PagedResultDto<UserSelectionDto>(totalCount, pagedItems);
    }

    private async Task<PagedResultDto<UserSelectionDto>> GetStudentsForSelectionAsync(GetUsersForSelectionDto input)
    {
        var query = await _studentRepository.GetQueryableAsync();

        // Filter by academic year
        query = query.Where(s => s.AcademicYearId == input.AcademicYearId);

        // Apply division filters
        if (input.DivisionFilters.Count > 0)
        {
            query = query.Where(s =>
                s.DivisionId.HasValue && input.DivisionFilters.Contains(s.DivisionId.Value));
        }

        // Apply search term
        if (!string.IsNullOrWhiteSpace(input.SearchTerm))
        {
            var searchLower = input.SearchTerm.ToLower();
            query = query.Where(s =>
                (s.FirstName != null && s.FirstName.ToLower().Contains(searchLower)) ||
                (s.LastName != null && s.LastName.ToLower().Contains(searchLower)) ||
                (s.MiddleName != null && s.MiddleName.ToLower().Contains(searchLower)) ||
                (s.RollNo.HasValue && s.RollNo.ToString()!.Contains(searchLower)) ||
                (s.GrNo.HasValue && s.GrNo.ToString()!.Contains(searchLower)));
        }

        // Apply additional filters using RuleQueryBuilder
        if (input.StudentFilters.Count > 0)
        {
            var studentRules = input.StudentFilters.Select(f => new StudentRuleDto
            {
                Field = (StudentRuleField) f.Field,
                Operator = f.Operator,
                Value = f.Value
            }).ToList();

            query = RuleQueryBuilder.ApplyStudentRules(query, studentRules, MapStudentRuleField);
        }

        var totalCount = await AsyncExecuter.CountAsync(query);

        var students = await AsyncExecuter.ToListAsync(
            query.OrderBy(s => s.FirstName).ThenBy(s => s.LastName));

        // Get standard and division names
        var standardIds = students.Where(s => s.StandardId.HasValue).Select(s => s.StandardId!.Value).Distinct().ToList();
        var divisionIds = students.Where(s => s.DivisionId.HasValue).Select(s => s.DivisionId!.Value).Distinct().ToList();

        var standards = await AsyncExecuter.ToListAsync(
            (await _standardRepository.GetQueryableAsync()).Where(s => standardIds.Contains(s.Id)));
        var divisions = await AsyncExecuter.ToListAsync(
            (await _divisionRepository.GetQueryableAsync()).Where(d => divisionIds.Contains(d.Id)));

        var items = students.Select(s => new UserSelectionDto
        {
            MemberType = GroupMemberType.Student,
            MemberId = s.Id,
            UserId = null, // Students may not have UserId linked yet
            DisplayName = string.Join(" ", new[] { s.FirstName, s.MiddleName, s.LastName }.Where(n => !string.IsNullOrWhiteSpace(n))),
            Identifier = s.RollNo?.ToString() ?? s.GrNo?.ToString(),
            StandardName = s.StandardId.HasValue ? standards.FirstOrDefault(st => st.Id == s.StandardId)?.Name : null,
            DivisionName = s.DivisionId.HasValue ? divisions.FirstOrDefault(d => d.Id == s.DivisionId)?.Name : null
        }).ToList();

        return new PagedResultDto<UserSelectionDto>(totalCount, items);
    }

    private async Task<PagedResultDto<UserSelectionDto>> GetEmployeesForSelectionAsync(GetUsersForSelectionDto input)
    {
        var query = await _employeeRepository.GetQueryableAsync();

        // Apply role filters
        if (input.RoleFilters.Count > 0)
        {
            query = query.Where(e => input.RoleFilters.Contains(e.RoleId));
        }

        // Apply search term
        if (!string.IsNullOrWhiteSpace(input.SearchTerm))
        {
            var searchLower = input.SearchTerm.ToLower();
            query = query.Where(e =>
                (e.FirstName != null && e.FirstName.ToLower().Contains(searchLower)) ||
                (e.LastName != null && e.LastName.ToLower().Contains(searchLower)) ||
                (e.MiddleName != null && e.MiddleName.ToLower().Contains(searchLower)) ||
                e.EmployeeCode.ToLower().Contains(searchLower));
        }

        // Apply additional filters using RuleQueryBuilder
        if (input.EmployeeFilters.Count > 0)
        {
            var employeeRules = input.EmployeeFilters.Select(f => new EmployeeRuleDto
            {
                Field = (EmployeeRuleField) f.Field,
                Operator = f.Operator,
                Value = f.Value
            }).ToList();

            query = RuleQueryBuilder.ApplyEmployeeRules(query, employeeRules, MapEmployeeRuleField);
        }

        var totalCount = await AsyncExecuter.CountAsync(query);

        var employees = await AsyncExecuter.ToListAsync(
            query.OrderBy(e => e.FirstName).ThenBy(e => e.LastName));

        // Get role names separately
        var roleIds = employees.Select(e => e.RoleId).Distinct().ToList();
        var roles = await AsyncExecuter.ToListAsync(
            (await _roleRepository.GetQueryableAsync()).Where(r => roleIds.Contains(r.Id)));

        var items = employees.Select(e => new UserSelectionDto
        {
            MemberType = GroupMemberType.Employee,
            MemberId = e.Id,
            UserId = e.UserId,
            DisplayName = e.EmployeeName,
            Identifier = e.EmployeeCode,
            Department = e.Department,
            RoleName = roles.FirstOrDefault(r => r.Id == e.RoleId)?.Name
        }).ToList();

        return new PagedResultDto<UserSelectionDto>(totalCount, items);
    }

    #endregion

    #region Member Resolution

    public async Task<List<Guid>> GetGroupMembersAsync(Guid groupId)
    {
        var group = await _groupRepository.GetWithDetailsAsync(groupId);
        if (group == null)
            throw new UserFriendlyException($"Group with ID {groupId} not found.");

        return await ResolveGroupMembersAsync(group);
    }

    public async Task<bool> IsUserInGroupsAsync(Guid userId, List<Guid> groupIds)
    {
        foreach (var groupId in groupIds)
        {
            var members = await GetGroupMembersAsync(groupId);
            if (members.Contains(userId))
                return true;
        }

        return false;
    }

    public async Task<int> GetGroupMemberCountAsync(Guid groupId)
    {
        var group = await _groupRepository.GetWithDetailsAsync(groupId);
        if (group == null)
            return 0;

        return await GetGroupMemberCountInternalAsync(group);
    }

    public async Task<List<Guid>> GetUserGroupIdsAsync(Guid userId)
    {
        var groups = await AsyncExecuter.ToListAsync(await _repository.GetQueryableAsync());
        var userGroupIds = new List<Guid>();

        foreach (var group in groups)
        {
            var groupWithDetails = await _groupRepository.GetWithDetailsAsync(group.Id);
            if (groupWithDetails == null)
                continue;

            var members = await ResolveGroupMembersAsync(groupWithDetails);
            if (members.Contains(userId))
            {
                userGroupIds.Add(group.Id);
            }
        }

        return userGroupIds;
    }

    private async Task<int> GetGroupMemberCountInternalAsync(Group group)
    {
        var members = await ResolveGroupMembersAsync(group);
        return members.Count;
    }

    private async Task<List<Guid>> ResolveGroupMembersAsync(Group group)
    {
        var userIds = new HashSet<Guid>();

        // Get students matching academic selections and student rules
        var studentUserIds = await ResolveStudentMembersAsync(group);
        foreach (var id in studentUserIds)
            userIds.Add(id);

        // Get employees matching role selections and employee rules
        var employeeUserIds = await ResolveEmployeeMembersAsync(group);
        foreach (var id in employeeUserIds)
            userIds.Add(id);

        // Add manually selected members
        foreach (var member in group.Members.Where(m => m.UserId.HasValue))
            userIds.Add(member.UserId!.Value);

        return userIds.ToList();
    }

    private async Task<List<Guid>> ResolveStudentMembersAsync(Group group)
    {
        var ruleBasedStudentRules = group.GetRuleBasedStudentRules().ToList();

        // If no rule-based division selections and no student rules, skip students
        if (group.RuleBasedDivisionIds.Count == 0 && ruleBasedStudentRules.Count == 0)
            return new List<Guid>();

        var query = await _studentRepository.GetQueryableAsync();

        // Filter by academic year
        query = query.Where(s => s.AcademicYearId == group.AcademicYearId);

        // Apply division selections from rule-based configuration
        if (group.RuleBasedDivisionIds.Count > 0)
        {
            query = query.Where(s =>
                s.DivisionId.HasValue && group.RuleBasedDivisionIds.Contains(s.DivisionId.Value));
        }

        // Apply student rules from rule-based configuration
        if (ruleBasedStudentRules.Count > 0)
        {
            var rules = ruleBasedStudentRules.Select(r => new StudentRuleDto
            {
                Field = r.Field,
                Operator = r.Operator,
                Value = r.Value
            }).ToList();

            query = RuleQueryBuilder.ApplyStudentRules(query, rules, MapStudentRuleField);
        }

        // Note: Students may not have UserId linked, so we return an empty list for now
        // In a real scenario, you would have a UserId property on Student
        var students = await AsyncExecuter.ToListAsync(query);

        // Return empty list since Student doesn't have UserId
        // The frontend will use MemberId (Student.Id) for reference
        return new List<Guid>();
    }

    private async Task<List<Guid>> ResolveEmployeeMembersAsync(Group group)
    {
        var ruleBasedEmployeeRules = group.GetRuleBasedEmployeeRules().ToList();

        // If no rule-based role selections and no employee rules, skip employees
        if (group.RuleBasedRoleIds.Count == 0 && ruleBasedEmployeeRules.Count == 0)
            return new List<Guid>();

        var query = await _employeeRepository.GetQueryableAsync();

        // Apply role selections from rule-based configuration
        if (group.RuleBasedRoleIds.Count > 0)
        {
            query = query.Where(e => group.RuleBasedRoleIds.Contains(e.RoleId));
        }

        // Apply employee rules from rule-based configuration
        if (ruleBasedEmployeeRules.Count > 0)
        {
            var rules = ruleBasedEmployeeRules.Select(r => new EmployeeRuleDto
            {
                Field = r.Field,
                Operator = r.Operator,
                Value = r.Value
            }).ToList();

            query = RuleQueryBuilder.ApplyEmployeeRules(query, rules, MapEmployeeRuleField);
        }

        var employees = await AsyncExecuter.ToListAsync(query.Where(e => e.UserId.HasValue));

        return employees
            .Where(e => e.UserId.HasValue)
            .Select(e => e.UserId!.Value)
            .Distinct()
            .ToList();
    }

    #endregion

    #region Helper Methods

    private async Task<GroupDto> MapToDtoAsync(Group group)
    {
        var ruleBasedStudentRules = group.GetRuleBasedStudentRules().ToList();
        var ruleBasedEmployeeRules = group.GetRuleBasedEmployeeRules().ToList();
        var nameBasedStudentRules = group.GetNameBasedStudentRules().ToList();
        var nameBasedEmployeeRules = group.GetNameBasedEmployeeRules().ToList();

        // Check if there's any rule-based configuration
        var hasRuleBased = group.RuleBasedDivisionIds.Count > 0 ||
                           group.RuleBasedRoleIds.Count > 0 ||
                           ruleBasedStudentRules.Count > 0 ||
                           ruleBasedEmployeeRules.Count > 0;

        // Check if there's any name-based configuration
        var hasNameBased = group.NameBasedDivisionIds.Count > 0 ||
                           group.NameBasedRoleIds.Count > 0 ||
                           nameBasedStudentRules.Count > 0 ||
                           nameBasedEmployeeRules.Count > 0 ||
                           group.Members.Count > 0;

        // Build academic selections for both rule-based and name-based
        var ruleBasedAcademicSelection = hasRuleBased && group.RuleBasedDivisionIds.Count > 0 
            ? await BuildAcademicSelectionAsync(group.RuleBasedDivisionIds)
            : null;
        var nameBasedAcademicSelection = hasNameBased && group.NameBasedDivisionIds.Count > 0 
            ? await BuildAcademicSelectionAsync(group.NameBasedDivisionIds)
            : null;

        return new GroupDto
        {
            Id = group.Id,
            TenantId = group.TenantId ?? Guid.Empty,
            AcademicYearId = group.AcademicYearId,
            Name = group.Name,
            Description = group.Description,
            RuleBased = hasRuleBased ? new RuleBasedSelectionResponseDto
            {
                AcademicSelection = ruleBasedAcademicSelection,
                RoleIds = group.RuleBasedRoleIds,
                StudentFilters = ruleBasedStudentRules.Select(r => new StudentRuleDto
                {
                    Id = r.Id,
                    Field = r.Field,
                    Operator = r.Operator,
                    Value = r.Value
                }).ToList(),
                EmployeeFilters = ruleBasedEmployeeRules.Select(r => new EmployeeRuleDto
                {
                    Id = r.Id,
                    Field = r.Field,
                    Operator = r.Operator,
                    Value = r.Value
                }).ToList()
            } : null,
            NameBased = hasNameBased ? new NameBasedSelectionResponseDto
            {
                AcademicSelection = nameBasedAcademicSelection,
                RoleIds = group.NameBasedRoleIds,
                StudentFilters = nameBasedStudentRules.Select(r => new StudentRuleDto
                {
                    Id = r.Id,
                    Field = r.Field,
                    Operator = r.Operator,
                    Value = r.Value
                }).ToList(),
                EmployeeFilters = nameBasedEmployeeRules.Select(r => new EmployeeRuleDto
                {
                    Id = r.Id,
                    Field = r.Field,
                    Operator = r.Operator,
                    Value = r.Value
                }).ToList(),
                SelectedMembers = group.Members.Select(m => new GroupMemberDto
                {
                    Id = m.Id,
                    MemberType = m.MemberType,
                    MemberId = m.MemberId,
                    UserId = m.UserId
                }).ToList()
            } : null,
            CreationTime = group.CreationTime,
            CreatorId = group.CreatorId,
            LastModificationTime = group.LastModificationTime,
            LastModifierId = group.LastModifierId
        };
    }

    /// <summary>
    /// Builds AcademicSelectionDto from division IDs by traversing the hierarchy
    /// Division → Standard (Class) → Stage → Medium → Board
    /// </summary>
    private async Task<AcademicSelectionDto> BuildAcademicSelectionAsync(List<Guid> divisionIds)
    {
        if (divisionIds.Count == 0)
            return new AcademicSelectionDto();

        // Get divisions with their Standards
        var divisionQuery = await _divisionRepository.GetQueryableAsync();
        var divisions = await AsyncExecuter.ToListAsync(
            divisionQuery.Where(d => divisionIds.Contains(d.Id)));

        // Get unique Standard IDs
        var standardIds = divisions.Select(d => d.StandardId).Distinct().ToList();

        // Get Standards with their Stages
        var standardQuery = await _standardRepository.GetQueryableAsync();
        var standards = await AsyncExecuter.ToListAsync(
            standardQuery.Where(s => standardIds.Contains(s.Id)));

        // Get unique Stage IDs
        var stageIds = standards.Select(s => s.EducationMediumStageId).Distinct().ToList();

        // Get Stages with their Mediums
        var stageQuery = await _stageRepository.GetQueryableAsync();
        var stages = await AsyncExecuter.ToListAsync(
            stageQuery.Where(s => stageIds.Contains(s.Id)));

        // Get unique Medium IDs
        var mediumIds = stages.Select(s => s.EducationMediumId).Distinct().ToList();

        // Get Mediums with their Boards
        var mediumQuery = await _mediumRepository.GetQueryableAsync();
        var mediums = await AsyncExecuter.ToListAsync(
            mediumQuery.Where(m => mediumIds.Contains(m.Id)));

        // Get unique Board IDs
        var boardIds = mediums.Select(m => m.EducationBoardId).Distinct().ToList();

        return new AcademicSelectionDto
        {
            Boards = boardIds,
            Mediums = mediumIds,
            Stages = stageIds,
            Classes = standardIds,
            Divisions = divisionIds
        };
    }

    private async Task<Guid?> ResolveUserIdAsync(GroupMemberType memberType, Guid memberId)
    {
        if (memberType == GroupMemberType.Employee)
        {
            var employee = await _employeeRepository.FindAsync(memberId);
            return employee?.UserId;
        }

        // Students typically don't have UserId, return null
        return null;
    }

    private static string MapStudentRuleField(StudentRuleField field)
    {
        return field switch
        {
            StudentRuleField.Class => "StandardId",
            StudentRuleField.Section => "DivisionId",
            StudentRuleField.Gender => "AdditionalDetail.Gender",
            StudentRuleField.AdmissionStatus => "Status",
            StudentRuleField.Name => "FirstName", // Will also need to handle LastName
            _ => string.Empty
        };
    }

    private static string MapEmployeeRuleField(EmployeeRuleField field)
    {
        return field switch
        {
            EmployeeRuleField.Department => "Department",
            EmployeeRuleField.Role => "RoleId",
            EmployeeRuleField.EmploymentType => "EmployeeGroup",
            EmployeeRuleField.Name => "FirstName", // Will also need to handle LastName
            EmployeeRuleField.Gender => "GenderType",
            _ => string.Empty
        };
    }

    #endregion
}
