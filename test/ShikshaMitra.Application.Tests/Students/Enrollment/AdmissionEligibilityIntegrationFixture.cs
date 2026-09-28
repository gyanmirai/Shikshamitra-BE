using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Security.UserScope;
using Shouldly;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	private async Task<EligibilityFixture> CreateEligibilityFixtureAsync(string configurationJson,
		DateTime? applicantDateOfBirth, bool existingStudent = false,
		IReadOnlyList<CreateUpdateAdmissionWorkflowStageDto>? priorStages = null, Guid? applicationFormDefinitionId = null,
		bool routeIneligibleToReview = false, Guid? inquiryFormDefinitionId = null)
	{
		if (!await WithUnitOfWorkAsync(() => _scopeRepository.AnyAsync(item => item.UserId == TestUserId)))
			await _scopeRepository.InsertAsync(new UserScopeAssignment(Guid.NewGuid(), TestTenantId,
				userId: TestUserId, description: "Eligibility integration scope"), autoSave: true);
		var legacy = await CreateAndPublishWorkflowAsync();
		var draft = await _workflowAppService.CreateNewVersionAsync(legacy.Id);
		var input = ToUpdateInput(draft);
		var entry = input.Stages.Single(item => item.IsEntryStage);
		var terminal = input.Stages.Single(item => item.IsTerminalStage);
		var previousStageId = entry.Id!.Value;
		input.Transitions = [];
		var order = 1;
		foreach (var stage in priorStages ?? [])
		{
			stage.Id ??= Guid.NewGuid();
			stage.DisplayOrder = order++;
			input.Stages.Add(stage);
			input.Transitions.Add(new() { FromStageId = previousStageId, ToStageId = stage.Id.Value, IsDefault = true });
			previousStageId = stage.Id.Value;
		}
		var eligibilityDefinitionId = Guid.NewGuid();
		input.Stages.Add(new()
		{
			Id = eligibilityDefinitionId, Code = "ELIGIBILITY", Name = "Check applicant eligibility",
			StageType = AdmissionWorkflowStageType.Eligibility, DisplayOrder = order++, IsRequired = true,
			ConfigurationJson = configurationJson
		});
		terminal.DisplayOrder = order;
		input.Transitions.Add(new() { FromStageId = previousStageId, ToStageId = eligibilityDefinitionId, IsDefault = true });
		input.Transitions.Add(new() { FromStageId = eligibilityDefinitionId, ToStageId = terminal.Id!.Value, IsDefault = true });
		if (routeIneligibleToReview)
		{
			var reviewId = Guid.NewGuid();
			input.Stages.Add(new() { Id = reviewId, Code = "PLACEMENT_REVIEW", Name = "Review unmet eligibility", StageType = AdmissionWorkflowStageType.Custom, DisplayOrder = order++, IsRequired = true });
			terminal.DisplayOrder = order;
			input.Transitions.Add(new() { FromStageId = eligibilityDefinitionId, ToStageId = reviewId, IsDefault = false, Priority = 1,
				ConditionRuleJson = "{\"field\":\"eligibility.status\",\"operator\":\"Equals\",\"value\":\"ineligible\"}" });
			input.Transitions.Add(new() { FromStageId = reviewId, ToStageId = terminal.Id.Value, IsDefault = true });
		}
		draft = await _workflowAppService.UpdateAsync(draft.Id, input);
		var published = await _workflowAppService.PublishAsync(draft.Id, draft.ConcurrencyStamp);
		var offering = await CreateAvailableOfferingAsync(legacy.Id);
		offering = await WithUnitOfWorkAsync(async () =>
		{
			var storedOffering = await _offeringRepository.GetAsync(offering.Id);
			storedOffering.ConfigureReferences(published.Id, inquiryFormDefinitionId ?? storedOffering.InquiryFormDefinitionId,
				applicationFormDefinitionId ?? storedOffering.ApplicationFormDefinitionId);
			await _offeringRepository.UpdateAsync(storedOffering, autoSave: true);
			var validation = await _offeringValidator.ValidateStoredAsync(storedOffering);
			validation.Errors.ShouldBeEmpty(string.Join(" | ", validation.Errors.Select(error => error.Message)));
			return storedOffering;
		});
		Guid? studentId = existingStudent ? (await CreateExistingStudentAsync(offering, "Eligibility", "Patel")).Id : null;
		var applicant = await CreateCertificationApplicantAsync("Eligibility", true, true, studentId);
		if (applicantDateOfBirth.HasValue) await UpdateEligibilityApplicantBirthDateAsync(applicant.Id, applicantDateOfBirth);
		var application = await StartFixtureApplicationAsync(applicant.Id, offering.Id);
		await _applicationAppService.SubmitAsync(application.Id);
		// A profile correction can remove DOB after the required application form was submitted.
		if (!applicantDateOfBirth.HasValue) await UpdateEligibilityApplicantBirthDateAsync(applicant.Id, null);
		var workflow = await _applicationAppService.GetWorkflowAsync(application.Id);
		return new(applicant.Id, application.Id, offering,
			workflow.Stages.Single(item => item.Code == "ELIGIBILITY").Id,
			eligibilityDefinitionId, studentId, workflow.Stages.ToDictionary(item => item.Code, item => item.Id));
	}

	private async Task UpdateEligibilityApplicantBirthDateAsync(Guid applicantId, DateTime? dateOfBirth) =>
		await WithUnitOfWorkAsync(async () =>
		{
			var applicant = await _applicantRepository.GetAsync(applicantId);
			applicant.UpdateProfile(applicant.FirstName, applicant.MiddleName, applicant.LastName, dateOfBirth,
				applicant.Gender, applicant.AddressLine1, applicant.AddressLine2, applicant.City, applicant.State,
				applicant.Country, applicant.PostalCode);
			await _applicantRepository.UpdateAsync(applicant, autoSave: true);
		});

	private async Task UpdateEligibilityStudentBirthDateAsync(Guid studentId, DateTimeOffset? dateOfBirth) =>
		await WithUnitOfWorkAsync(async () =>
		{
			var student = await _studentRepository.GetAsync(studentId);
			var details = student.AdditionalDetail ?? throw new InvalidOperationException("Student details are required for this fixture.");
			details.UpdateDetails(dateOfBirth, details.Gender, details.DateOfInquiry, details.InquiryPreferenceId, details.PreviousSchool);
			await _studentRepository.UpdateAsync(student, autoSave: true);
		});

	private sealed record EligibilityFixture(Guid ApplicantId, Guid ApplicationId, AdmissionOffering Offering,
		Guid StageId, Guid DefinitionStageId, Guid? StudentId, Dictionary<string, Guid> StageIds);

	private static string EligibilityPolicyJson(object[] criteria, bool allowExceptions = false, string? approvalRole = null) =>
		JsonSerializer.Serialize(new { eligibility = new { version = 1, enabled = true, allowExceptions,
			exceptionApprovalRole = approvalRole, requireIndependentReviewer = true, criteria } });

	private static object EligibilityAgeCriterion() => new
	{
		code = "AGE", name = "Age on school cutoff", kind = "age", minAgeMonths = 60,
		maxAgeMonths = 72, cutoffDate = "2026-04-01", failureMessage = "Age does not meet this grade's cutoff."
	};

	private async Task<AdmissionEligibilityDto> EvaluateEligibilityAsync(EligibilityFixture fixture) =>
		await GetRequiredService<IAdmissionEligibilityAppService>().EvaluateAsync(fixture.ApplicationId, fixture.StageId,
			new() { ConcurrencyStamp = (await _applicationAppService.GetAsync(fixture.ApplicationId)).ConcurrencyStamp
				?? throw new InvalidOperationException("The persisted application must have a concurrency stamp.") });

	private async Task SeedEligibilityFormSpoofAsync(Guid applicationId, string fieldCode, string value) =>
		await WithUnitOfWorkAsync(async () =>
		{
			var form = await GetRequiredService<IRepository<AdmissionFormInstance, Guid>>().GetAsync(item => item.ApplicationId == applicationId);
			await GetRequiredService<IRepository<AdmissionFormFieldValue, Guid>>().InsertAsync(
				new(Guid.NewGuid(), TestTenantId, form.Id, fieldCode, JsonSerializer.Serialize(value)), autoSave: true);
		});

	private async Task CompleteEligibilitySourceAssessmentAsync(EligibilityFixture fixture, string code, decimal marks)
	{
		var service = GetRequiredService<IAdmissionAssessmentAppService>();
		var stageId = fixture.StageIds[code];
		var state = await service.GetAsync(fixture.ApplicationId, stageId);
		state = await service.BookAsync(fixture.ApplicationId, stageId, new() { ConcurrencyStamp = state.ConcurrencyStamp });
		var attempt = state.Attempts.Single();
		await service.RecordResultAsync(attempt.Id, new() { ConcurrencyStamp = attempt.ConcurrencyStamp, Marks = marks });
		await AdvanceAssessmentAsync(fixture.ApplicationId, stageId);
	}

	private async Task<Guid> CreateEligibilityAnswerFormAsync(AdmissionFormFieldType answerType,
		AdmissionFormPurpose purpose = AdmissionFormPurpose.Registration) => await WithUnitOfWorkAsync(async () =>
	{
		var form = new AdmissionFormDefinition(Guid.NewGuid(), TestTenantId, "Eligibility source form",
			$"ELIG_{Guid.NewGuid():N}"[..24], AdmissionFormLayoutType.SinglePage, purpose: purpose);
		form.Publish(TestUserId);
		await _formRepository.InsertAsync(form, autoSave: true);
		var section = new AdmissionFormSectionDefinition(Guid.NewGuid(), form.Id, "ANSWERS", "Eligibility answers", 0, null);
		await _formSectionRepository.InsertAsync(section, autoSave: true);
		await _formFieldRepository.InsertAsync(new AdmissionFormFieldDefinition(Guid.NewGuid(), form.Id, section.Id, "APPLICANT_FIRST_NAME", "Applicant first name", AdmissionFormFieldType.Text,
			AdmissionFormBindingType.System, "Applicant.FirstName", true, false, 1, null, null, null, null, null), autoSave: true);
		await _formFieldRepository.InsertAsync(new AdmissionFormFieldDefinition(Guid.NewGuid(), form.Id, section.Id, "APPLICANT_DOB", "Applicant date of birth", AdmissionFormFieldType.Date,
			AdmissionFormBindingType.System, "Applicant.DateOfBirth", true, false, 2, null, null, null, null, null), autoSave: true);
		await _formFieldRepository.InsertAsync(new AdmissionFormFieldDefinition(Guid.NewGuid(), form.Id, section.Id, "APPLICANT_GENDER", "Applicant gender", AdmissionFormFieldType.Select,
			AdmissionFormBindingType.System, "Applicant.Gender", true, false, 3, null, null, null, null, null,
			"[{\"value\":\"Male\",\"label\":\"Male\"},{\"value\":\"Female\",\"label\":\"Female\"}]"), autoSave: true);
		await _formFieldRepository.InsertAsync(new AdmissionFormFieldDefinition(Guid.NewGuid(), form.Id, section.Id, "ANSWER", "Criterion answer", answerType,
			AdmissionFormBindingType.Custom, null, false, false, 0, null, null, null, null, null,
			answerType is AdmissionFormFieldType.MultiSelect or AdmissionFormFieldType.Select ? "[{\"value\":\"OBC\",\"label\":\"OBC\"},{\"value\":\"BC\",\"label\":\"BC\"}]" : null), autoSave: true);
		return form.Id;
	});
}
