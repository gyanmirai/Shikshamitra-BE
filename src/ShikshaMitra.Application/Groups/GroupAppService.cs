using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Application.Contracts.Groups;
using ShikshaMitra.Groups;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Students;
using ShikshaMitra.TenantSettings.Divisions;
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
public class GroupAppService : ApplicationService, IGroupAppService
{
    private readonly IRepository<Group, Guid> _repository;
    private readonly IGroupRepository _groupRepository;
    private readonly IRepository<Student, Guid> _studentRepository;
    private readonly IRepository<Employee, Guid> _employeeRepository;
    private readonly IRepository<IdentityRole, Guid> _roleRepository;
    private readonly IRepository<Standard, Guid> _standardRepository;
    private readonly IRepository<Division, Guid> _divisionRepository;

    public GroupAppService(
        IRepository<Group, Guid> repository,
        IGroupRepository groupRepository,
        IRepository<Student, Guid> studentRepository,
        IRepository<Employee, Guid> employeeRepository,
        IRepository<IdentityRole, Guid> roleRepository,
        IRepository<Standard, Guid> standardRepository,
        IRepository<Division, Guid> divisionRepository)
    {
        _repository = repository;
        _groupRepository = groupRepository;
        _studentRepository = studentRepository;
        _employeeRepository = employeeRepository;
        _roleRepository = roleRepository;
        _standardRepository = standardRepository;
        _divisionRepository = divisionRepository;
    }

    #region CRUD Operations

    public async Task<GroupDto> GetAsync(Guid id)
    {
        var group = await _groupRepository.GetWithDetailsAsync(id);
        if (group == null)
            throw new UserFriendlyException($"Group with ID {id} not found.");

        var dto = MapToDto(group);
        dto.MemberCount = await GetGroupMemberCountAsync(id);
        return dto;
    }

    public async Task<PagedResultDto<GroupDto>> GetListAsync(GetGroupListRequestDto input)
    {
        var totalCount = await _groupRepository.GetCountAsync(
            input.AcademicYearId,
            input.Filter,
            input.SelectionMode);

        var groups = await _groupRepository.GetListWithDetailsAsync(
            input.AcademicYearId,
            input.Filter,
            input.SelectionMode,
            input.SkipCount,
            input.MaxResultCount);

        var dtos = new List<GroupDto>();
        foreach (var group in groups)
        {
            var dto = MapToDto(group);
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

        var group = input.SelectionMode == GroupSelectionMode.RuleBased
            ? Group.CreateRuleBased(
                GuidGenerator.Create(),
                CurrentTenant.Id,
                academicYearId,
                input.Name,
                input.Description,
                input.SelectedDivisionIds,
                input.RoleIds)
            : Group.CreateNameBased(
                GuidGenerator.Create(),
                CurrentTenant.Id,
                academicYearId,
                input.Name,
                input.Description);

        // Add student rules
        foreach (var rule in input.StudentRules)
        {
            group.AddStudentRule(new GroupStudentRule(
                GuidGenerator.Create(),
                group.Id,
                rule.Field,
                rule.Operator,
                rule.Value));
        }

        // Add employee rules
        foreach (var rule in input.EmployeeRules)
        {
            group.AddEmployeeRule(new GroupEmployeeRule(
                GuidGenerator.Create(),
                group.Id,
                rule.Field,
                rule.Operator,
                rule.Value));
        }

        // Add members (for name-based groups)
        foreach (var member in input.Members)
        {
            var userId = member.UserId ?? await ResolveUserIdAsync(member.MemberType, member.MemberId);
            group.AddMember(new GroupMember(
                GuidGenerator.Create(),
                group.Id,
                member.MemberType,
                member.MemberId,
                userId));
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

        group.Update(input.Name, input.Description, input.SelectedDivisionIds, input.RoleIds);

        // Update student rules
        group.ClearStudentRules();
        foreach (var rule in input.StudentRules)
        {
            group.AddStudentRule(new GroupStudentRule(
                GuidGenerator.Create(),
                group.Id,
                rule.Field,
                rule.Operator,
                rule.Value));
        }

        // Update employee rules
        group.ClearEmployeeRules();
        foreach (var rule in input.EmployeeRules)
        {
            group.AddEmployeeRule(new GroupEmployeeRule(
                GuidGenerator.Create(),
                group.Id,
                rule.Field,
                rule.Operator,
                rule.Value));
        }

        // Update members (for name-based groups)
        group.ClearMembers();
        foreach (var member in input.Members)
        {
            var userId = member.UserId ?? await ResolveUserIdAsync(member.MemberType, member.MemberId);
            group.AddMember(new GroupMember(
                GuidGenerator.Create(),
                group.Id,
                member.MemberType,
                member.MemberId,
                userId));
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
                Field = (StudentRuleField)f.Field,
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
                Field = (EmployeeRuleField)f.Field,
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

    private async Task<int> GetGroupMemberCountInternalAsync(Group group)
    {
        var members = await ResolveGroupMembersAsync(group);
        return members.Count;
    }

    private async Task<List<Guid>> ResolveGroupMembersAsync(Group group)
    {
        if (group.SelectionMode == GroupSelectionMode.NameBased)
        {
            // For name-based groups, return stored member user IDs
            return group.Members
                .Where(m => m.UserId.HasValue)
                .Select(m => m.UserId!.Value)
                .Distinct()
                .ToList();
        }

        // For rule-based groups, evaluate rules and return matching user IDs
        var userIds = new HashSet<Guid>();

        // Get students matching academic selections and student rules
        var studentUserIds = await ResolveStudentMembersAsync(group);
        foreach (var id in studentUserIds)
            userIds.Add(id);

        // Get employees matching role selections and employee rules
        var employeeUserIds = await ResolveEmployeeMembersAsync(group);
        foreach (var id in employeeUserIds)
            userIds.Add(id);

        return userIds.ToList();
    }

    private async Task<List<Guid>> ResolveStudentMembersAsync(Group group)
    {
        // If no division selections and no student rules, skip students
        if (group.SelectedDivisionIds.Count == 0 && group.StudentRules.Count == 0)
            return new List<Guid>();

        var query = await _studentRepository.GetQueryableAsync();

        // Filter by academic year
        query = query.Where(s => s.AcademicYearId == group.AcademicYearId);

        // Apply division selections
        if (group.SelectedDivisionIds.Count > 0)
        {
            query = query.Where(s =>
                s.DivisionId.HasValue && group.SelectedDivisionIds.Contains(s.DivisionId.Value));
        }

        // Apply student rules
        if (group.StudentRules.Count > 0)
        {
            var rules = group.StudentRules.Select(r => new StudentRuleDto
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
        // If no role selections and no employee rules, skip employees
        if (group.RoleIds.Count == 0 && group.EmployeeRules.Count == 0)
            return new List<Guid>();

        var query = await _employeeRepository.GetQueryableAsync();

        // Apply role selections
        if (group.RoleIds.Count > 0)
        {
            query = query.Where(e => group.RoleIds.Contains(e.RoleId));
        }

        // Apply employee rules
        if (group.EmployeeRules.Count > 0)
        {
            var rules = group.EmployeeRules.Select(r => new EmployeeRuleDto
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

    private GroupDto MapToDto(Group group)
    {
        return new GroupDto
        {
            Id = group.Id,
            TenantId = group.TenantId ?? Guid.Empty,
            AcademicYearId = group.AcademicYearId,
            Name = group.Name,
            Description = group.Description,
            SelectionMode = group.SelectionMode,
            SelectedDivisionIds = group.SelectedDivisionIds,
            RoleIds = group.RoleIds,
            StudentRules = group.StudentRules.Select(r => new StudentRuleDto
            {
                Id = r.Id,
                Field = r.Field,
                Operator = r.Operator,
                Value = r.Value
            }).ToList(),
            EmployeeRules = group.EmployeeRules.Select(r => new EmployeeRuleDto
            {
                Id = r.Id,
                Field = r.Field,
                Operator = r.Operator,
                Value = r.Value
            }).ToList(),
            Members = group.Members.Select(m => new GroupMemberDto
            {
                Id = m.Id,
                MemberType = m.MemberType,
                MemberId = m.MemberId,
                UserId = m.UserId
            }).ToList(),
            CreationTime = group.CreationTime,
            CreatorId = group.CreatorId,
            LastModificationTime = group.LastModificationTime,
            LastModifierId = group.LastModifierId
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
            _ => string.Empty
        };
    }

    #endregion
}
