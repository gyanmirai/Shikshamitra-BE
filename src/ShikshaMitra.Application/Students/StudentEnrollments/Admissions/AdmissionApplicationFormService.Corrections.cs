using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionApplicationFormService
{
	public async Task<AdmissionApplicationFormDto> CorrectAsync(Guid applicationId, CorrectAdmissionApplicationFormDto input)
	{
		await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.Edit);
		try
		{
			using var work = UnitOfWorkManager.Begin(new AbpUnitOfWorkOptions
			{ IsTransactional = true, IsolationLevel = System.Data.IsolationLevel.Serializable }, requiresNew: true);
			var application = await applicationReadService.GetEntityAsync(applicationId);
			var inputValidation = new CorrectAdmissionApplicationFormDtoValidator().Validate(input);
			if (!inputValidation.IsValid)
				throw InvalidCorrection("Review the correction details before saving.", inputValidation.Errors
					.GroupBy(error => error.PropertyName).ToDictionary(group => group.Key, group => group.First().ErrorMessage));
			var eventId = new Guid(SHA256.HashData(Encoding.UTF8.GetBytes(
				$"admission-form-correction:{application.TenantId:N}:{application.Id:N}:{input.IdempotencyKey}"))[..16]);
			var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(input))));
			var previous = await historyRepository.FindAsync(eventId);
			// Reauthorize every retry, but permit an exact successful replay after later lifecycle changes.
			if (previous != null)
			{
				CorrectionAudit? audit;
				try { audit = JsonSerializer.Deserialize<CorrectionAudit>(previous.ChangedFieldsJson ?? "null"); }
				catch (JsonException) { throw CorrectionConflict(); }
				if (previous.ApplicationId != application.Id || previous.ChangeType != "FormCorrected" || audit?.Fingerprint != fingerprint)
					throw CorrectionConflict();
				var replay = await GetAsync(applicationId);
				await work.CompleteAsync();
				return replay;
			}
			var blockedReason = await GetCorrectionBlockedReasonAsync(application);
			if (blockedReason != null) throw new BusinessException(AdmissionFormCorrectionErrorCodes.Blocked, blockedReason);
			if (application.ConcurrencyStamp != input.ConcurrencyStamp) throw CorrectionConflict();
			var instance = await GetInstanceAsync(applicationId);
			var validation = await ValidateCorrectionAsync(application, instance, input);
			var changed = validation.ChangedFields;
			if (changed.Count == 0) throw InvalidCorrection("No application details have changed.", []);
			// This is the same binding/identity/Finance owner as Draft save, with its existing safeguards.
			await ApplyValuesAsync(application, instance, input, validation.WritableCodes);
			application.RecordFormCorrection(Clock.Now);
			await applicationRepository.UpdateAsync(application, false);
			var metadata = JsonSerializer.Serialize(new CorrectionAudit(1, input.Reason.Trim(),
				changed.Select(field => field.Code).ToArray(), changed.Select(field => field.Label).ToArray(), fingerprint));
			await historyRepository.InsertAsync(new AdmissionFormChangeHistory(eventId, CurrentTenant.Id, instance.Id,
				application.Id, "FormCorrected", metadata, CurrentUser.Id), false);
			await activityRepository.InsertAsync(LazyServiceProvider.LazyGetRequiredService<StudentJourneyAuditWriter>().CreateActivity(
				"ApplicationFormCorrected", "Application form corrected", application.ApplicantId, application.Id, application.InquiryId, metadata), false);
			await work.SaveChangesAsync();
			var result = await GetAsync(applicationId);
			await work.CompleteAsync();
			return result;
		}
		catch (AbpDbConcurrencyException) { throw CorrectionConflict(); }
		catch (Exception error) when (IsCorrectionDatabaseConflict(error)) { throw CorrectionConflict(); }
		catch (BusinessException error) { error.WithData("Reason", error.Message); throw; }
	}

	private async Task<string?> GetCorrectionBlockedReasonAsync(AdmissionApplication application)
	{
		if (!await AuthorizationService.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.Edit))
			return "You do not have permission to correct this application form.";
		if (!application.CanCorrectForm)
			return application.Status == AdmissionApplicationStatus.Draft ? "Save this application through its draft form." :
				"Only an open application before admission finalization can be corrected.";
		var runtime = await LazyServiceProvider.LazyGetRequiredService<IRepository<AdmissionApplicationWorkflowInstance, Guid>>()
			.FindAsync(row => row.ApplicationId == application.Id);
		if (runtime == null || runtime.Status is not (AdmissionWorkflowInstanceStatus.Active or AdmissionWorkflowInstanceStatus.Completed))
			return "The admission workflow must be active or completed before correcting the form.";
		if (application.CurrentStageInstanceId.HasValue)
		{
			var stage = await LazyServiceProvider.LazyGetRequiredService<IRepository<AdmissionApplicationStageInstance, Guid>>().FindAsync(application.CurrentStageInstanceId.Value);
			if (stage == null || stage.WorkflowInstanceId != runtime.Id || stage.Status is AdmissionRuntimeStageStatus.Cancelled or AdmissionRuntimeStageStatus.Failed)
				return "The current admission stage is unavailable for form corrections.";
		}
		return null;
	}

	private async Task<(List<AdmissionFormFieldDefinition> ChangedFields, IReadOnlySet<string> WritableCodes)> ValidateCorrectionAsync(AdmissionApplication application,
		AdmissionFormInstance instance, CorrectAdmissionApplicationFormDto input)
	{
		var frozen = AdmissionEffectiveFormStore.Read(instance);
		if (frozen == null) AdmissionEffectiveFormStore.EnsureLegacyAllowed(await formRepository.GetAsync(instance.FormDefinitionId));
		var fields = frozen == null ? await fieldRepository.GetListAsync(field => field.FormDefinitionId == instance.FormDefinitionId) :
			AdmissionEffectiveFormResolver.MaterializeFields(frozen.Definition);
		var sections = frozen == null ? await sectionRepository.GetListAsync(section => section.FormDefinitionId == instance.FormDefinitionId) :
			AdmissionEffectiveFormResolver.MaterializeSections(frozen.Definition);
		var experience = frozen == null ? await GetLegacyExperienceAsync(instance) : AdmissionEffectiveFormResolver.Experience(frozen.Definition);
		var definition = frozen?.Definition ?? MapDefinition(await formRepository.GetAsync(instance.FormDefinitionId), sections, fields);
		// Legacy reads expose the same step/repeat metadata; correction validation must honor it too.
		if (frozen == null) sections = AdmissionEffectiveFormResolver.MaterializeSections(definition);
		var existing = await valueRepository.GetListAsync(value => value.FormInstanceId == instance.Id);
		var merged = existing.ToDictionary(value => value.FieldCode, value => AdmissionApplicationValueConverter.Unwrap(value.ValueJson), StringComparer.OrdinalIgnoreCase);
		var changed = new List<AdmissionFormFieldDefinition>();
		foreach (var value in input.Values)
		{
			var field = fields.FirstOrDefault(field => string.Equals(field.Code, value.FieldCode, StringComparison.OrdinalIgnoreCase));
			if (field == null) throw InvalidCorrection("This answer is not part of the application's saved form.", new() { [value.FieldCode] = "Unknown form field." });
			if (field.ReadOnly)
			{
				if (merged.GetValueOrDefault(field.Code, string.Empty) != AdmissionApplicationValueConverter.Unwrap(value.ValueJson))
					throw InvalidCorrection("An inherited or read-only answer cannot be changed.", new() { [field.Code] = "This answer is read-only." });
				continue;
			}
			var normalized = AdmissionApplicationValueConverter.Unwrap(AdmissionFormEmailValueNormalizer.Normalize(field, value.ValueJson));
			if (merged.GetValueOrDefault(field.Code, string.Empty) == normalized) continue;
			merged[field.Code] = normalized;
			changed.Add(field);
		}
		var visible = AdmissionEffectiveFormResolver.VisibleFieldCodes(definition, merged, formValidator);
		var writable = fields.Where(field => !field.ReadOnly && visible.Contains(field.Code)).Select(field => field.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
		var hidden = changed.FirstOrDefault(field => !writable.Contains(field.Code));
		if (hidden != null) throw InvalidCorrection("A hidden answer cannot be changed.", new() { [hidden.Code] = "This answer is not currently editable." });
		// The shared validator checks repeat cardinality before field visibility. Only sections with
		// a visible field in at least one row participate in this correction's full validation.
		var visibleSections = sections.Where(section => fields.Any(field => field.SectionId == section.Id && visible.Contains(field.Code))).ToList();
		var validation = formValidator.Validate(fields, visibleSections, merged, experience);
		var cycle = await cycleRepository.GetAsync(application.AdmissionCycleId);
		await masterDataValidator.ValidateAsync(fields, visibleSections, merged, cycle.AcademicYearId, validation, experience);
		if (validation.Errors.Count > 0) throw InvalidCorrection("Review the highlighted form answers before saving.", validation.FieldErrors);
		return (changed, writable);
	}

	private static string[] ReadAuthoredFieldCodes(AdmissionFormChangeHistory history)
	{
		try
		{
			return history.ChangeType == "FormCorrected" ? JsonSerializer.Deserialize<CorrectionAudit>(history.ChangedFieldsJson ?? "null")?.FieldCodes ?? [] :
				JsonSerializer.Deserialize<string[]>(history.ChangedFieldsJson ?? "[]") ?? [];
		}
		catch (JsonException) { return []; }
	}

	private static BusinessException InvalidCorrection(string reason, Dictionary<string, string> errors) =>
		new BusinessException(AdmissionFormCorrectionErrorCodes.Invalid, reason).WithData("Reason", reason).WithData("FieldErrors", errors);

	private static BusinessException CorrectionConflict() => new BusinessException(AdmissionFormCorrectionErrorCodes.Conflict,
		"This application changed or the retry key belongs to different correction details. Refresh and review the latest form.")
		.WithData("Reason", "This application changed or the retry key belongs to different correction details. Refresh and review the latest form.");

	private static bool IsCorrectionDatabaseConflict(Exception error)
	{
		for (Exception? current = error; current != null; current = current.InnerException)
			if (current is DbException database && database.SqlState is "40001" or "40P01" or "23505") return true;
		return false;
	}

	private sealed record CorrectionAudit(int Version, string Reason, string[] FieldCodes, string[] ChangedFields, string Fingerprint);
}
