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
        var result = new List<OnboardingAvailableOfferingDto>();
        foreach (var cycle in cycles)
        {
            var skip = 0;
            while (true)
            {
                // Reuse the tenant and academic-scope reader without requiring the separate Admissions menu permission.
                var page = await reader.GetListAsync(new AdmissionOfferingListInput { AdmissionCycleId = cycle.Id, SkipCount = skip, MaxResultCount = 1000 });
                var ids = page.Items.Select(o => o.Id).ToArray();
                var offeringById = (await offeringRepository.GetListAsync(o => Enumerable.Contains(ids, o.Id))).ToDictionary(o => o.Id);
                foreach (var row in page.Items)
                    result.Add(new OnboardingAvailableOfferingDto
                    {
                        Id = row.Id, Name = row.Name, Code = row.Code, AdmissionCycleId = row.AdmissionCycleId,
                        AdmissionCycleName = row.AdmissionCycleName, AcademicYearId = cycle.AcademicYearId,
                        CampusId = row.CampusId, CampusName = row.CampusName, BoardId = row.BoardId, BoardName = row.BoardName,
                        MediumId = row.MediumId, MediumName = row.MediumName, StageId = row.StageId, StageName = row.StageName,
                        StandardId = row.StandardId, StandardName = row.StandardName, Track = row.Track, EntryMode = row.EntryMode,
                        IsActive = row.IsActive, WorkflowName = row.WorkflowName, WorkflowVersion = row.WorkflowVersion,
                        WorkflowDefinitionId = offeringById[row.Id].WorkflowDefinitionId,
                        EffectiveOpenDate = row.EffectiveOpenDate, EffectiveCloseDate = row.EffectiveCloseDate,
                        AvailabilityStatus = row.AvailabilityStatus, ConfigurationStatus = row.ConfigurationStatus, ApplicationCount = row.ApplicationCount
                    });
                skip += page.Items.Count;
                if (skip >= page.TotalCount || page.Items.Count == 0) break;
            }
        }
        return result.OrderBy(o => o.Name).ToList();
    }
}
