using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using Volo.Abp.Uow;

namespace ShikshaMitra;

public sealed class OnboardingAccountFailureProbe
{
    public Guid? CaseId { get; set; }
    public int Attempts { get; set; }
}

public sealed class FaultingOnboardingCredentialEligibilityResolver(
    StudentOnboardingCredentialEligibilityResolver actual, OnboardingAccountFailureProbe probe, IUnitOfWorkManager work)
    : IStudentCredentialEmailEligibilityResolver
{
    public async Task<StudentCredentialEmailEligibility> ResolveAsync(Guid caseId, string accountType, bool enforceStaffScope = true)
    {
        await CheckFailureAsync(caseId);
        return await actual.ResolveAsync(caseId, accountType, enforceStaffScope);
    }

    public async Task<StudentCredentialEmailEligibility> ResolveExplicitAsync(Guid caseId, string accountType, bool enforceStaffScope = true)
    {
        await CheckFailureAsync(caseId);
        return await actual.ResolveExplicitAsync(caseId, accountType, enforceStaffScope);
    }

    private async Task CheckFailureAsync(Guid caseId)
    {
        if (probe.CaseId == caseId)
        {
            probe.Attempts++;
            // Simulate an account database error that aborts its transaction, as PostgreSQL does.
            await work.Current!.RollbackAsync();
            throw new DbUpdateException("Simulated account preparation database failure.");
        }
    }
}
