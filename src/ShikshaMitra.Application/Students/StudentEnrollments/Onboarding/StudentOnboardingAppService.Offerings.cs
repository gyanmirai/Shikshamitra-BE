using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students.StudentEnrollments.Admissions;

namespace ShikshaMitra.Students.StudentEnrollments.Onboarding;

public partial class StudentOnboardingAppService
{
    [Authorize(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.View)]
    public async Task<IReadOnlyList<OnboardingAvailableOfferingDto>> GetAvailableOfferingsAsync(Guid? academicYearId)
    {
        var scope = LazyServiceProvider.LazyGetRequiredService<StudentScopeAccessService>();
        if (academicYearId.HasValue)
            await scope.EnsureCanAccessAcademicYearAsync(academicYearId.Value, nameof(GetAvailableOfferingsAsync));
        var yearScope = await scope.GetCurrentUserAcademicYearScopeAsync();
        var yearIds = yearScope.AcademicYearIds.ToArray();
        var reader = LazyServiceProvider.LazyGetRequiredService<AdmissionOfferingReadService>();
        var cycles = await cycleRepository.GetListAsync(c =>
            (!academicYearId.HasValue || c.AcademicYearId == academicYearId.Value) &&
            (yearScope.HasAllAcademicYears || Enumerable.Contains(yearIds, c.AcademicYearId)));
        // Reuse the tenant and academic-scope reader without requiring a separate Admissions menu permission.
        var offerings = (await reader.GetCatalogueAsync(cycles.Select(cycle => cycle.Id).ToArray()))
            .ToLookup(item => item.Row.AdmissionCycleId);
        var result = new List<OnboardingAvailableOfferingDto>();
        foreach (var cycle in cycles)
        {
            foreach (var item in offerings[cycle.Id])
            {
                var row = item.Row;
                result.Add(new OnboardingAvailableOfferingDto
                {
                    Id = row.Id, Name = row.Name, Code = row.Code, AdmissionCycleId = row.AdmissionCycleId,
                    AdmissionCycleName = row.AdmissionCycleName, AcademicYearId = cycle.AcademicYearId,
                    CampusId = row.CampusId, CampusName = row.CampusName, BoardId = row.BoardId, BoardName = row.BoardName,
                    MediumId = row.MediumId, MediumName = row.MediumName, StageId = row.StageId, StageName = row.StageName,
                    StandardId = row.StandardId, StandardName = row.StandardName, Track = row.Track, EntryMode = row.EntryMode,
                    IsActive = row.IsActive, WorkflowName = row.WorkflowName, WorkflowVersion = row.WorkflowVersion,
                    CoreFieldPolicyVersion = row.CoreFieldPolicyVersion,
                    WorkflowDefinitionId = item.WorkflowDefinitionId,
                    EffectiveOpenDate = row.EffectiveOpenDate, EffectiveCloseDate = row.EffectiveCloseDate,
                    AvailabilityStatus = row.AvailabilityStatus, ConfigurationStatus = row.ConfigurationStatus, ApplicationCount = row.ApplicationCount
                });
            }
        }
        return result.OrderBy(o => o.Name).ToList();
    }
}
