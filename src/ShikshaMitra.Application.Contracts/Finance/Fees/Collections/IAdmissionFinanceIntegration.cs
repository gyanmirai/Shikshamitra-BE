using System;
using System.Threading.Tasks;

namespace ShikshaMitra.Finance.Fees.Collections;

/// <summary>
/// Internal Student lifecycle handoff. The authorized lifecycle owns identity/enrollment;
/// Finance links its existing evidence and never creates a second payment or due schedule.
/// </summary>
public interface IAdmissionFinanceIntegration
{
    /// <summary>Scoped, Finance-owned settlement decision for automatic Onboarding readiness; no monetary writes.</summary>
    Task<bool> IsSettledForOnboardingAsync(Guid applicationId);
    Task LinkFinalizedStudentAsync(Guid applicationId, Guid studentId);
    Task BindActivatedStudentAsync(Guid applicationId, Guid studentId, Guid enrollmentId);
}
