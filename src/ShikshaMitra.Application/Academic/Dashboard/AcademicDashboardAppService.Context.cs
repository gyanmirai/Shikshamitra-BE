using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ShikshaMitra.Academic.ClassTeacher;
using Volo.Abp.Authorization;

namespace ShikshaMitra.Academic.Dashboard;

public partial class AcademicDashboardAppService
{
    private static readonly string[] AdminDashboardRoles =
    [
        "admin",
        "administrator",
        "super admin"
    ];

    private static readonly string[] LeadershipDashboardRoles =
    [
        "principal",
        "vice principal",
        "hod",
        "head of department",
        "academic coordinator"
    ];

    private static readonly string[] TeacherDashboardRoles =
    [
        "teacher",
        "class teacher"
    ];

    public async Task<AcademicDashboardContextDto> GetContextAsync()
    {
        if (!CurrentUser.Id.HasValue)
        {
            throw new AbpAuthorizationException("A signed-in user is required to load the academic dashboard.");
        }

        var normalizedRoles = CurrentUser.Roles
            .Select(NormalizeDashboardRole)
            .Where(role => !string.IsNullOrWhiteSpace(role))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var employeeQuery = await _employeeRepository.GetQueryableAsync();
        var employeeIds = await AsyncExecuter.ToListAsync(employeeQuery
            .Where(employee => employee.UserId == CurrentUser.Id.Value)
            .Select(employee => employee.Id));

        var assignments = new List<AcademicDashboardClassAssignmentDto>();
        if (employeeIds.Count > 0)
        {
            var assignmentQuery = await _classTeacherRepository.GetQueryableAsync();
            var activeAssignments = await AsyncExecuter.ToListAsync(assignmentQuery
                .Where(assignment =>
                    employeeIds.Contains(assignment.TeacherId) &&
                    assignment.Status == ClassTeacherAssignmentStatus.Active)
                .Select(assignment => new
                {
                    assignment.Id,
                    assignment.AcademicYearId,
                    assignment.StandardId,
                    assignment.DivisionId
                }));

            var standardIds = activeAssignments.Select(assignment => assignment.StandardId).Distinct().ToList();
            var divisionIds = activeAssignments.Select(assignment => assignment.DivisionId).Distinct().ToList();

            var standards = standardIds.Count == 0
                ? []
                : await _standardRepository.GetListAsync(standard => standardIds.Contains(standard.Id));
            var divisions = divisionIds.Count == 0
                ? []
                : await _divisionRepository.GetListAsync(division => divisionIds.Contains(division.Id));
            var standardNames = standards.ToDictionary(standard => standard.Id, standard => standard.Name);
            var divisionNames = divisions.ToDictionary(division => division.Id, division => division.Name);

            assignments = activeAssignments.Select(assignment =>
            {
                var standardName = standardNames.GetValueOrDefault(assignment.StandardId, "Class");
                var divisionName = divisionNames.GetValueOrDefault(assignment.DivisionId, "Division");
                return new AcademicDashboardClassAssignmentDto
                {
                    Id = assignment.Id,
                    AcademicYearId = assignment.AcademicYearId,
                    StandardId = assignment.StandardId,
                    StandardName = standardName,
                    DivisionId = assignment.DivisionId,
                    DivisionName = divisionName,
                    DisplayName = $"{standardName} - {divisionName}"
                };
            }).OrderBy(assignment => assignment.DisplayName).ToList();
        }

        var availableModes = new List<string>();
        AddModeWhenRoleMatches(availableModes, "admin", normalizedRoles, AdminDashboardRoles);
        AddModeWhenRoleMatches(availableModes, "leadership", normalizedRoles, LeadershipDashboardRoles);
        if (assignments.Count > 0)
        {
            availableModes.Add("classTeacher");
        }
        AddModeWhenRoleMatches(availableModes, "teacher", normalizedRoles, TeacherDashboardRoles);

        if (assignments.Count > 0 && !availableModes.Contains("teacher"))
        {
            availableModes.Add("teacher");
        }

        if (availableModes.Count == 0)
        {
            availableModes.Add("common");
        }

        return new AcademicDashboardContextDto
        {
            DefaultMode = ResolveDefaultDashboardMode(availableModes),
            AvailableModes = availableModes,
            DisplayName = CurrentUser.Name ?? string.Empty,
            ClassAssignments = assignments
        };
    }

    private static void AddModeWhenRoleMatches(
        ICollection<string> modes,
        string mode,
        IReadOnlySet<string> currentRoles,
        IEnumerable<string> matchingRoles)
    {
        if (matchingRoles.Any(currentRoles.Contains) && !modes.Contains(mode))
        {
            modes.Add(mode);
        }
    }

    private static string ResolveDefaultDashboardMode(IReadOnlyCollection<string> modes)
    {
        string[] priority = ["admin", "leadership", "classTeacher", "teacher", "common"];
        return priority.First(modes.Contains);
    }

    private static string NormalizeDashboardRole(string role) =>
        string.Join(' ', Regex.Replace(role.Trim(), "([a-z])([A-Z])", "$1 $2")
            .Replace('_', ' ')
            .Replace('-', ' ')
            .Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .ToLowerInvariant();
}
