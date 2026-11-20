using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Autofac;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Fact]
	public Task Terminal_approval_completes_without_loading_unrelated_stage_runtimes_or_routing_facts() =>
		RunCertificationScenarioAsync(async () =>
		{
			var fixture = await CreateCertificationFixtureAsync("TerminalDependencies", completeWorkflow: false);
			var application = await _applicationAppService.ApproveAsync(fixture.Application.Id,
				Decision(fixture.Application, "QA final approval"));
			var applications = GetRequiredService<AdmissionApplicationReadService>();
			var workflows = GetRequiredService<AdmissionApplicationWorkflowReadService>();
			using var scope = ServiceProvider.GetRequiredService<ILifetimeScope>().BeginLifetimeScope(builder =>
			{
				// Keep the real authorized readers; isolate the advancement helpers only.
				builder.RegisterInstance(applications).ExternallyOwned();
				builder.RegisterInstance(workflows).ExternallyOwned();
				foreach (var type in new[] { typeof(AdmissionFormStageRuntimeService), typeof(AdmissionInquiryRuntimeService),
					typeof(AdmissionEligibilityRuntimeService), typeof(AdmissionReviewRuntimeService),
					typeof(AdmissionOfferRuntimeService), typeof(AdmissionWorkflowRuntimeValueService) })
					builder.Register<object>(_ => throw new InvalidOperationException($"Unused terminal routing dependency: {type.Name}"))
						.As(type);
			});
			await WithUnitOfWorkAsync(async () =>
			{
				await scope.Resolve<AdmissionApplicationWorkflowService>().AdvanceAsync(application.Id, new()
				{
					ExpectedCurrentStageInstanceId = application.CurrentStageInstanceId!.Value,
					ConcurrencyStamp = application.ConcurrencyStamp,
					IdempotencyKey = "terminal-dependency-regression"
				});
			});
			var saved = await _applicationAppService.GetWorkflowAsync(application.Id);
			saved.Status.ShouldBe(AdmissionWorkflowInstanceStatus.Completed);
			saved.CurrentStageInstanceId.ShouldBeNull();
			var history = saved.History.Single(item => item.FromStageInstanceId == application.CurrentStageInstanceId);
			history.Action.ShouldBe(AdmissionWorkflowTransitionAction.Completed);
			history.ToStageInstanceId.ShouldBeNull();
			history.TransitionDefinitionId.ShouldBeNull();
			using var snapshot = JsonDocument.Parse(history.ConditionEvaluationSnapshotJson!);
			snapshot.RootElement.GetProperty("AssessedValues").ValueKind.ShouldBe(JsonValueKind.Null);
			(await WithUnitOfWorkAsync(() => _historyRepository.CountAsync(item => item.ApplicationId == application.Id &&
				item.IdempotencyKey == "terminal-dependency-regression"))).ShouldBe(1);
			(await _applicationAppService.GetAsync(application.Id)).Status.ShouldBe(AdmissionApplicationStatus.Approved);
		});

	[Fact]
	public Task Terminal_approval_still_requires_a_recorded_admission_decision() =>
		RunCertificationScenarioAsync(async () =>
		{
			var fixture = await CreateCertificationFixtureAsync("TerminalUndecided", completeWorkflow: false);
			var application = fixture.Application;
			(await Should.ThrowAsync<BusinessException>(() => _applicationAppService.AdvanceWorkflowAsync(application.Id, new()
			{
				ExpectedCurrentStageInstanceId = application.CurrentStageInstanceId!.Value,
				ConcurrencyStamp = application.ConcurrencyStamp,
				IdempotencyKey = "terminal-without-decision"
			}))).Code.ShouldBe(AdmissionApplicationErrorCodes.StageCannotAdvance);
			var saved = await _applicationAppService.GetWorkflowAsync(application.Id);
			saved.Status.ShouldBe(AdmissionWorkflowInstanceStatus.Active);
			saved.CurrentStageInstanceId.ShouldBe(application.CurrentStageInstanceId);
			saved.History.ShouldNotContain(item => item.FromStageInstanceId == application.CurrentStageInstanceId);
		});
}
