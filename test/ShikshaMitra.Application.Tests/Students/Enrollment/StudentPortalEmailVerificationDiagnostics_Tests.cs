using System;
using System.Threading.Tasks;
using NSubstitute;
using ShikshaMitra.Students.Portal;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using Shouldly;
using Volo.Abp.Emailing;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Fact]
	public Task Portal_email_verification_job_reports_safe_stage_without_retaining_transport_secrets() => RunCertificationScenarioAsync(async () =>
	{
		var (fixture, link) = await LinkOnlyEmailOwnerForTestAsync("father");
		var service = GetRequiredService<IStudentOnboardingAppService>();
		var workspace = await service.GetCredentialsAsync(fixture.Case.Id);
		var owner = System.Linq.Enumerable.Single(workspace.Recipients, value => value.Relationship == "father");
		await service.UpdateCredentialsEmailAsync(fixture.Case.Id,
			EmailEdit(workspace, owner, $"verification-diagnostic-{Guid.NewGuid():N}@example.test"));
		var (dispatcher, sender, args) = await EmailVerificationDispatcherForTestAsync(link.UserId);
		sender.SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<AdditionalEmailSendingArgs?>())
			.Returns(_ => Task.FromException(new InvalidOperationException(
				"SMTP password=diagnostic-secret https://school.example.test/confirm?token=diagnostic-token")));
		var job = new StudentPortalEmailChangeJob(dispatcher, GetRequiredService<ICurrentTenant>());
		var error = await Should.ThrowAsync<InvalidOperationException>(() => WithUnitOfWorkAsync(() => job.ExecuteAsync(args)));
		error.Message.ShouldContain("stage: transport");
		error.Message.ShouldContain(nameof(InvalidOperationException));
		error.InnerException.ShouldBeNull();
		error.ToString().ShouldNotContain("diagnostic-secret");
		error.ToString().ShouldNotContain("diagnostic-token");
		error.ToString().ShouldNotContain("https://school.example.test/confirm");
	});
}
