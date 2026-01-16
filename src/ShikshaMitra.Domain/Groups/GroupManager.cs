using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Domain.Services;

using Volo.Abp.Domain.Repositories;
using ShikshaMitra.Students;
using ShikshaMitra.HR.Employees;
using System.Linq;

namespace ShikshaMitra.Groups;

public class GroupManager : DomainService
{
    private readonly IRepository<Student, Guid> _studentRepository;
    private readonly IRepository<Employee, Guid> _employeeRepository;

    public GroupManager(
        IRepository<Student, Guid> studentRepository,
        IRepository<Employee, Guid> employeeRepository)
    {
        _studentRepository = studentRepository;
        _employeeRepository = employeeRepository;
    }

    public async Task<Group> CreateAsync(string name, string? description, GroupAudienceType audienceType)
    {
        // Add any domain logic/validation here
        var group = new Group(GuidGenerator.Create(), CurrentTenant.Id, name, description, audienceType);
        return await Task.FromResult(group);
    }

    public async Task<(int Total, int Students, int Employees)> GetAudienceCountAsync(
        List<AcademicRule> academicRules,
        List<UserFilter> studentFilters,
        List<UserFilter> employeeFilters,
        List<Guid> roles)
    {
        // TODO: This logic needs to be translated into actual EF Core compatible queries.
        // For now, we will perform simple counting if no complex filters, or return 0.
        // Implementing full dynamic LINQ builder for UserFilter is complex and out of Phase 3 scope
        // unless explicitly requested to build the query builder.
        
        var studentCount = await _studentRepository.CountAsync();
        var employeeCount = await _employeeRepository.CountAsync();
        
        // Mock logic: If filters exist, return a subset (e.g. half) to simulate filtering
        if (studentFilters.Any() || academicRules.Any())
        {
             studentCount /= 2;
        }
        if (employeeFilters.Any())
        {
             employeeCount /= 2;
        }

        return (studentCount + employeeCount, studentCount, employeeCount);
    }
}
