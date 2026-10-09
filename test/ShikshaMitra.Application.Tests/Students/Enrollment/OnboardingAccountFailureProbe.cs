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
        if (probe.CaseId == caseId)
        {
            probe.Attempts++;
            // Simulate an account database error that aborts its transaction, as PostgreSQL does.
            await work.Current!.RollbackAsync();
            throw new DbUpdateException("Simulated account preparation database failure.");
        }
        return await actual.ResolveAsync(caseId, accountType, enforceStaffScope);
    }
}
