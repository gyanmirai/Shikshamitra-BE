using System;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Fact]
	public Task Review_reads_commands_and_queue_preserve_application_stage_tenant_and_user_scope_boundaries() =>
		RunCertificationScenarioAsync(async () =>
		{
			var fixture = await CreateReviewFixtureAsync();
			var other = await CreateReviewFixtureAsync();
			var service = GetRequiredService<IAdmissionReviewAppService>();
			var state = await service.GetAsync(fixture.ApplicationId, fixture.StageId);
			await Should.ThrowAsync<EntityNotFoundException>(() => service.GetAsync(fixture.ApplicationId, other.StageId));
			await Should.ThrowAsync<EntityNotFoundException>(() => service.SubmitAsync(fixture.ApplicationId, other.StageId,
				new() { ConcurrencyStamp = state.ConcurrencyStamp, Recommendation = "approve", Reason = "Wrong application stage." }));
			using (_principalAccessor.Change(EligibilityPrincipal(Guid.NewGuid(), "review-no-scope", "review-staff")))
			{
				await Should.ThrowAsync<EntityNotFoundException>(() => service.GetAsync(fixture.ApplicationId, fixture.StageId));
				await Should.ThrowAsync<EntityNotFoundException>(() => service.GetHistoryAsync(fixture.ApplicationId, fixture.StageId));
				await Should.ThrowAsync<EntityNotFoundException>(() => service.SaveDraftAsync(fixture.ApplicationId, fixture.StageId,
					new() { ConcurrencyStamp = state.ConcurrencyStamp, Notes = "Out-of-scope draft." }));
				await Should.ThrowAsync<EntityNotFoundException>(() => service.AssignAsync(fixture.ApplicationId, fixture.StageId,
					new() { ConcurrencyStamp = state.ConcurrencyStamp, Reason = "Out-of-scope assignment." }));
				(await service.GetQueueAsync(new())).TotalCount.ShouldBe(0);
			}
			using (_currentTenant.Change(Guid.NewGuid()))
			{
				await Should.ThrowAsync<EntityNotFoundException>(() => service.GetAsync(fixture.ApplicationId, fixture.StageId));
				await Should.ThrowAsync<EntityNotFoundException>(() => service.GetHistoryAsync(fixture.ApplicationId, fixture.StageId));
				await Should.ThrowAsync<EntityNotFoundException>(() => service.SubmitAsync(fixture.ApplicationId, fixture.StageId,
					new() { ConcurrencyStamp = state.ConcurrencyStamp, Recommendation = "approve", Reason = "Foreign tenant submit." }));
				(await service.GetQueueAsync(new())).TotalCount.ShouldBe(0);
			}
			(await service.GetHistoryAsync(fixture.ApplicationId, fixture.StageId)).ShouldBeEmpty();
		});

	[Fact]
	public Task Review_only_the_assigned_reviewer_with_the_configured_role_can_submit() =>
		RunCertificationScenarioAsync(async () =>
		{
			const string role = "admission-reviewer";
			var assigned = await CreateReviewUserAsync(role);
			var colleague = await CreateReviewUserAsync(role);
			var fixture = await CreateReviewFixtureAsync(ReviewPolicyJson(reviewerRole: role, assignedReviewerId: assigned));
			var service = GetRequiredService<IAdmissionReviewAppService>();
			using (_principalAccessor.Change(EligibilityPrincipal(colleague, "other-reviewer", role)))
			{
				var state = await service.GetAsync(fixture.ApplicationId, fixture.StageId);
				state.CanSubmit.ShouldBeFalse();
				(await Should.ThrowAsync<BusinessException>(() => SubmitReviewAsync(fixture)))
					.Code.ShouldBe(AdmissionReviewErrorCodes.ReviewerNotAllowed);
			}
			using (_principalAccessor.Change(EligibilityPrincipal(assigned, "assigned-with-wrong-role", "other-role")))
				(await Should.ThrowAsync<BusinessException>(() => SubmitReviewAsync(fixture)))
					.Code.ShouldBe(AdmissionReviewErrorCodes.ReviewerNotAllowed);
			using (_principalAccessor.Change(EligibilityPrincipal(assigned, "assigned-reviewer", role)))
			{
				var submitted = await SubmitReviewAsync(fixture);
				submitted.LatestRecord!.RecordedBy.ShouldBe(assigned);
				submitted.LatestRecord.AssignedReviewerId.ShouldBe(assigned);
			}
		});

	[Fact]
	public Task Review_existing_approval_requires_configured_role_and_a_different_actor_when_enabled() =>
		RunCertificationScenarioAsync(async () =>
		{
			const string role = "admission-principal";
			var principalId = await CreateReviewUserAsync(role);
			var fixture = await CreateReviewFixtureAsync(ReviewPolicyJson(independent: true, approverRole: role));
			var service = GetRequiredService<IAdmissionReviewAppService>();
			await SubmitReviewAsync(fixture);
			var application = await _applicationAppService.GetAsync(fixture.ApplicationId);
			using (_principalAccessor.Change(EligibilityPrincipal(TestUserId, "self-principal", role)))
			{
				(await service.GetAsync(application.Id, fixture.StageId)).Decision.CanApprove.ShouldBeFalse();
				(await Should.ThrowAsync<BusinessException>(() => _applicationAppService.ApproveAsync(application.Id,
					Decision(application, "Self approval must not bypass independent review."))))
					.Code.ShouldBe(AdmissionReviewErrorCodes.DecisionNotAllowed);
			}
			using (_principalAccessor.Change(EligibilityPrincipal(principalId, "principal-with-wrong-role", "other-role")))
				(await Should.ThrowAsync<BusinessException>(() => _applicationAppService.ApproveAsync(application.Id,
					Decision(application, "An unrelated role cannot approve."))))
					.Code.ShouldBe(AdmissionReviewErrorCodes.DecisionNotAllowed);
			using (_principalAccessor.Change(EligibilityPrincipal(principalId, "principal", role)))
				(await _applicationAppService.ApproveAsync(application.Id, Decision(application, "Independent final approval.")))
					.Status.ShouldBe(AdmissionApplicationStatus.Approved);
			var record = (await service.GetHistoryAsync(application.Id, fixture.StageId)).Single();
			record.RecordedBy.ShouldBe(TestUserId);
			record.Decision!.DecidedBy.ShouldBe(principalId);
		});

	[Fact]
	public Task Review_assignment_rejects_inactive_unscoped_and_wrong_role_users_without_replacing_the_owner() =>
		RunCertificationScenarioAsync(async () =>
		{
			const string role = "admission-assignee";
			var allowed = await CreateReviewUserAsync(role);
			var inactive = await CreateReviewUserAsync(role, active: false);
			var unscoped = await CreateReviewUserAsync(role, grantScope: false);
			var wrongRole = await CreateReviewUserAsync("other-reviewer");
			var fixture = await CreateReviewFixtureAsync(ReviewPolicyJson(reviewerRole: role, assignedReviewerId: allowed));
			var service = GetRequiredService<IAdmissionReviewAppService>();
			var initial = await service.GetAsync(fixture.ApplicationId, fixture.StageId);
			foreach (var id in new[] { inactive, unscoped, wrongRole, Guid.NewGuid() })
			{
				await Should.ThrowAsync<BusinessException>(() => service.AssignAsync(fixture.ApplicationId, fixture.StageId,
					new() { ConcurrencyStamp = initial.ConcurrencyStamp, AssignedReviewerId = id, Reason = "Attempt invalid reassignment." }));
				var state = await service.GetAsync(fixture.ApplicationId, fixture.StageId);
				state.AssignedReviewerId.ShouldBe(allowed);
				state.ConcurrencyStamp.ShouldBe(initial.ConcurrencyStamp);
			}
			(await service.GetHistoryAsync(fixture.ApplicationId, fixture.StageId)).ShouldBeEmpty();
		});
}
