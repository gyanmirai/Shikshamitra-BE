using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

[Authorize]
public class AdmissionApplicationAppService(
	IRepository<AdmissionApplication, Guid> applicationRepository,
	IRepository<AdmissionApplicant, Guid> applicantRepository,
	IRepository<AdmissionFamily, Guid> familyRepository,
	IRepository<AdmissionGuardian, Guid> guardianRepository,
	IRepository<AdmissionOffering, Guid> offeringRepository,
	AdmissionOfferingRuntimeService offeringRuntimeService,
	IRepository<AdmissionWorkflowDefinition, Guid> workflowRepository,
	IRepository<AdmissionWorkflowStageDefinition, Guid> stageRepository,
	IRepository<AdmissionFormDefinition, Guid> formRepository,
	IRepository<AdmissionFormSectionDefinition, Guid> formSectionRepository,
	IRepository<AdmissionFormFieldDefinition, Guid> formFieldRepository,
	IRepository<AdmissionFormFieldDefinition, Guid> fieldRepository,
	IRepository<AdmissionApplicationWorkflowInstance, Guid> workflowInstanceRepository,
	IRepository<AdmissionApplicationStageInstance, Guid> stageInstanceRepository,
	IRepository<AdmissionFormInstance, Guid> formInstanceRepository,
		IRepository<AdmissionFormFieldValue, Guid> valueRepository,
		IRepository<AdmissionActivity, Guid> activityRepository,
		IRepository<AdmissionInquiry, Guid> inquiryRepository,
		IRepository<AdmissionWorkflowTransitionDefinition, Guid> transitionRepository) : ApplicationService, IAdmissionApplicationAppService
{
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.View)]
	public async Task<PagedResultDto<AdmissionApplicationDto>> GetListAsync(AdmissionApplicationListInput input)
	{
		var query = await applicationRepository.GetQueryableAsync();
		var applicantIds = string.IsNullOrWhiteSpace(input.Search) ? null : await AsyncExecuter.ToListAsync((await applicantRepository.GetQueryableAsync()).Where(x => (x.FirstName + " " + (x.LastName ?? string.Empty)).Contains(input.Search!)).Select(x => x.Id));
		var stageIdsByName = string.IsNullOrWhiteSpace(input.CurrentStageSearch) ? null : await AsyncExecuter.ToListAsync((await stageInstanceRepository.GetQueryableAsync()).Where(x => x.Name.Contains(input.CurrentStageSearch!)).Select(x => x.Id));
		query = query.Where(x => (!input.Status.HasValue || x.Status == input.Status) && (!input.AdmissionCycleId.HasValue || x.AdmissionCycleId == input.AdmissionCycleId) && (!input.AdmissionOfferingId.HasValue || x.AdmissionOfferingId == input.AdmissionOfferingId) && (!input.CurrentStageInstanceId.HasValue || x.CurrentStageInstanceId == input.CurrentStageInstanceId) &&
			(string.IsNullOrWhiteSpace(input.CurrentStageSearch) || (x.CurrentStageInstanceId.HasValue && stageIdsByName!.Contains(x.CurrentStageInstanceId.Value))) &&
			(string.IsNullOrWhiteSpace(input.Search) || x.ApplicationNumber.Contains(input.Search) || (applicantIds != null && applicantIds.Contains(x.ApplicantId))));
		var count = await AsyncExecuter.CountAsync(query);
		var rows = await AsyncExecuter.ToListAsync(query.OrderByDescending(x => x.CreationTime).Skip(input.SkipCount).Take(input.MaxResultCount));
		var applicants = await applicantRepository.GetListAsync(x => rows.Select(r => r.ApplicantId).Contains(x.Id));
		var offerings = await offeringRepository.GetListAsync(x => rows.Select(r => r.AdmissionOfferingId).Contains(x.Id));
		var currentStages = await stageInstanceRepository.GetListAsync(x => rows.Where(r => r.CurrentStageInstanceId.HasValue).Select(r => r.CurrentStageInstanceId!.Value).Contains(x.Id));
		return new PagedResultDto<AdmissionApplicationDto>(count, rows.Select(x => Map(x, applicants.FirstOrDefault(a => a.Id == x.ApplicantId)?.FirstName + (applicants.FirstOrDefault(a => a.Id == x.ApplicantId)?.LastName == null ? string.Empty : " " + applicants.FirstOrDefault(a => a.Id == x.ApplicantId)!.LastName), offerings.FirstOrDefault(o => o.Id == x.AdmissionOfferingId)?.Code ?? string.Empty, currentStages.FirstOrDefault(s => s.Id == x.CurrentStageInstanceId)?.Name ?? string.Empty)).ToList());
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.View)]
	public async Task<AdmissionApplicationDto> GetAsync(Guid id) { var application = await GetApplicationAsync(id); var applicant = await applicantRepository.FindAsync(application.ApplicantId); return Map(application, applicant == null ? string.Empty : string.Join(" ", new[] { applicant.FirstName, applicant.LastName }.Where(x => !string.IsNullOrWhiteSpace(x)))); }

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.Start)]
	public async Task<AdmissionApplicationDto> StartAsync(Guid applicantId, Guid offeringId, Guid? inquiryId = null)
	{
		var existingQuery = await applicationRepository.GetQueryableAsync();
		var existing = await AsyncExecuter.FirstOrDefaultAsync(existingQuery.Where(x => x.ApplicantId == applicantId && x.AdmissionOfferingId == offeringId && x.Status != AdmissionApplicationStatus.Withdrawn));
		if (existing != null) return Map(existing);

		var applicant = await applicantRepository.FindAsync(applicantId) ?? throw new EntityNotFoundException(typeof(AdmissionApplicant), applicantId);
		var runtime = await offeringRuntimeService.GetAvailableAsync(offeringId);
		var offering = runtime.Offering;
		if (!inquiryId.HasValue)
		{
			var inquiryQuery = await inquiryRepository.GetQueryableAsync();
			var latestInquiry = await AsyncExecuter.FirstOrDefaultAsync(inquiryQuery
				.Where(x => x.ApplicantId == applicantId && x.AdmissionOfferingId == offeringId)
				.OrderByDescending(x => x.CreationTime));
			inquiryId = latestInquiry?.Id;
		}
		if (inquiryId.HasValue)
		{
			var sourceInquiry = await inquiryRepository.FindAsync(inquiryId.Value) ??
				throw new EntityNotFoundException(typeof(AdmissionInquiry), inquiryId.Value);
			if (sourceInquiry.ApplicantId != applicantId || sourceInquiry.AdmissionCycleId != offering.AdmissionCycleId ||
				sourceInquiry.AdmissionOfferingId != offeringId)
			{
				throw new BusinessException(AdmissionApplicationErrorCodes.OfferingNotAvailable);
			}
		}
		var workflow = await workflowRepository.FindAsync(offering.WorkflowDefinitionId.Value);
		var form = await formRepository.FindAsync(offering.ApplicationFormDefinitionId.Value);
		if (workflow == null || workflow.Status != AdmissionWorkflowStatus.Published || form == null || form.Status != AdmissionFormStatus.Published)
			throw new BusinessException(AdmissionApplicationErrorCodes.ConfigurationNotPublished);

		var application = new AdmissionApplication(GuidGenerator.Create(), CurrentTenant.Id, $"APP/{DateTime.UtcNow:yyyy}/{GuidGenerator.Create():N}"[..20], applicantId, inquiryId, offering.AdmissionCycleId, offeringId, workflow.Id, form.Id);
		var workflowInstance = new AdmissionApplicationWorkflowInstance(GuidGenerator.Create(), CurrentTenant.Id, application.Id, workflow.Id);
		var stages = await stageRepository.GetListAsync(x => x.WorkflowDefinitionId == workflow.Id);
		var orderedStages = stages.OrderBy(x => x.DisplayOrder).ToList();
		if (orderedStages.Count == 0) throw new BusinessException(AdmissionApplicationErrorCodes.ConfigurationNotPublished);
		var stageInstances = orderedStages.Select((x, i) => new AdmissionApplicationStageInstance(GuidGenerator.Create(), workflowInstance.Id, x.Id, x.Code, x.Name, x.StageType, x.DisplayOrder, i == 0)).ToList();
		application.SetCurrentStage(stageInstances[0].Id);
		var formInstance = new AdmissionFormInstance(GuidGenerator.Create(), CurrentTenant.Id, application.Id, form.Id, form.Version);
		var family = await familyRepository.FindAsync(applicant.FamilyId);
		var guardian = family?.PrimaryGuardianId.HasValue == true ? await guardianRepository.FindAsync(family.PrimaryGuardianId.Value) : null;
		var fields = await fieldRepository.GetListAsync(x => x.FormDefinitionId == form.Id);
		var values = fields.Where(x => (x.BindingType == AdmissionFormBindingType.System && !string.IsNullOrWhiteSpace(x.BindingKey)) || !string.IsNullOrWhiteSpace(x.DefaultValue))
			.Select(x => new AdmissionFormFieldValue(GuidGenerator.Create(), CurrentTenant.Id, formInstance.Id, x.Code, x.BindingType == AdmissionFormBindingType.System ? JsonSerializer.Serialize(BoundValue(x.BindingKey!, applicant, guardian)) : x.DefaultValue))
			.Where(x => x.ValueJson != "null" && !string.IsNullOrWhiteSpace(x.ValueJson)).ToList();
		await applicationRepository.InsertAsync(application, false); await workflowInstanceRepository.InsertAsync(workflowInstance, false);
		foreach (var stage in stageInstances) await stageInstanceRepository.InsertAsync(stage, false);
		await formInstanceRepository.InsertAsync(formInstance, false); foreach (var value in values) await valueRepository.InsertAsync(value, false);
		if (inquiryId.HasValue)
		{
			var inquiry = await inquiryRepository.FindAsync(inquiryId.Value);
			if (inquiry != null)
			{
				inquiry.SetStatus(AdmissionInquiryStatus.ApplicationStarted);
				await inquiryRepository.UpdateAsync(inquiry, false);
			}
		}
		await activityRepository.InsertAsync(new AdmissionActivity(GuidGenerator.Create(), CurrentTenant.Id, "ApplicationStarted", "Application started", applicantId, application.Id, inquiryId), true);
		return Map(application);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.View)]
	public async Task<AdmissionApplicationWorkflowDto> GetWorkflowAsync(Guid id)
	{
		var application = await GetApplicationAsync(id); var instance = await workflowInstanceRepository.FindAsync(x => x.ApplicationId == application.Id);
		if (instance == null) throw new EntityNotFoundException(typeof(AdmissionApplicationWorkflowInstance), application.Id);
		var stages = await stageInstanceRepository.GetListAsync(x => x.WorkflowInstanceId == instance.Id);
		return new AdmissionApplicationWorkflowDto { WorkflowDefinitionId = application.WorkflowDefinitionId, Stages = stages.OrderBy(x => x.DisplayOrder).Select(x => new AdmissionApplicationStageDto { Id = x.Id, Code = x.Code, Name = x.Name, StageType = x.StageType, DisplayOrder = x.DisplayOrder, Status = x.Status }).ToList() };
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.View)]
	public async Task<AdmissionApplicationFormDto> GetFormAsync(Guid id)
	{
		var application = await GetApplicationAsync(id); var instance = await formInstanceRepository.FindAsync(x => x.ApplicationId == application.Id);
		if (instance == null) throw new EntityNotFoundException(typeof(AdmissionFormInstance), application.Id);
		var values = await valueRepository.GetListAsync(x => x.FormInstanceId == instance.Id);
		var definition = await formRepository.FindAsync(instance.FormDefinitionId) ?? throw new EntityNotFoundException(typeof(AdmissionFormDefinition), instance.FormDefinitionId);
		var sections = await formSectionRepository.GetListAsync(x => x.FormDefinitionId == definition.Id);
		var fields = await formFieldRepository.GetListAsync(x => x.FormDefinitionId == definition.Id);
		var definitionDto = new AdmissionFormDto { Id = definition.Id, Name = definition.Name, Code = definition.Code, Version = definition.Version, Status = definition.Status, LayoutType = definition.LayoutType, ParentVersionId = definition.ParentVersionId, PublishedAt = definition.PublishedAt, Sections = sections.OrderBy(x => x.DisplayOrder).Select(s => new AdmissionFormSectionDto { Id = s.Id, FormDefinitionId = s.FormDefinitionId, Code = s.Code, Name = s.Name, DisplayOrder = s.DisplayOrder, VisibilityRuleJson = s.VisibilityRuleJson, Fields = fields.Where(f => f.SectionId == s.Id).OrderBy(f => f.DisplayOrder).Select(f => new AdmissionFormFieldDto { Id = f.Id, FormDefinitionId = f.FormDefinitionId, SectionId = f.SectionId, Code = f.Code, Label = f.Label, FieldType = f.FieldType, BindingType = f.BindingType, BindingKey = f.BindingKey, Placeholder = f.Placeholder, HelpText = f.HelpText, DefaultValue = f.DefaultValue, Required = f.Required, ReadOnly = f.ReadOnly, DisplayOrder = f.DisplayOrder, ValidationJson = f.ValidationJson, VisibilityRuleJson = f.VisibilityRuleJson }).ToList() }).ToList() };
		return new AdmissionApplicationFormDto { FormDefinitionId = instance.FormDefinitionId, FormVersion = instance.FormVersion, Definition = definitionDto, Values = values.OrderBy(x => x.FieldCode).Select(x => new AdmissionFormValueDto { FieldCode = x.FieldCode, ValueJson = x.ValueJson }).ToList() };
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.Edit)]
	public async Task<AdmissionApplicationFormDto> UpdateFormValuesAsync(Guid id, UpdateAdmissionApplicationFormValuesDto input)
	{
		var application = await GetApplicationAsync(id); application.SaveDraft();
		if (!string.IsNullOrWhiteSpace(input.ConcurrencyStamp) && !string.Equals(application.ConcurrencyStamp, input.ConcurrencyStamp, StringComparison.Ordinal)) throw new BusinessException(AdmissionApplicationErrorCodes.ConcurrencyConflict);
		var instance = await formInstanceRepository.FindAsync(x => x.ApplicationId == id) ?? throw new EntityNotFoundException(typeof(AdmissionFormInstance), id);
		var fields = await valueRepository.GetListAsync(x => x.FormInstanceId == instance.Id);
		var definitions = await fieldRepository.GetListAsync(x => x.FormDefinitionId == instance.FormDefinitionId);
		foreach (var item in input.Values.DistinctBy(x => x.FieldCode.ToUpperInvariant()).Select(x => new { Input = x, Definition = definitions.FirstOrDefault(d => d.Code == x.FieldCode.ToUpperInvariant()) }).Where(x => x.Definition != null && !x.Definition.ReadOnly))
		{
			var existing = fields.FirstOrDefault(x => x.FieldCode == item.Input.FieldCode.ToUpperInvariant());
			if (existing == null) await valueRepository.InsertAsync(new AdmissionFormFieldValue(GuidGenerator.Create(), CurrentTenant.Id, instance.Id, item.Input.FieldCode, item.Input.ValueJson), false); else { existing.SetValue(item.Input.ValueJson); await valueRepository.UpdateAsync(existing, false); }
		}
		await applicationRepository.UpdateAsync(application, false); await activityRepository.InsertAsync(new AdmissionActivity(GuidGenerator.Create(), CurrentTenant.Id, "ApplicationSaved", "Application draft saved", application.ApplicantId, application.Id), true); return await GetFormAsync(id);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.Submit)]
	public async Task<AdmissionApplicationSubmitResultDto> SubmitAsync(Guid id)
	{
		var application = await GetApplicationAsync(id);
		if (application.Status == AdmissionApplicationStatus.Submitted) return new AdmissionApplicationSubmitResultDto { Application = Map(application) };
		application.SaveDraft();
		var instance = await formInstanceRepository.FindAsync(x => x.ApplicationId == id) ?? throw new EntityNotFoundException(typeof(AdmissionFormInstance), id);
		var fields = await fieldRepository.GetListAsync(x => x.FormDefinitionId == instance.FormDefinitionId);
		var sections = await formSectionRepository.GetListAsync(x => x.FormDefinitionId == instance.FormDefinitionId);
		var sectionVisibility = sections.ToDictionary(x => x.Id, x => x.VisibilityRuleJson);
		var values = await valueRepository.GetListAsync(x => x.FormInstanceId == instance.Id);
		var valueMap = values.ToDictionary(x => x.FieldCode, x => UnwrapValue(x.ValueJson), StringComparer.OrdinalIgnoreCase);
		var errors = new List<string>();
		var fieldErrors = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		foreach (var field in fields)
		{
			var visible = IsVisible(field.VisibilityRuleJson, valueMap) && IsVisible(sectionVisibility.GetValueOrDefault(field.SectionId), valueMap);
			var value = valueMap.TryGetValue(field.Code, out var current) ? current : string.Empty;
			if (visible && field.Required && string.IsNullOrWhiteSpace(value)) AddFieldError(field, "required", errors, fieldErrors);
			if (visible && !string.IsNullOrWhiteSpace(value)) foreach (var error in ValidateValue(field, value)) { errors.Add(error); fieldErrors.TryAdd(field.Code, error); }
		}
		if (errors.Count > 0) return new AdmissionApplicationSubmitResultDto { Application = Map(application), ValidationErrors = errors, FieldErrors = fieldErrors };
		application.Submit();
		var workflow = await workflowInstanceRepository.FindAsync(x => x.ApplicationId == id);
		if (workflow != null)
		{
			var stageInstances = await stageInstanceRepository.GetListAsync(x => x.WorkflowInstanceId == workflow.Id);
			var current = stageInstances.FirstOrDefault(x => x.Id == application.CurrentStageInstanceId);
			if (current != null)
			{
				current.Complete();
				await stageInstanceRepository.UpdateAsync(current, false);
				await activityRepository.InsertAsync(new AdmissionActivity(GuidGenerator.Create(), CurrentTenant.Id, "WorkflowStageCompleted", $"Workflow stage completed: {current.Name}", application.ApplicantId, application.Id), false);
				var transitions = await transitionRepository.GetListAsync(x => x.WorkflowDefinitionId == application.WorkflowDefinitionId && x.FromStageId == current.DefinitionStageId);
				var nextDefinitionId = transitions.OrderBy(x => x.Priority).Select(x => (Guid?)x.ToStageId).FirstOrDefault();
				var next = nextDefinitionId.HasValue ? stageInstances.FirstOrDefault(x => x.DefinitionStageId == nextDefinitionId.Value && x.Status == AdmissionRuntimeStageStatus.Pending) : null;
				if (next != null)
				{
					next.Enter();
					application.SetCurrentStage(next.Id);
					await stageInstanceRepository.UpdateAsync(next, false);
					await activityRepository.InsertAsync(new AdmissionActivity(GuidGenerator.Create(), CurrentTenant.Id, "WorkflowStageEntered", $"Workflow stage entered: {next.Name}", application.ApplicantId, application.Id), false);
				}
			}
		}
		await applicationRepository.UpdateAsync(application, false); await activityRepository.InsertAsync(new AdmissionActivity(GuidGenerator.Create(), CurrentTenant.Id, "ApplicationSubmitted", "Application submitted", application.ApplicantId, application.Id), true);
		return new AdmissionApplicationSubmitResultDto { Application = Map(application) };
	}

	private async Task<AdmissionApplication> GetApplicationAsync(Guid id) => await applicationRepository.FindAsync(id) ?? throw new EntityNotFoundException(typeof(AdmissionApplication), id);
	private static AdmissionApplicationDto Map(AdmissionApplication x, string? applicantName = null, string offeringCode = "", string currentStageName = "") => new() { Id = x.Id, ConcurrencyStamp = x.ConcurrencyStamp, ApplicationNumber = x.ApplicationNumber, ApplicantId = x.ApplicantId, ApplicantName = applicantName ?? string.Empty, AdmissionCycleId = x.AdmissionCycleId, AdmissionOfferingId = x.AdmissionOfferingId, OfferingCode = offeringCode, CurrentStageName = currentStageName, Status = x.Status, CurrentStageInstanceId = x.CurrentStageInstanceId, LastSavedAt = x.LastSavedAt, SubmittedAt = x.SubmittedAt, CreationTime = x.CreationTime, CreatorId = x.CreatorId, LastModificationTime = x.LastModificationTime, LastModifierId = x.LastModifierId };
	private static object? BoundValue(string key, AdmissionApplicant applicant, AdmissionGuardian? guardian) => key switch { "Applicant.FirstName" => applicant.FirstName, "Applicant.MiddleName" => applicant.MiddleName, "Applicant.LastName" => applicant.LastName, "Applicant.DateOfBirth" => applicant.DateOfBirth, "Applicant.Gender" => applicant.Gender, "Guardian.FirstName" => guardian?.FirstName, "Guardian.LastName" => guardian?.LastName, "Guardian.Mobile" => guardian?.Mobile, "Guardian.Email" => guardian?.Email, _ => null };
	private static string UnwrapValue(string? value) { if (string.IsNullOrWhiteSpace(value) || value == "null") return string.Empty; try { using var json = JsonDocument.Parse(value); return json.RootElement.ValueKind == JsonValueKind.String ? json.RootElement.GetString() ?? string.Empty : json.RootElement.ToString(); } catch { return value; } }
	private static bool IsVisible(string? ruleJson, IReadOnlyDictionary<string, string> values) { if (string.IsNullOrWhiteSpace(ruleJson)) return true; try { using var doc = JsonDocument.Parse(ruleJson); return EvaluateRule(doc.RootElement, values); } catch { return false; } }
	private static bool EvaluateRule(JsonElement rule, IReadOnlyDictionary<string, string> values) { var op = rule.TryGetProperty("operator", out var operatorProperty) ? operatorProperty.GetString() : "ALL"; if (rule.TryGetProperty("conditions", out var conditions) && conditions.ValueKind == JsonValueKind.Array) { var results = conditions.EnumerateArray().Select(x => EvaluateRule(x, values)).ToList(); return string.Equals(op, "ANY", StringComparison.OrdinalIgnoreCase) ? results.Any(x => x) : results.All(x => x); } var field = rule.TryGetProperty("field", out var fieldProperty) ? fieldProperty.GetString() : null; var current = field != null && values.TryGetValue(field, out var fieldValue) ? fieldValue : string.Empty; var expected = rule.TryGetProperty("value", out var valueProperty) ? valueProperty.ToString() : string.Empty; return op?.ToLowerInvariant() switch { "equals" => string.Equals(current, expected, StringComparison.OrdinalIgnoreCase) || string.Equals(current, expected.Trim('"'), StringComparison.OrdinalIgnoreCase), "notequals" => !string.Equals(current, expected, StringComparison.OrdinalIgnoreCase), "contains" => current.Contains(expected.Trim('"'), StringComparison.OrdinalIgnoreCase), "isempty" => string.IsNullOrWhiteSpace(current), "isnotempty" => !string.IsNullOrWhiteSpace(current), "greaterthan" => decimal.TryParse(current, out var gt) && decimal.TryParse(expected, out var ge) && gt > ge, "lessthan" => decimal.TryParse(current, out var lt) && decimal.TryParse(expected, out var le) && lt < le, _ => true };
	}
	private static void AddFieldError(AdmissionFormFieldDefinition field, string rule, List<string> errors, IDictionary<string, string> fieldErrors) { var message = GetCustomMessage(field, rule) ?? $"{field.Code} is required."; errors.Add(message); fieldErrors.TryAdd(field.Code, message); }
	private static string? GetCustomMessage(AdmissionFormFieldDefinition field, string rule) { if (string.IsNullOrWhiteSpace(field.ValidationJson)) return null; try { using var doc = JsonDocument.Parse(field.ValidationJson); var root = doc.RootElement; if (root.TryGetProperty("messages", out var messages) && messages.TryGetProperty(rule, out var ruleMessage)) return ruleMessage.GetString(); if (root.TryGetProperty("message", out var message)) return message.GetString(); } catch { } return null; }
	private static IEnumerable<string> ValidateValue(AdmissionFormFieldDefinition field, string value) { var errors = new List<string>(); if (string.IsNullOrWhiteSpace(field.ValidationJson)) return errors; try { using var doc = JsonDocument.Parse(field.ValidationJson); var root = doc.RootElement; string Message(string rule, string fallback) => GetCustomMessage(field, rule) ?? fallback; if (root.TryGetProperty("minLength", out var min) && value.Length < min.GetInt32()) errors.Add(Message("minLength", $"{field.Code} is too short.")); if (root.TryGetProperty("maxLength", out var max) && value.Length > max.GetInt32()) errors.Add(Message("maxLength", $"{field.Code} is too long.")); if (root.TryGetProperty("min", out var minimum) && (!decimal.TryParse(value, out var number) || number < minimum.GetDecimal())) errors.Add(Message("min", $"{field.Code} is below the minimum value.")); if (root.TryGetProperty("max", out var maximum) && (!decimal.TryParse(value, out var numberForMax) || numberForMax > maximum.GetDecimal())) errors.Add(Message("max", $"{field.Code} exceeds the maximum value.")); if (root.TryGetProperty("regex", out var regex) && !Regex.IsMatch(value, regex.GetString() ?? string.Empty)) errors.Add(Message("regex", $"{field.Code} has an invalid format.")); if (root.TryGetProperty("email", out var email) && email.GetBoolean()) { try { var address = new System.Net.Mail.MailAddress(value); if (!address.Address.Equals(value, StringComparison.OrdinalIgnoreCase)) errors.Add(Message("email", $"{field.Code} must be a valid email.")); } catch { errors.Add(Message("email", $"{field.Code} must be a valid email.")); } } if ((root.TryGetProperty("phone", out var phone) && phone.GetBoolean()) || field.FieldType == AdmissionFormFieldType.Phone) { if (!Regex.IsMatch(value, @"^\+?[0-9 ()-]{7,20}$")) errors.Add(Message("phone", $"{field.Code} must be a valid phone number.")); } if (field.FieldType == AdmissionFormFieldType.Date && DateTime.TryParse(value, out var date)) { if (root.TryGetProperty("minDate", out var minDate) && date.Date < minDate.GetDateTime().Date) errors.Add(Message("minDate", $"{field.Code} is before the allowed date.")); if (root.TryGetProperty("maxDate", out var maxDate) && date.Date > maxDate.GetDateTime().Date) errors.Add(Message("maxDate", $"{field.Code} is after the allowed date.")); } } catch { errors.Add($"{field.Code} has invalid validation configuration."); } return errors; }
}
