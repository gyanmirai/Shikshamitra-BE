using System;
using System.Threading.Tasks;

namespace ShikshaMitra.Students.StudentEnrollments.Onboarding;

public record StudentCredentialEmailEligibility(Guid CaseId, Guid StudentId, Guid? LinkId, Guid? UserId,
	string Email, string StudentName, bool EmailEnabled, bool IsReady, string? BlockedReason,
	string LoginUrl, string SchoolName);

public interface IStudentCredentialEmailEligibilityResolver
{
	Task<StudentCredentialEmailEligibility> ResolveAsync(Guid caseId, string accountType, bool enforceStaffScope = true);
}
