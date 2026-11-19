using System.Threading.Tasks;

namespace ShikshaMitra.Students.StudentEnrollments.Onboarding;

public partial class StudentOnboardingAppService
{
	// Login creation and email delivery now require an explicit per-student staff command.
	// Keep these lifecycle hooks harmless for old cases and existing activation callers.
	private static Task PrepareAutomaticPortalAccountsAsync(StudentOnboardingCase item) => Task.CompletedTask;
	private static Task QueueAutomaticCredentialEmailsAsync(StudentOnboardingCase item) => Task.CompletedTask;
}
