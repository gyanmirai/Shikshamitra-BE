using System;
using System.Threading.Tasks;

namespace ShikshaMitra.Students.StudentEnrollments.Onboarding;

public record StudentCredentialEmailEligibility(Guid CaseId, Guid StudentId, Guid? LinkId, Guid? UserId,
	string Email, string StudentName, bool EmailEnabled, bool IsReady, string? BlockedReason,
	string LoginUrl, string SchoolName, bool WaitingForCompletion = false, string? Relationship = null);

public interface IStudentCredentialEmailEligibilityResolver
{
	Task<StudentCredentialEmailEligibility> ResolveAsync(Guid caseId, string accountType, bool enforceStaffScope = true);
	Task<StudentCredentialEmailEligibility> ResolveExplicitAsync(Guid caseId, string accountType, bool enforceStaffScope = true);
}
