using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Theory]
	[InlineData(AdmissionFormFieldType.Number, "\" \"", "NotEquals", "0", "needsInformation")]
	[InlineData(AdmissionFormFieldType.Checkbox, "\"\"", "NotEquals", "true", "needsInformation")]
	[InlineData(AdmissionFormFieldType.Select, "\"\"", "NotEquals", "\"BC\"", "needsInformation")]
	[InlineData(AdmissionFormFieldType.Text, "\" \"", "NotContains", "\"restricted\"", "needsInformation")]
	[InlineData(AdmissionFormFieldType.Number, "80.0", "Equals", "80", "eligible")]
	[InlineData(AdmissionFormFieldType.MultiSelect, "[\"OBC\"]", "Contains", "\"BC\"", "ineligible")]
	[InlineData(AdmissionFormFieldType.MultiSelect, "[\"OBC\"]", "NotContains", "\"BC\"", "eligible")]
	public Task Eligibility_typed_answers_use_numeric_equality_and_exact_choices_while_blanks_remain_unknown(
		AdmissionFormFieldType type, string rawAnswer, string operation, string expectedJson, string expectedOutcome) =>
		RunCertificationScenarioAsync(async () =>
		{
			var formId = await CreateEligibilityAnswerFormAsync(type);
			var criterion = new
			{
				code = "ANSWER", name = "Typed admission answer", kind = "application", formDefinitionId = formId,
				conditionJson = JsonSerializer.Serialize(new { field = "application.ANSWER", @operator = operation, value = JsonSerializer.Deserialize<JsonElement>(expectedJson) })
			};
			var fixture = await CreateEligibilityFixtureAsync(EligibilityPolicyJson([criterion], true), new DateTime(2021, 4, 1), applicationFormDefinitionId: formId);
			await WithUnitOfWorkAsync(async () =>
			{
				var instance = await GetRequiredService<IRepository<AdmissionFormInstance, Guid>>().GetAsync(item => item.ApplicationId == fixture.ApplicationId);
				var values = GetRequiredService<IRepository<AdmissionFormFieldValue, Guid>>();
				var stored = await values.FindAsync(item => item.FormInstanceId == instance.Id && item.FieldCode == "ANSWER");
				if (stored == null) await values.InsertAsync(new(Guid.NewGuid(), TestTenantId, instance.Id, "ANSWER", rawAnswer), autoSave: true);
				else { stored.SetValue(rawAnswer); await values.UpdateAsync(stored, autoSave: true); }
			});
			var state = await EvaluateEligibilityAsync(fixture);
			state.Status.ShouldBe(expectedOutcome);
			state.CanComplete.ShouldBe(expectedOutcome == "eligible");
			if (expectedOutcome == "needsInformation") state.CanRequestException.ShouldBeFalse();
		});

	[Theory]
	[InlineData(2021, 4, 1, "eligible")]
	[InlineData(2021, 4, 2, "ineligible")]
	[InlineData(2020, 4, 1, "eligible")]
	[InlineData(2020, 3, 1, "ineligible")]
	public Task Eligibility_age_uses_the_configured_cutoff_and_inclusive_completed_month_boundaries(int year, int month, int day, string expected) =>
		RunCertificationScenarioAsync(async () =>
		{
			var fixture = await CreateEligibilityFixtureAsync(EligibilityPolicyJson([EligibilityAgeCriterion()]), new DateTime(year, month, day));
			var state = await EvaluateEligibilityAsync(fixture);
			state.Status.ShouldBe(expected);
			state.LatestEvaluation!.Outcome.ShouldBe(expected);
			state.LatestEvaluation.Criteria.Single().Outcome.ShouldBe(expected == "eligible" ? "passed" : "failed");
			state.CanComplete.ShouldBe(expected == "eligible");
		});

	[Fact]
	public Task Eligibility_application_answer_can_be_corrected_and_resubmitted_before_explicit_recheck_and_advance() =>
		RunCertificationScenarioAsync(async () =>
		{
			var formId = await CreateEligibilityAnswerFormAsync(AdmissionFormFieldType.Number);
			var criterion = new { code = "MARKS", name = "Prior school marks", kind = "application", formDefinitionId = formId,
				conditionJson = "{\"field\":\"application.ANSWER\",\"operator\":\"GreaterThanOrEqual\",\"value\":80}" };
			var fixture = await CreateEligibilityFixtureAsync(EligibilityPolicyJson([criterion]), new DateTime(2021, 4, 1), applicationFormDefinitionId: formId);
			var service = GetRequiredService<IAdmissionEligibilityAppService>();
			(await EvaluateEligibilityAsync(fixture)).Status.ShouldBe("needsInformation");
			foreach (var (answer, expected) in new[] { ("60", "ineligible"), ("90", "eligible") })
			{
				(await _applicationAppService.ReopenFormAsync(fixture.ApplicationId)).Status.ShouldBe(AdmissionFormInstanceStatus.Reopened);
				(await service.GetAsync(fixture.ApplicationId, fixture.StageId)).CanEvaluate.ShouldBeFalse();
				var application = await _applicationAppService.GetAsync(fixture.ApplicationId);
				await _applicationAppService.UpdateFormValuesAsync(fixture.ApplicationId, new()
				{
					ConcurrencyStamp = application.ConcurrencyStamp, Values = [new() { FieldCode = "ANSWER", ValueJson = answer }]
				});
				var submitted = await _applicationAppService.SubmitAsync(fixture.ApplicationId);
				submitted.ValidationErrors.ShouldBeEmpty();
				submitted.Application.Status.ShouldBe(AdmissionApplicationStatus.Submitted);
				(await _applicationAppService.GetWorkflowAsync(fixture.ApplicationId)).CurrentStageInstanceId.ShouldBe(fixture.StageId);
				var stale = await service.GetAsync(fixture.ApplicationId, fixture.StageId);
				stale.IsStale.ShouldBeTrue();
				stale.CanComplete.ShouldBeFalse();
				(await EvaluateEligibilityAsync(fixture)).Status.ShouldBe(expected);
			}
			await AdvanceAssessmentAsync(fixture.ApplicationId, fixture.StageId);
			(await _applicationAppService.GetWorkflowAsync(fixture.ApplicationId)).CurrentStageInstanceId.ShouldNotBe(fixture.StageId);
			(await service.GetHistoryAsync(fixture.ApplicationId, fixture.StageId)).Count.ShouldBe(3);
		});

	[Fact]
	public Task Eligibility_missing_canonical_birth_date_cannot_be_satisfied_by_saved_form_facts() =>
		RunCertificationScenarioAsync(async () =>
		{
			var fixture = await CreateEligibilityFixtureAsync(EligibilityPolicyJson([EligibilityAgeCriterion()], allowExceptions: true), null);
			await SeedEligibilityFormSpoofAsync(fixture.ApplicationId, "dateOfBirth", "2021-04-01");
			await SeedEligibilityFormSpoofAsync(fixture.ApplicationId, "applicant.dateOfBirth", "2021-04-01");
			await SeedEligibilityFormSpoofAsync(fixture.ApplicationId, "eligibility.outcome", "eligible");
			var state = await EvaluateEligibilityAsync(fixture);
			state.Status.ShouldBe("needsInformation");
			state.LatestEvaluation!.Criteria.Single().Outcome.ShouldBe("unknown");
			state.CanComplete.ShouldBeFalse();
			state.CanRequestException.ShouldBeFalse();
		});

	[Fact]
	public Task Eligibility_linked_student_birth_date_and_external_corrections_override_the_applicant_snapshot() =>
		RunCertificationScenarioAsync(async () =>
		{
			var fixture = await CreateEligibilityFixtureAsync(EligibilityPolicyJson([EligibilityAgeCriterion()]), new DateTime(2021, 4, 1), existingStudent: true);
			await UpdateEligibilityStudentBirthDateAsync(fixture.StudentId!.Value, new DateTimeOffset(2022, 4, 1, 0, 0, 0, TimeSpan.Zero));
			var service = GetRequiredService<IAdmissionEligibilityAppService>();
			var failed = await EvaluateEligibilityAsync(fixture);
			failed.Status.ShouldBe("ineligible");
			var applicationStamp = (await _applicationAppService.GetAsync(fixture.ApplicationId)).ConcurrencyStamp;
			await UpdateEligibilityStudentBirthDateAsync(fixture.StudentId.Value, new DateTimeOffset(2021, 4, 1, 0, 0, 0, TimeSpan.Zero));
			(await _applicationAppService.GetAsync(fixture.ApplicationId)).ConcurrencyStamp.ShouldBe(applicationStamp);
			var stale = await service.GetAsync(fixture.ApplicationId, fixture.StageId);
			stale.IsStale.ShouldBeTrue();
			stale.CanComplete.ShouldBeFalse();
			var corrected = await EvaluateEligibilityAsync(fixture);
			corrected.Status.ShouldBe("eligible");
			corrected.LatestEvaluation!.Id.ShouldNotBe(failed.LatestEvaluation!.Id);
			await UpdateEligibilityStudentBirthDateAsync(fixture.StudentId.Value, null);
			(await service.GetAsync(fixture.ApplicationId, fixture.StageId)).IsStale.ShouldBeTrue();
			(await EvaluateEligibilityAsync(fixture)).Status.ShouldBe("needsInformation");
			(await service.GetHistoryAsync(fixture.ApplicationId, fixture.StageId)).Count.ShouldBe(3);
		});

	[Fact]
	public Task Eligibility_document_source_tracks_accepted_evidence_and_invalidates_after_external_revision_and_review() =>
		RunCertificationScenarioAsync(async () =>
		{
			var requirement = new AdmissionDocumentRequirement { Code = "BIRTH", Name = "Birth certificate", CollectionMode = "original" };
			var policy = HandoffPolicy(requirement);
			var criterion = new { code = "PROOF", name = "Birth evidence verified", kind = "documents", sourceStageCode = "DOCUMENTS", requiredOutcome = "verified" };
			var fixture = await CreateEligibilityFixtureAsync(EligibilityPolicyJson([criterion]), new DateTime(2021, 4, 1), priorStages:
			[
				new() { Code = "DOCUMENTS", Name = "Verify birth evidence", StageType = AdmissionWorkflowStageType.DocumentVerification, IsRequired = true,
					ConfigurationJson = JsonSerializer.Serialize(new { documentVerification = policy }) }
			]);
			var documents = GetRequiredService<IAdmissionDocumentVerificationAppService>();
			var documentStageId = fixture.StageIds["DOCUMENTS"];
			var evidence = await documents.GetAsync(fixture.ApplicationId, documentStageId);
			evidence = await documents.RecordOriginalAsync(fixture.ApplicationId, documentStageId, "BIRTH",
				new() { ConcurrencyStamp = evidence.ConcurrencyStamp, Notes = "Inspected the original birth certificate." });
			evidence = await documents.ReviewAsync(evidence.Requirements.Single().Evidence.Single().Id,
				new() { ConcurrencyStamp = evidence.ConcurrencyStamp, Decision = "accepted", Reason = "Original identity evidence verified." });
			await AdvanceAssessmentAsync(fixture.ApplicationId, documentStageId);
			var service = GetRequiredService<IAdmissionEligibilityAppService>();
			var checkedState = await EvaluateEligibilityAsync(fixture);
			checkedState.Status.ShouldBe("eligible");
			var stamp = checkedState.ConcurrencyStamp;
			// An external source correction appends immutable evidence; normal document UI locks completed stages.
			var documentFixture = new DocumentHandoffFixture(await _applicationAppService.GetAsync(fixture.ApplicationId), fixture.Offering, documentStageId, policy, null);
			var replacement = await AddHandoffEvidenceAsync(documentFixture, requirement, version: 2, kind: "original", decision: null);
			(await _applicationAppService.GetAsync(fixture.ApplicationId)).ConcurrencyStamp.ShouldBe(stamp);
			(await service.GetAsync(fixture.ApplicationId, fixture.StageId)).IsStale.ShouldBeTrue();
			(await EvaluateEligibilityAsync(fixture)).Status.ShouldBe("ineligible");
			await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<AdmissionDocumentReview, Guid>>().InsertAsync(
				new(Guid.NewGuid(), TestTenantId, replacement, policy, "accepted", "Corrected original evidence independently verified.", HandoffReviewer, DateTime.UtcNow), autoSave: true));
			var afterReview = await service.GetAsync(fixture.ApplicationId, fixture.StageId);
			afterReview.IsStale.ShouldBeTrue();
			afterReview.CanComplete.ShouldBeFalse();
			(await EvaluateEligibilityAsync(fixture)).Status.ShouldBe("eligible");
			var history = await service.GetHistoryAsync(fixture.ApplicationId, fixture.StageId);
			history.Count.ShouldBe(3);
			history.Single(item => item.Id == checkedState.LatestEvaluation!.Id).Outcome.ShouldBe("eligible");
		});

	[Fact]
	public Task Eligibility_assessment_criterion_reads_its_selected_prior_stage_instead_of_the_latest_assessment() =>
		RunCertificationScenarioAsync(async () =>
		{
			var assessmentJson = JsonSerializer.Serialize(new { assessment = new AdmissionAssessmentPolicy
			{
				Version = 1, Enabled = true, SchedulingEnabled = false, AttendanceRequired = false, ScoringEnabled = true,
				TotalMarks = 100, PassingMarks = 40, ReviewRequired = false, MaxAttempts = 1
			} });
			var criterion = new { code = "ENTRANCE", name = "Entrance result", kind = "assessment", sourceStageCode = "ASSESSMENT_A", minimumPercentage = 80, requiredOutcome = "passed" };
			var fixture = await CreateEligibilityFixtureAsync(EligibilityPolicyJson([criterion]), new DateTime(2021, 4, 1), priorStages:
			[
				new() { Id = Guid.NewGuid(), Code = "ASSESSMENT_A", Name = "Entrance exam", StageType = AdmissionWorkflowStageType.Assessment, IsRequired = true, ConfigurationJson = assessmentJson },
				new() { Id = Guid.NewGuid(), Code = "ASSESSMENT_B", Name = "Later interview", StageType = AdmissionWorkflowStageType.Assessment, IsRequired = true, ConfigurationJson = assessmentJson }
			]);
			await CompleteEligibilitySourceAssessmentAsync(fixture, "ASSESSMENT_A", 60);
			await CompleteEligibilitySourceAssessmentAsync(fixture, "ASSESSMENT_B", 95);
			await SeedEligibilityFormSpoofAsync(fixture.ApplicationId, "assessment.percentage", "99");
			var state = await EvaluateEligibilityAsync(fixture);
			state.Status.ShouldBe("ineligible");
			state.LatestEvaluation!.Criteria.Single().Outcome.ShouldBe("failed");
			state.CanComplete.ShouldBeFalse();
		});
}
