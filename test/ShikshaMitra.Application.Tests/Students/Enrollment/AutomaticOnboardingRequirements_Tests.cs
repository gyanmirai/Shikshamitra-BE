using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Fact]
	public Task Automatic_onboarding_rejects_undetermined_document_requirements_atomically() =>
		RunCertificationScenarioAsync(async () =>
		{
			var requirement = new AdmissionDocumentRequirement { Code = "CONDITIONAL", Name = "Conditional certificate", Requirement = "conditional",
				ConditionJson = """{"field":"application.missingAdmissionFact","operator":"Equals","value":"Yes"}""" };
			var fixture = await CreateOnboardingDocumentFixtureAsync(HandoffPolicy(requirement with { Requirement = "required", ConditionJson = null }));
			var application = await CompleteOnboardingDecisionAsync(fixture.Application.Id);
			// Current authoring accepts required/optional documents. Restore a historical
			// published conditional policy directly so finalization must determine it safely.
			await WithUnitOfWorkAsync(async () =>
			{
				var runtime = await GetRequiredService<IRepository<AdmissionApplicationStageInstance, Guid>>().GetAsync(fixture.StageId);
				var definitions = GetRequiredService<IRepository<AdmissionWorkflowStageDefinition, Guid>>();
				var stage = await definitions.GetAsync(runtime.DefinitionStageId);
				stage.Update(stage.Code, stage.Name, stage.StageType, stage.DisplayOrder, stage.IsRequired,
					stage.AllowManualSkip, stage.Description,
					JsonSerializer.Serialize(new { documentVerification = HandoffPolicy(requirement) with { AllowWaivers = false } }),
					stage.IsEntryStage, stage.IsTerminalStage);
				await definitions.UpdateAsync(stage, true);
			});
			var before = await WithUnitOfWorkAsync(() => _studentRepository.CountAsync());
			(await Should.ThrowAsync<BusinessException>(() => FinalizeAsync(application, true, "automatic-undetermined")))
				.Code.ShouldBe(AdmissionDocumentErrorCodes.EvidenceRequired);
			(await WithUnitOfWorkAsync(() => _studentRepository.CountAsync())).ShouldBe(before);
			(await WithUnitOfWorkAsync(() => _onboardingCaseRepository.CountAsync(c => c.ApplicationId == application.Id))).ShouldBe(0);
			(await _applicationAppService.GetAsync(application.Id)).StudentId.ShouldBeNull();
		});

	[Fact]
	public Task Automatic_onboarding_access_save_preserves_dormant_stale_profile_links() =>
		RunCertificationScenarioAsync(async () =>
		{
			var fixture = await CreateOnboardingFlowFixtureAsync("DormantProfile", null);
			var cycle = await WithUnitOfWorkAsync(() => _cycleRepository.GetAsync(fixture.Offering.AdmissionCycleId));
			var service = GetRequiredService<IStudentOnboardingAppService>();
			var settings = (await service.GetSettingsAsync(cycle.AcademicYearId)).Single(s => s.AcademicYearId == cycle.AcademicYearId);
			var config = JsonNode.Parse(settings.ConfigurationJson)!.AsObject();
			var profile = config["onboardingProfiles"]![0]!;
			profile["offeringIds"] = new JsonArray(Guid.NewGuid().ToString());
			profile["applicability"]!["boardMode"] = "selected";
			profile["applicability"]!["boardIds"] = new JsonArray(Guid.NewGuid().ToString());
			var dormant = config["onboardingProfiles"]!.ToJsonString();
			config["readinessAccess"]!["guardianAccount"] = true;
			var saved = await service.SaveSettingsAsync(new() { AcademicYearId = cycle.AcademicYearId,
				ConfigurationJson = config.ToJsonString(), ConcurrencyStamp = settings.ConcurrencyStamp });
			JsonNode.Parse(saved.ConfigurationJson)!["onboardingProfiles"]!.ToJsonString().ShouldBe(dormant);
			var finalized = await FinalizeAsync(fixture.Application, true, "automatic-dormant-profile");
			(await service.GetAsync(finalized.OnboardingCaseId)).GuardianAccountRequired.ShouldBeTrue();
		});

	[Fact]
	public Task Automatic_onboarding_inherits_document_blockers_without_a_named_rule_or_offering_link() =>
		RunCertificationScenarioAsync(async () =>
		{
			var requirement = new AdmissionDocumentRequirement { Code = "BIRTH", Name = "Birth certificate" };
			var fixture = await CreateOnboardingDocumentFixtureAsync(HandoffPolicy(requirement));
			var application = await CompleteOnboardingDecisionAsync(fixture.Application.Id);
			var cycle = await WithUnitOfWorkAsync(() => _cycleRepository.GetAsync(fixture.Offering.AdmissionCycleId));
			await SaveAutomaticAccessAsync(cycle.AcademicYearId, student: false, guardian: true);
			var finalized = await FinalizeAsync(application, true, "automatic-documents");
			var service = GetRequiredService<IStudentOnboardingAppService>();
			var item = await service.GetAsync(finalized.OnboardingCaseId);
			item = await service.RefreshReadinessAsync(item.Id, item.ConcurrencyStamp);
			item.RequirementsNeedReview.ShouldBeFalse();
			item.HasDocumentRequirementsSnapshot.ShouldBeTrue();
			item.StudentAccountRequired.ShouldBeFalse();
			item.GuardianAccountRequired.ShouldBeTrue();
			item.Steps.Single(s => s.Key == StudentOnboardingStepKeys.Documents).Required.ShouldBeTrue();
			item.Steps.Single(s => s.Key == StudentOnboardingStepKeys.Documents).Status.ShouldBe(StudentOnboardingStepStatus.Blocked);
			item.Steps.ShouldNotContain(s => s.Key == StudentOnboardingStepKeys.ApprovalReview && s.Required);
			(await service.PreviewActivationAsync(item.Id)).LastError.ShouldNotBeNull().ShouldContain("Documents and compliance");
			(await Should.ThrowAsync<BusinessException>(() => service.ActivateAsync(item.Id,
				new() { ConcurrencyStamp = item.ConcurrencyStamp, IdempotencyKey = "automatic-documents-blocked" })))
				.Code.ShouldBe(StudentOnboardingErrorCodes.RequiredStepsIncomplete);
			(await WithUnitOfWorkAsync(() => _studentEnrollmentRepository.CountAsync(e => e.StudentId == finalized.StudentId))).ShouldBe(0);
		});

	[Fact]
	public Task Automatic_onboarding_adopts_pending_cases_once_and_updates_access_without_changing_documents() =>
		RunCertificationScenarioAsync(async () =>
		{
			var fixture = await CreateOnboardingFlowFixtureAsync("AutomaticPending", null);
			var cycle = await WithUnitOfWorkAsync(() => _cycleRepository.GetAsync(fixture.Offering.AdmissionCycleId));
			var finalized = await FinalizeAsync(fixture.Application, true, "automatic-pending");
			var service = GetRequiredService<IStudentOnboardingAppService>();
			var repo = GetRequiredService<IRepository<StudentOnboardingCase, Guid>>();
			await WithUnitOfWorkAsync(async () =>
			{
				var old = await repo.GetAsync(finalized.OnboardingCaseId);
				old.ExtraProperties.Remove("ReadinessRulesSnapshot");
				old.ExtraProperties.Remove("DocumentPoliciesSnapshot");
				old.ExtraProperties.Remove(AutomaticOnboardingReadinessRules.SnapshotVersionKey);
				await repo.UpdateAsync(old, true);
			});
			await SaveAutomaticAccessAsync(cycle.AcademicYearId, student: false, guardian: true);
			var first = await service.GetAsync(finalized.OnboardingCaseId);
			first.RequirementsNeedReview.ShouldBeFalse();
			first.GuardianAccountRequired.ShouldBeTrue();
			first.Events.Count(e => e.EventType == "AutomaticRequirementsAdopted").ShouldBe(1);
			var documents = await WithUnitOfWorkAsync(async () => (await repo.GetAsync(first.Id)).ExtraProperties["DocumentPoliciesSnapshot"]!.ToString());
			var repeated = await service.GetAsync(first.Id);
			repeated.ConcurrencyStamp.ShouldBe(first.ConcurrencyStamp);
			repeated.Events.Count(e => e.EventType == "AutomaticRequirementsAdopted").ShouldBe(1);
			await SaveAutomaticAccessAsync(cycle.AcademicYearId, student: true, guardian: false, sendGuardian: true);
			var changed = await service.GetAsync(first.Id);
			changed.StudentAccountRequired.ShouldBeTrue();
			changed.GuardianAccountRequired.ShouldBeFalse();
			changed.Events.Count(e => e.EventType == "AutomaticRequirementsAdopted").ShouldBe(2);
			await WithUnitOfWorkAsync(async () =>
			{
				var stored = await repo.GetAsync(first.Id);
				stored.ExtraProperties["DocumentPoliciesSnapshot"]!.ToString().ShouldBe(documents);
				using var rules = JsonDocument.Parse(stored.ExtraProperties["ReadinessRulesSnapshot"]!.ToString()!);
				OnboardingRuleConfiguration.Boolean(rules.RootElement, "sendGuardianCredentialEmail").ShouldBeFalse();
			});
			var settings = (await service.GetSettingsAsync(cycle.AcademicYearId)).Single(s => s.AcademicYearId == cycle.AcademicYearId);
			using var saved = JsonDocument.Parse(settings.ConfigurationJson);
			OnboardingRuleConfiguration.Boolean(saved.RootElement.GetProperty("readinessAccess"), "sendGuardianCredentialEmail").ShouldBeTrue();
		});

	[Theory]
	[InlineData("activated")]
	[InlineData("completed")]
	[InlineData("cancelled")]
	public Task Automatic_onboarding_preserves_activated_completed_and_cancelled_snapshots(string state) =>
		RunCertificationScenarioAsync(async () =>
		{
			var fixture = await CreateOnboardingFlowFixtureAsync("AutomaticHistory", null);
			var cycle = await WithUnitOfWorkAsync(() => _cycleRepository.GetAsync(fixture.Offering.AdmissionCycleId));
			var finalized = await FinalizeAsync(fixture.Application, true, "automatic-history");
			var repo = GetRequiredService<IRepository<StudentOnboardingCase, Guid>>();
			var before = await WithUnitOfWorkAsync(async () =>
			{
				var item = await repo.GetAsync(finalized.OnboardingCaseId);
				if (state == "cancelled") item.Cancel("Historical withdrawal", DateTime.UtcNow);
				else if (state == "completed") item.CompleteHistoricalImport(Guid.NewGuid(), null, null, DateTime.UtcNow);
				else
				{
					item.RecordPlacement(Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow);
					item.RecordIdentifier(null, 1, DateTime.UtcNow);
					item.MarkReady(DateTime.UtcNow);
					item.BeginProvisioning(Guid.NewGuid());
				}
				await repo.UpdateAsync(item, true);
				return JsonSerializer.Serialize(item.ExtraProperties);
			});
			await SaveAutomaticAccessAsync(cycle.AcademicYearId, student: true, guardian: true);
			await WithUnitOfWorkAsync(async () =>
			{
				var item = await repo.GetAsync(finalized.OnboardingCaseId);
				(await GetRequiredService<OnboardingRequirementSnapshotService>().CaptureAsync(item)).ShouldBeFalse();
				JsonSerializer.Serialize(item.ExtraProperties).ShouldBe(before);
			});
		});

	private async Task SaveAutomaticAccessAsync(Guid yearId, bool student, bool guardian, bool sendGuardian = false)
	{
		var service = GetRequiredService<IStudentOnboardingAppService>();
		var settings = (await service.GetSettingsAsync(yearId)).Single(s => s.AcademicYearId == yearId);
		var config = JsonNode.Parse(settings.ConfigurationJson)!.AsObject();
		config["onboardingProfiles"] = new JsonArray();
		config["readinessAccess"]!["studentAccount"] = student;
		config["readinessAccess"]!["guardianAccount"] = guardian;
		config["readinessAccess"]!["sendGuardianCredentialEmail"] = sendGuardian;
		await service.SaveSettingsAsync(new() { AcademicYearId = yearId, ConfigurationJson = config.ToJsonString(), ConcurrencyStamp = settings.ConcurrencyStamp });
	}
}
