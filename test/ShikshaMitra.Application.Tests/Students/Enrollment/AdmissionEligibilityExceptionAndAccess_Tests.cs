using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using ShikshaMitra.Security.UserScope;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.Security.Claims;
using Volo.Abp.Validation;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Fact]
	public Task Eligibility_exception_requires_reasons_and_a_separate_reviewer_preserving_the_failed_evaluation() =>
		RunCertificationScenarioAsync(async () =>
		{
			var fixture = await CreateEligibilityFixtureAsync(EligibilityPolicyJson([EligibilityAgeCriterion()], true), new DateTime(2022, 4, 1));
			var service = GetRequiredService<IAdmissionEligibilityAppService>();
			var state = await EvaluateEligibilityAsync(fixture);
			var evaluationId = state.LatestEvaluation!.Id;
			await Should.ThrowAsync<AbpValidationException>(() => service.RequestExceptionAsync(fixture.ApplicationId, fixture.StageId, evaluationId,
				new() { ConcurrencyStamp = state.ConcurrencyStamp, Reason = " " }));
			state = await service.RequestExceptionAsync(fixture.ApplicationId, fixture.StageId, evaluationId,
				new() { ConcurrencyStamp = state.ConcurrencyStamp, Reason = "Documented grade placement exception requested." });
			var request = state.LatestEvaluation!.Exceptions.Single();
			request.RequestedBy.ShouldBe(TestUserId);
			request.Status.ShouldBe("pending");
			state.CanComplete.ShouldBeFalse();
			await Should.ThrowAsync<BusinessException>(() => service.ReviewExceptionAsync(fixture.ApplicationId, fixture.StageId, request.Id,
				new() { ConcurrencyStamp = state.ConcurrencyStamp, Decision = "approved", Reason = "The requester cannot approve their own exception." }));
			var reviewerId = Guid.NewGuid();
			await _scopeRepository.InsertAsync(new UserScopeAssignment(Guid.NewGuid(), TestTenantId, userId: reviewerId, description: "Eligibility reviewer scope"), autoSave: true);
			using (_principalAccessor.Change(EligibilityPrincipal(reviewerId, "eligibility-reviewer")))
			{
				await Should.ThrowAsync<AbpValidationException>(() => service.ReviewExceptionAsync(fixture.ApplicationId, fixture.StageId, request.Id,
					new() { ConcurrencyStamp = state.ConcurrencyStamp, Decision = "approved", Reason = " " }));
				state = await service.ReviewExceptionAsync(fixture.ApplicationId, fixture.StageId, request.Id,
					new() { ConcurrencyStamp = state.ConcurrencyStamp, Decision = "rejected", Reason = "Supporting placement evidence is insufficient." });
			}
			state.Status.ShouldBe("ineligible");
			state.CanComplete.ShouldBeFalse();
			state = await service.RequestExceptionAsync(fixture.ApplicationId, fixture.StageId, evaluationId,
				new() { ConcurrencyStamp = state.ConcurrencyStamp, Reason = "New school placement evidence supplied." });
			var replacementRequest = state.LatestEvaluation!.Exceptions.Single(item => item.Status == "pending");
			using (_principalAccessor.Change(EligibilityPrincipal(reviewerId, "eligibility-reviewer")))
				state = await service.ReviewExceptionAsync(fixture.ApplicationId, fixture.StageId, replacementRequest.Id,
					new() { ConcurrencyStamp = state.ConcurrencyStamp, Decision = "approved", Reason = "Approved after independently reviewing placement evidence." });
			state.Status.ShouldBe("exceptionApproved");
			state.CanComplete.ShouldBeTrue();
			state.LatestEvaluation!.Outcome.ShouldBe("ineligible");
			var history = (await service.GetHistoryAsync(fixture.ApplicationId, fixture.StageId)).Single();
			history.Criteria.Single().Outcome.ShouldBe("failed");
			history.Exceptions.Count.ShouldBe(2);
			history.Exceptions.Single(item => item.Id == request.Id).Status.ShouldBe("rejected");
			var approved = history.Exceptions.Single(item => item.Id == replacementRequest.Id);
			approved.ReviewedBy.ShouldBe(reviewerId);
			approved.ReviewReason.ShouldBe("Approved after independently reviewing placement evidence.");
			approved.ReviewedAt.ShouldNotBeNull();
			await UpdateEligibilityApplicantBirthDateAsync(fixture.ApplicantId, new DateTime(2023, 4, 1));
			var stale = await service.GetAsync(fixture.ApplicationId, fixture.StageId);
			stale.IsStale.ShouldBeTrue();
			stale.CanComplete.ShouldBeFalse();
			stale.CanRequestException.ShouldBeFalse();
		});

	[Fact]
	public Task Eligibility_exception_approval_requires_the_configured_role_even_for_an_otherwise_authorized_reviewer() =>
		RunCertificationScenarioAsync(async () =>
		{
			const string approvalRole = "eligibility-principal";
			await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<IdentityRole, Guid>>().InsertAsync(
				new IdentityRole(Guid.NewGuid(), approvalRole, TestTenantId), autoSave: true));
			var fixture = await CreateEligibilityFixtureAsync(EligibilityPolicyJson([EligibilityAgeCriterion()], true, approvalRole), new DateTime(2022, 4, 1));
			var service = GetRequiredService<IAdmissionEligibilityAppService>();
			var state = await EvaluateEligibilityAsync(fixture);
			state = await service.RequestExceptionAsync(fixture.ApplicationId, fixture.StageId, state.LatestEvaluation!.Id,
				new() { ConcurrencyStamp = state.ConcurrencyStamp, Reason = "Principal approval is required for this placement exception." });
			var requestId = state.LatestEvaluation!.Exceptions.Single().Id;
			var reviewerId = Guid.NewGuid();
			await _scopeRepository.InsertAsync(new UserScopeAssignment(Guid.NewGuid(), TestTenantId, userId: reviewerId, description: "Scoped role reviewer"), autoSave: true);
			using (_principalAccessor.Change(EligibilityPrincipal(reviewerId, "reviewer-with-wrong-role")))
			{
				(await service.GetAsync(fixture.ApplicationId, fixture.StageId)).CanReviewException.ShouldBeFalse();
				var denied = await Should.ThrowAsync<BusinessException>(() => service.ReviewExceptionAsync(fixture.ApplicationId, fixture.StageId, requestId,
					new() { ConcurrencyStamp = state.ConcurrencyStamp, Decision = "approved", Reason = "General access must not bypass the configured approval role." }));
				denied.Code.ShouldBe(AdmissionEligibilityErrorCodes.ExceptionNotAllowed);
				(await service.GetHistoryAsync(fixture.ApplicationId, fixture.StageId)).Single().Exceptions.Single().Status.ShouldBe("pending");
			}
			using (_principalAccessor.Change(EligibilityPrincipal(reviewerId, "configured-principal", approvalRole)))
			{
				(await service.GetAsync(fixture.ApplicationId, fixture.StageId)).CanReviewException.ShouldBeTrue();
				state = await service.ReviewExceptionAsync(fixture.ApplicationId, fixture.StageId, requestId,
					new() { ConcurrencyStamp = state.ConcurrencyStamp, Decision = "approved", Reason = "Principal approved the documented placement exception." });
			}
			state.Status.ShouldBe("exceptionApproved");
			state.LatestEvaluation!.Exceptions.Single().ReviewedBy.ShouldBe(reviewerId);
		});

	[Fact]
	public Task Eligibility_reads_evaluation_and_exception_actions_deny_foreign_tenants_stages_and_user_scope() =>
		RunCertificationScenarioAsync(async () =>
		{
			var policy = EligibilityPolicyJson([EligibilityAgeCriterion()], true);
			var fixture = await CreateEligibilityFixtureAsync(policy, new DateTime(2022, 4, 1));
			var other = await CreateEligibilityFixtureAsync(policy, new DateTime(2022, 4, 1));
			var service = GetRequiredService<IAdmissionEligibilityAppService>();
			var state = await EvaluateEligibilityAsync(fixture);
			state = await service.RequestExceptionAsync(fixture.ApplicationId, fixture.StageId, state.LatestEvaluation!.Id,
				new() { ConcurrencyStamp = state.ConcurrencyStamp, Reason = "Placement exception for access-control verification." });
			var request = state.LatestEvaluation!.Exceptions.Single();
			await Should.ThrowAsync<EntityNotFoundException>(() => service.GetAsync(fixture.ApplicationId, other.StageId));
			using (_principalAccessor.Change(EligibilityPrincipal(Guid.NewGuid(), "staff-without-eligibility-scope")))
			{
				await Should.ThrowAsync<EntityNotFoundException>(() => service.GetAsync(fixture.ApplicationId, fixture.StageId));
				await Should.ThrowAsync<EntityNotFoundException>(() => service.GetHistoryAsync(fixture.ApplicationId, fixture.StageId));
				await Should.ThrowAsync<EntityNotFoundException>(() => service.EvaluateAsync(fixture.ApplicationId, fixture.StageId, new() { ConcurrencyStamp = state.ConcurrencyStamp }));
				await Should.ThrowAsync<EntityNotFoundException>(() => service.RequestExceptionAsync(fixture.ApplicationId, fixture.StageId, state.LatestEvaluation.Id,
					new() { ConcurrencyStamp = state.ConcurrencyStamp, Reason = "Unauthorized exception request." }));
				await Should.ThrowAsync<EntityNotFoundException>(() => service.ReviewExceptionAsync(fixture.ApplicationId, fixture.StageId, request.Id,
					new() { ConcurrencyStamp = state.ConcurrencyStamp, Decision = "approved", Reason = "Unauthorized approval." }));
			}
			using (_currentTenant.Change(Guid.NewGuid()))
			{
				await Should.ThrowAsync<EntityNotFoundException>(() => service.GetAsync(fixture.ApplicationId, fixture.StageId));
				await Should.ThrowAsync<EntityNotFoundException>(() => service.GetHistoryAsync(fixture.ApplicationId, fixture.StageId));
				await Should.ThrowAsync<EntityNotFoundException>(() => service.EvaluateAsync(fixture.ApplicationId, fixture.StageId, new() { ConcurrencyStamp = state.ConcurrencyStamp }));
				await Should.ThrowAsync<EntityNotFoundException>(() => service.ReviewExceptionAsync(fixture.ApplicationId, fixture.StageId, request.Id,
					new() { ConcurrencyStamp = state.ConcurrencyStamp, Decision = "approved", Reason = "Foreign-tenant approval." }));
			}
			(await service.GetHistoryAsync(fixture.ApplicationId, fixture.StageId)).Single().Exceptions.Single().Status.ShouldBe("pending");
		});

	private static ClaimsPrincipal EligibilityPrincipal(Guid userId, string name, string role = "admin") => new(new ClaimsIdentity(
	[
		new Claim(AbpClaimTypes.UserId, userId.ToString()), new Claim(AbpClaimTypes.UserName, name),
		new Claim(AbpClaimTypes.Role, role)
	], "Test"));
}
