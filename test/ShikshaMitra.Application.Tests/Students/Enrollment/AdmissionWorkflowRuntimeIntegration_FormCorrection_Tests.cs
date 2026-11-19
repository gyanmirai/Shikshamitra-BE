using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using ShikshaMitra.Constants.Permissions;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Security.Claims;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Fact]
	public Task Form_correction_preserves_approval_completed_workflow_and_submitted_evidence() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CreateCertificationFixtureAsync("Correction");
		var before = await _applicationAppService.GetAsync(fixture.Application.Id);
		var workflow = await _applicationAppService.GetWorkflowAsync(before.Id);
		var form = await _applicationAppService.GetFormAsync(before.Id);
		var submissions = await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<AdmissionFormSubmission, Guid>>().GetListAsync(row => row.ApplicationId == before.Id));
		var input = new CorrectAdmissionApplicationFormDto
		{
			ConcurrencyStamp = before.ConcurrencyStamp, Reason = "Correct the spelling", IdempotencyKey = "approved-spelling",
			Values = [new() { FieldCode = "APPLICANT_FIRST_NAME", ValueJson = "\"Corrected\"" }]
		};
		var corrected = await _applicationAppService.CorrectFormAsync(before.Id, input);
		corrected.Values.Single(row => row.FieldCode == "APPLICANT_FIRST_NAME").ValueJson.ShouldBe("\"Corrected\"");
		corrected.Status.ShouldBe(form.Status);
		corrected.FormVersion.ShouldBe(form.FormVersion);
		corrected.SubmittedAt.ShouldBe(form.SubmittedAt);
		var after = await _applicationAppService.GetAsync(before.Id);
		after.Status.ShouldBe(before.Status);
		after.ApplicationNumber.ShouldBe(before.ApplicationNumber);
		after.DecisionBy.ShouldBe(before.DecisionBy);
		after.DecisionAt.ShouldBe(before.DecisionAt);
		after.DecisionReason.ShouldBe(before.DecisionReason);
		JsonSerializer.Serialize(await _applicationAppService.GetWorkflowAsync(before.Id)).ShouldBe(JsonSerializer.Serialize(workflow));
		var retained = await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<AdmissionFormSubmission, Guid>>().GetListAsync(row => row.ApplicationId == before.Id));
		retained.Select(row => (row.Id, row.SnapshotJson, row.AttemptNumber)).ShouldBe(submissions.Select(row => (row.Id, row.SnapshotJson, row.AttemptNumber)));
		var history = await _applicationAppService.GetFormHistoryAsync(before.Id);
		history.Count(row => row.ChangeType == "FormCorrected").ShouldBe(1);
		var audit = history.Single(row => row.ChangeType == "FormCorrected");
		audit.ChangedBy.ShouldBe(TestUserId);
		using (var metadata = JsonDocument.Parse(audit.ChangedFieldsJson!))
		{
			metadata.RootElement.GetProperty("Reason").GetString().ShouldBe("Correct the spelling");
			metadata.RootElement.GetProperty("Fingerprint").GetString()!.Length.ShouldBe(64);
			metadata.RootElement.GetProperty("FieldCodes")[0].GetString().ShouldBe("APPLICANT_FIRST_NAME");
			metadata.RootElement.TryGetProperty("Values", out _).ShouldBeFalse();
		}
		await _applicationAppService.CorrectFormAsync(before.Id, input);
		(await _applicationAppService.GetFormHistoryAsync(before.Id)).Count(row => row.ChangeType == "FormCorrected").ShouldBe(1);
		(await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<AdmissionActivity, Guid>>().CountAsync(row => row.ApplicationId == before.Id && row.ActivityType == "ApplicationFormCorrected"))).ShouldBe(1);
	});

	[Fact]
	public async Task Form_correction_safety_matrix_validates_scope_state_identity_and_finance()
	{
		await Form_correction_keeps_existing_partial_draft_save_behavior();
		await Form_correction_allows_only_open_states_and_preserves_legacy_instance_status();
		await Form_correction_blocks_finalized_links_even_with_approved_status();
		await Form_correction_blocks_ineligible_workflow_without_replaying_it();
		await Form_correction_rejects_stale_stamp_and_changed_key_payload_but_replays_after_finalization();
		await Form_correction_validates_merged_required_answers_and_rejects_unknown_fields_before_any_write();
		await Form_correction_rechecks_exact_permission_tenant_and_scope_even_for_replay();
		await Form_correction_rejects_unchanged_answers_without_advancing_stamp_or_history();
		await Form_correction_respects_pinned_readonly_fields_without_changing_applicant_or_form();
		await Form_correction_legacy_step_and_repeat_visibility_match_saved_answers();
		await Form_correction_preserves_finance_payment_receipt_and_dues_and_keeps_joining_date_locked();
	}

	private Task Form_correction_keeps_existing_partial_draft_save_behavior() => RunCertificationScenarioAsync(async () =>
	{
		var offering = await CreateDecisionWorkspaceOfferingAsync();
		var application = await StartFixtureApplicationAsync((await CreateApplicantAsync("DraftPreserved")).Id, offering.Id);
		var saved = await _applicationAppService.UpdateFormValuesAsync(application.Id, new()
		{
			ConcurrencyStamp = application.ConcurrencyStamp,
			Values = [new() { FieldCode = "APPLICANT_FIRST_NAME", ValueJson = "\"\"" }]
		});
		saved.Status.ShouldBe(AdmissionFormInstanceStatus.Draft);
		(await _applicationAppService.GetAsync(application.Id)).Status.ShouldBe(AdmissionApplicationStatus.Draft);
		(await _applicationAppService.SubmitAsync(application.Id)).FieldErrors.ShouldContainKey("APPLICANT_FIRST_NAME");
		var history = await _applicationAppService.GetFormHistoryAsync(application.Id);
		history.Count(row => row.ChangeType == "DraftSaved").ShouldBe(1);
		history.ShouldNotContain(row => row.ChangeType == "FormCorrected");
	});

	private Task Form_correction_allows_only_open_states_and_preserves_legacy_instance_status() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CreateCertificationFixtureAsync("StateCorrection");
		foreach (var status in Enum.GetValues<AdmissionApplicationStatus>())
		{
			await SetCorrectionApplicationPropertyAsync(fixture.Application.Id, nameof(AdmissionApplication.Status), status);
			var application = await _applicationAppService.GetAsync(fixture.Application.Id);
			var allowed = status is AdmissionApplicationStatus.Submitted or AdmissionApplicationStatus.InReview or AdmissionApplicationStatus.Waitlisted or
				AdmissionApplicationStatus.Approved or AdmissionApplicationStatus.Offered or AdmissionApplicationStatus.OfferAccepted;
			var form = await _applicationAppService.GetFormAsync(application.Id);
			CorrectionCapability(form).ShouldBe(allowed);
			var command = new { application.ConcurrencyStamp, Reason = "State correction", IdempotencyKey = "state-" + status,
				Values = new[] { new { FieldCode = "APPLICANT_FIRST_NAME", ValueJson = JsonSerializer.Serialize("Corrected" + status) } } };
			if (allowed) (await InvokeCorrectionAsync(application.Id, command)).Status.ShouldBe(form.Status);
			else (await Should.ThrowAsync<BusinessException>(() => InvokeCorrectionAsync(application.Id, command))).Data["Reason"].ShouldNotBeNull();
		}
		await SetCorrectionApplicationPropertyAsync(fixture.Application.Id, nameof(AdmissionApplication.Status), AdmissionApplicationStatus.Approved);
		await WithUnitOfWorkAsync(async () =>
		{
			var instances = GetRequiredService<IRepository<AdmissionFormInstance, Guid>>();
			var instance = await instances.GetAsync(row => row.ApplicationId == fixture.Application.Id);
			typeof(AdmissionFormInstance).GetProperty(nameof(AdmissionFormInstance.Status))!.SetValue(instance, AdmissionFormInstanceStatus.Draft);
			await instances.UpdateAsync(instance, true);
		});
		var legacy = await _applicationAppService.GetAsync(fixture.Application.Id);
		(await InvokeCorrectionAsync(legacy.Id, CorrectionCommand(legacy, "legacy-approved"))).Status.ShouldBe(AdmissionFormInstanceStatus.Draft);
	});

	private Task Form_correction_blocks_finalized_links_even_with_approved_status() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CreateCertificationFixtureAsync("LinkedCorrection");
		var finalized = await FinalizeAsync(fixture.Application, true, "link-blocking-fixture");
		foreach (var property in new[] { nameof(AdmissionApplication.StudentId), nameof(AdmissionApplication.OnboardingCaseId), nameof(AdmissionApplication.EnrolledStudentId), nameof(AdmissionApplication.StudentEnrollmentId), nameof(AdmissionApplication.FinalizedAt) })
		{
			await WithUnitOfWorkAsync(async () =>
			{
				var application = await _applicationRepository.GetAsync(fixture.Application.Id);
				foreach (var link in new[] { nameof(AdmissionApplication.StudentId), nameof(AdmissionApplication.OnboardingCaseId), nameof(AdmissionApplication.EnrolledStudentId), nameof(AdmissionApplication.StudentEnrollmentId), nameof(AdmissionApplication.FinalizedAt) })
					typeof(AdmissionApplication).GetProperty(link)!.SetValue(application, null);
				object value = property == nameof(AdmissionApplication.FinalizedAt) ? DateTime.UtcNow : property == nameof(AdmissionApplication.StudentId) ? finalized.StudentId :
					property == nameof(AdmissionApplication.OnboardingCaseId) ? finalized.OnboardingCaseId : Guid.NewGuid();
				typeof(AdmissionApplication).GetProperty(property)!.SetValue(application, value);
				typeof(AdmissionApplication).GetProperty(nameof(AdmissionApplication.Status))!.SetValue(application, AdmissionApplicationStatus.Approved);
				await _applicationRepository.UpdateAsync(application, true);
			});
			var current = await _applicationAppService.GetAsync(fixture.Application.Id);
			CorrectionCapability(await _applicationAppService.GetFormAsync(current.Id)).ShouldBeFalse();
			await Should.ThrowAsync<BusinessException>(() => InvokeCorrectionAsync(current.Id, CorrectionCommand(current, "linked")));
		}
	});

	private Task Form_correction_blocks_ineligible_workflow_without_replaying_it() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CreateCertificationFixtureAsync("WorkflowCorrection");
		foreach (var status in new[] { AdmissionWorkflowInstanceStatus.Cancelled, AdmissionWorkflowInstanceStatus.Failed, AdmissionWorkflowInstanceStatus.NotStarted })
		{
			await WithUnitOfWorkAsync(async () =>
			{
				var repository = GetRequiredService<IRepository<AdmissionApplicationWorkflowInstance, Guid>>();
				var runtime = await repository.GetAsync(row => row.ApplicationId == fixture.Application.Id);
				typeof(AdmissionApplicationWorkflowInstance).GetProperty(nameof(AdmissionApplicationWorkflowInstance.Status))!.SetValue(runtime, status);
				await repository.UpdateAsync(runtime, true);
			});
			var current = await _applicationAppService.GetAsync(fixture.Application.Id);
			CorrectionCapability(await _applicationAppService.GetFormAsync(current.Id)).ShouldBeFalse();
			await Should.ThrowAsync<BusinessException>(() => InvokeCorrectionAsync(current.Id, CorrectionCommand(current, "workflow")));
		}
	});

	private Task Form_correction_rejects_stale_stamp_and_changed_key_payload_but_replays_after_finalization() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CreateCertificationFixtureAsync("RetryCorrection");
		var before = await _applicationAppService.GetAsync(fixture.Application.Id);
		var original = CorrectionCommand(before, "durable-replay");
		await InvokeCorrectionAsync(before.Id, original);
		(await Should.ThrowAsync<BusinessException>(() => InvokeCorrectionAsync(before.Id, CorrectionCommand(before, "stale-key")))).Code.ShouldBe("Students.Admissions.FormCorrection.Conflict");
		var current = await _applicationAppService.GetAsync(before.Id);
		(await Should.ThrowAsync<BusinessException>(() => InvokeCorrectionAsync(before.Id, CorrectionCommand(current, "durable-replay", "Different reason")))).Code.ShouldBe("Students.Admissions.FormCorrection.Conflict");
		await SetCorrectionApplicationPropertyAsync(before.Id, nameof(AdmissionApplication.Status), AdmissionApplicationStatus.AdmissionFinalized);
		await InvokeCorrectionAsync(before.Id, original);
		(await _applicationAppService.GetFormHistoryAsync(before.Id)).Count(row => row.ChangeType == "FormCorrected").ShouldBe(1);
	});

	private Task Form_correction_validates_merged_required_answers_and_rejects_unknown_fields_before_any_write() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CreateCertificationFixtureAsync("ValidatedCorrection");
		var before = await _applicationAppService.GetAsync(fixture.Application.Id);
		var form = await _applicationAppService.GetFormAsync(before.Id);
		var invalid = new { before.ConcurrencyStamp, Reason = "Check required answer", IdempotencyKey = "required", Values = new[]
			{ new { FieldCode = "APPLICANT_FIRST_NAME", ValueJson = "\"\"" } } };
		var error = await Should.ThrowAsync<BusinessException>(() => InvokeCorrectionAsync(before.Id, invalid));
		error.Data["FieldErrors"].ShouldNotBeNull();
		await Should.ThrowAsync<BusinessException>(() => InvokeCorrectionAsync(before.Id, new { before.ConcurrencyStamp, Reason = "Unknown answer", IdempotencyKey = "unknown", Values = new[]
			{ new { FieldCode = "NOT_IN_PINNED_FORM", ValueJson = "\"Value\"" } } }));
		JsonSerializer.Serialize(await _applicationAppService.GetFormAsync(before.Id)).ShouldBe(JsonSerializer.Serialize(form));
		(await _applicationAppService.GetAsync(before.Id)).ConcurrencyStamp.ShouldBe(before.ConcurrencyStamp);
		(await _applicationAppService.GetFormHistoryAsync(before.Id)).ShouldNotContain(row => row.ChangeType == "FormCorrected");
	});

	private Task Form_correction_rechecks_exact_permission_tenant_and_scope_even_for_replay() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CreateCertificationFixtureAsync("SecureCorrection");
		var current = await _applicationAppService.GetAsync(fixture.Application.Id);
		var command = CorrectionCommand(current, "secure-replay");
		await InvokeCorrectionAsync(current.Id, command);
		var provider = GetRequiredService<IServiceProvider>();
		var authorization = Substitute.For<IAbpAuthorizationService>();
		authorization.CurrentPrincipal.Returns(new ClaimsPrincipal(new ClaimsIdentity()));
		authorization.AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<object?>(), Arg.Any<string>()).Returns(call =>
			Task.FromResult(call.ArgAt<string>(2) == ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.Edit ? AuthorizationResult.Failed() : AuthorizationResult.Success()));
		var service = ActivatorUtilities.CreateInstance<AdmissionApplicationFormService>(provider);
		service.LazyServiceProvider = new AbpLazyServiceProvider(new CredentialsAuthorizationProvider(provider, authorization));
		CorrectionCapability(await WithUnitOfWorkAsync(() => service.GetAsync(current.Id))).ShouldBeFalse();
		await Should.ThrowAsync<AbpAuthorizationException>(() => InvokeCorrectionAsync(current.Id, command, service));
		using (_currentTenant.Change(Guid.NewGuid()))
			await Should.ThrowAsync<EntityNotFoundException>(() => InvokeCorrectionAsync(current.Id, command));
		var actor = Guid.NewGuid();
		await EnsureTestIdentityUserAsync(actor, "correction-no-scope");
		using (_principalAccessor.Change(new ClaimsPrincipal(new ClaimsIdentity([new Claim(AbpClaimTypes.UserId, actor.ToString()), new Claim(AbpClaimTypes.UserName, "correction-no-scope")], "Test"))))
			await Should.ThrowAsync<EntityNotFoundException>(() => InvokeCorrectionAsync(current.Id, command));
	});

	private static bool CorrectionCapability(AdmissionApplicationFormDto form) => form.CanCorrectForm;

	private Task Form_correction_rejects_unchanged_answers_without_advancing_stamp_or_history() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CreateCertificationFixtureAsync("UnchangedCorrection");
		var before = await _applicationAppService.GetAsync(fixture.Application.Id);
		var form = await _applicationAppService.GetFormAsync(before.Id);
		var history = await _applicationAppService.GetFormHistoryAsync(before.Id);
		var error = await Should.ThrowAsync<BusinessException>(() => _applicationAppService.CorrectFormAsync(before.Id, new()
		{
			ConcurrencyStamp = before.ConcurrencyStamp, Reason = "Unchanged review", IdempotencyKey = "unchanged",
			Values = [form.Values.Single(value => value.FieldCode == "APPLICANT_FIRST_NAME")]
		}));
		error.Code.ShouldBe("Students.Admissions.FormCorrection.Invalid");
		error.Data["Reason"].ShouldBe("No application details have changed.");
		(await _applicationAppService.GetAsync(before.Id)).ConcurrencyStamp.ShouldBe(before.ConcurrencyStamp);
		(await _applicationAppService.GetFormHistoryAsync(before.Id)).Select(row => row.Id).ShouldBe(history.Select(row => row.Id));
	});

	private Task Form_correction_respects_pinned_readonly_fields_without_changing_applicant_or_form() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CreateCertificationFixtureAsync("ReadonlyCorrection");
		var before = await _applicationAppService.GetAsync(fixture.Application.Id);
		var form = await _applicationAppService.GetFormAsync(before.Id);
		form.Definition.Sections.SelectMany(section => section.Fields).Single(field => field.Code == "APPLICANT_FIRST_NAME").ReadOnly = true;
		await WithUnitOfWorkAsync(async () =>
		{
			var repository = GetRequiredService<IRepository<AdmissionFormInstance, Guid>>();
			var instance = await repository.GetAsync(row => row.ApplicationId == before.Id);
			instance.ExtraProperties[AdmissionFormInstance.EffectiveSchemaEnabledKey] = true;
			instance.ExtraProperties[AdmissionFormInstance.EffectiveSchemaKey] = JsonSerializer.Serialize(new AdmissionEffectiveFormRuntimeSnapshot { Definition = form.Definition }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
			await repository.UpdateAsync(instance, true);
		});
		await Should.ThrowAsync<BusinessException>(() => InvokeCorrectionAsync(before.Id, CorrectionCommand(before, "readonly")));
		(await _applicationAppService.GetFormAsync(before.Id)).Values.Single(row => row.FieldCode == "APPLICANT_FIRST_NAME").ValueJson.ShouldBe(form.Values.Single(row => row.FieldCode == "APPLICANT_FIRST_NAME").ValueJson);
		(await WithUnitOfWorkAsync(() => _applicantRepository.GetAsync(before.ApplicantId))).FirstName.ShouldBe("ReadonlyCorrection");
	});

	private Task Form_correction_legacy_step_and_repeat_visibility_match_saved_answers() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CreateCertificationFixtureAsync("LegacyRowsCorrection");
		var before = await _applicationAppService.GetAsync(fixture.Application.Id);
		var formDto = await _applicationAppService.GetFormAsync(before.Id);
		await WithUnitOfWorkAsync(async () =>
		{
			var instances = GetRequiredService<IRepository<AdmissionFormInstance, Guid>>();
			AdmissionEffectiveFormStore.Read(await instances.GetAsync(row => row.ApplicationId == before.Id)).ShouldBeNull();
			var form = await _formRepository.GetAsync(formDto.FormDefinitionId);
			var rows = await _formSectionRepository.InsertAsync(new AdmissionFormSectionDefinition(Guid.NewGuid(), form.Id,
				"CORRECTION_ROWS", "Repeatable correction", 90, null), true);
			var hidden = await _formSectionRepository.InsertAsync(new AdmissionFormSectionDefinition(Guid.NewGuid(), form.Id,
				"HIDDEN_CORRECTION", "Hidden step", 91, null, Guid.NewGuid(), "HIDDEN", "Hidden step", 91,
				"{\"field\":\"APPLICANT_FIRST_NAME\",\"operator\":\"Equals\",\"value\":\"NEVER_VISIBLE\"}"), true);
			await _formFieldRepository.InsertAsync(new AdmissionFormFieldDefinition(Guid.NewGuid(), form.Id, rows.Id,
				"ROW_KIND", "Row kind", AdmissionFormFieldType.Text, AdmissionFormBindingType.Custom, null, false, false, 0,
				null, null, null, null, null), true);
			await _formFieldRepository.InsertAsync(new AdmissionFormFieldDefinition(Guid.NewGuid(), form.Id, rows.Id,
				"ROW_DETAIL", "Row detail", AdmissionFormFieldType.Text, AdmissionFormBindingType.Custom, null, false, false, 1,
				null, null, null, null, "{\"field\":\"ROW_KIND\",\"operator\":\"Equals\",\"value\":\"show\"}"), true);
			await _formFieldRepository.InsertAsync(new AdmissionFormFieldDefinition(Guid.NewGuid(), form.Id, hidden.Id,
				"HIDDEN_REQUIRED", "Hidden required answer", AdmissionFormFieldType.Text, AdmissionFormBindingType.Custom, null, true, false, 0,
				null, null, null, null, null), true);
			AdmissionFormMetadataStore.WriteExperience(form, new CreateUpdateAdmissionFormDto
			{
				Sections = [new() { Code = rows.Code, IsRepeatable = true, MinEntries = 0, MaxEntries = 3 },
					new() { Code = hidden.Code, IsRepeatable = true, MinEntries = 1, MaxEntries = 3 }]
			});
			await _formRepository.UpdateAsync(form, true);
		});
		var saved = await _applicationAppService.CorrectFormAsync(before.Id, new()
		{
			ConcurrencyStamp = before.ConcurrencyStamp, Reason = "Correct the visible repeated answer", IdempotencyKey = "legacy-visible-row",
			Values = [new() { FieldCode = "ROW_KIND", ValueJson = "[\"hide\",\"show\"]" },
				new() { FieldCode = "ROW_DETAIL", ValueJson = "[\"\",\"Visible correction\"]" }]
		});
		saved.Values.Single(row => row.FieldCode == "ROW_DETAIL").ValueJson.ShouldBe("[\"\",\"Visible correction\"]");
		var current = await _applicationAppService.GetAsync(before.Id);
		await Should.ThrowAsync<BusinessException>(() => _applicationAppService.CorrectFormAsync(before.Id, new()
		{
			ConcurrencyStamp = current.ConcurrencyStamp, Reason = "Hidden answer must not save", IdempotencyKey = "legacy-hidden-row",
			Values = [new() { FieldCode = "ROW_KIND", ValueJson = "[\"hide\",\"hide\"]" },
				new() { FieldCode = "ROW_DETAIL", ValueJson = "[\"Hidden change\",\"Hidden change\"]" }]
		}));
		(await _applicationAppService.GetAsync(before.Id)).ConcurrencyStamp.ShouldBe(current.ConcurrencyStamp);
		(await _applicationAppService.GetFormAsync(before.Id)).Values.Single(row => row.FieldCode == "ROW_KIND").ValueJson.ShouldBe("[\"hide\",\"show\"]");
	});

	private Task Form_correction_preserves_finance_payment_receipt_and_dues_and_keeps_joining_date_locked() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CreateCollectibleAdmissionAsync("FinanceCorrection");
		var dates = GetRequiredService<IAdmissionPaymentJoiningDateAppService>();
		var initial = await dates.GetAsync(fixture.Application.Id);
		var date = initial.AcademicYearStartDate.AddDays(1);
		await SaveJoiningAnswerAsync(fixture, date.ToString("yyyy-MM-dd"));
		await dates.SaveAsync(fixture.Application.Id, new() { JoiningDate = date.ToString("yyyy-MM-dd"), ConcurrencyStamp = (await dates.GetAsync(fixture.Application.Id)).ConcurrencyStamp });
		var finance = GetRequiredService<ShikshaMitra.Finance.Fees.Collections.IAdmissionFeeCollectionAppService>();
		var prepared = await finance.PrepareAsync(fixture.Application.Id, new());
		var due = (await AccountDuesAsync(prepared.AccountId!.Value)).Single();
		var payment = await finance.CollectAsync(fixture.Application.Id, AdmissionCashPayment(due, 300m, "correction-payment"));
		async Task<string> FinanceEvidenceAsync() => JsonSerializer.Serialize(new
		{
			Account = await ReadAsync<ShikshaMitra.Finance.Fees.FeeCollection.FinanceFeeAccount>(prepared.AccountId.Value),
			Dues = await AccountDuesAsync(prepared.AccountId.Value),
			Payment = await ReadAsync<ShikshaMitra.Finance.Fees.FeeCollection.FeeRecordMetadata>(payment.Id),
			Receipt = await ReadAsync<ShikshaMitra.Finance.Fees.Receipts.FeePaymentReceipt>(payment.FeeReceiptId!.Value)
		});
		var beforeFinance = await FinanceEvidenceAsync();
		var before = await _applicationAppService.GetAsync(fixture.Application.Id);
		await InvokeCorrectionAsync(before.Id, CorrectionCommand(before, "paid-spelling"));
		(await FinanceEvidenceAsync()).ShouldBe(beforeFinance);
		await AssertOneCollectionAsync(payment, prepared.AccountId.Value);
		var current = await _applicationAppService.GetAsync(before.Id);
		var error = await Should.ThrowAsync<BusinessException>(() => InvokeCorrectionAsync(before.Id, new
		{
			current.ConcurrencyStamp, Reason = "Attempt date correction", IdempotencyKey = "locked-date",
			Values = new[] { new { FieldCode = "ACTUAL_JOINING_DATE", ValueJson = JsonSerializer.Serialize(date.AddDays(1).ToString("yyyy-MM-dd")) } }
		}));
		error.Code.ShouldBe(AdmissionFeeSelectionErrorCodes.Locked);
		error.Data["Reason"].ShouldNotBeNull();
		(await FinanceEvidenceAsync()).ShouldBe(beforeFinance);
	});

	private static object CorrectionCommand(AdmissionApplicationDto application, string key, string reason = "Correct the spelling") => new
	{
		application.ConcurrencyStamp, Reason = reason, IdempotencyKey = key,
		Values = new[] { new { FieldCode = "APPLICANT_FIRST_NAME", ValueJson = "\"Corrected\"" } }
	};

	private Task<AdmissionApplicationFormDto> InvokeCorrectionAsync(Guid id, object command, AdmissionApplicationFormService? service = null)
	{
		var input = JsonSerializer.Deserialize<CorrectAdmissionApplicationFormDto>(JsonSerializer.Serialize(command))!;
		return service == null ? _applicationAppService.CorrectFormAsync(id, input) : service.CorrectAsync(id, input);
	}

	private Task SetCorrectionApplicationPropertyAsync(Guid id, string property, object value) => WithUnitOfWorkAsync(async () =>
	{
		var application = await _applicationRepository.GetAsync(id);
		typeof(AdmissionApplication).GetProperty(property)!.SetValue(application, value);
		await _applicationRepository.UpdateAsync(application, true);
	});
}
