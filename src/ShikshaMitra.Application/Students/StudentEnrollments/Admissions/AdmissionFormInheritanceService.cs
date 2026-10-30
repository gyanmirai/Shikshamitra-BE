using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

/// <summary>Loads tenant-owned definitions and composes one exact source version, without writing them.</summary>
[RemoteService(false)]
public class AdmissionFormInheritanceService(
	IRepository<AdmissionFormDefinition, Guid> forms,
	IRepository<AdmissionFormSectionDefinition, Guid> sections,
	IRepository<AdmissionFormFieldDefinition, Guid> fields,
	AdmissionApplicationScopeAccessService applicationScope) : ApplicationService, ITransientDependency
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

	public async Task<AdmissionFormInheritancePreviewDto> ComposeAsync(
		Guid registrationFormId, Guid? inquiryFormId, bool requireApprovedSource = true)
	{
		var registration = await ReadDefinitionAsync(registrationFormId);
		if (registration.Purpose != AdmissionFormPurpose.Registration)
			throw new BusinessException(AdmissionFormInheritanceErrorCodes.InvalidPolicy, "Choose a Registration form for inheritance.");
		if (registration.Inheritance?.Enabled != true || !inquiryFormId.HasValue)
			return new() { Definition = registration };
		var inquiry = await ReadDefinitionAsync(inquiryFormId.Value);
		if (inquiry.Status is not (AdmissionFormStatus.Published or AdmissionFormStatus.Archived))
			throw new BusinessException(AdmissionFormInheritanceErrorCodes.IncompatibleSource, "The source Inquiry version is not published.");
		return Compose(registration, inquiry, requireApprovedSource);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionForms.Manage)]
	public async Task<AdmissionFormInheritancePreviewDto> PreviewAsync(AdmissionFormInheritancePreviewInput input)
	{
		if (input.Draft?.Inheritance != null) AdmissionFormInheritanceMetadataStore.Validate(input.Draft.Inheritance);
		if (input.Draft != null && (input.Draft.Sections == null || input.Draft.Sections.Any(section => section == null || section.Fields == null || section.Fields.Any(field => field == null))))
			throw new BusinessException(AdmissionFormErrorCodes.InvalidDefinition, "Review the Registration sections and fields before previewing inheritance.");
		AdmissionFormDto registration;
		AdmissionFormDto? persistedRegistration = null;
		if (input.RegistrationFormDefinitionId.HasValue)
		{
			registration = await ReadDefinitionAsync(input.RegistrationFormDefinitionId.Value);
			persistedRegistration = registration;
			if (input.Draft != null)
			{
				if (registration.Status != AdmissionFormStatus.Draft || input.Draft.ConcurrencyStamp != registration.ConcurrencyStamp)
					throw new BusinessException("Admissions.FormConcurrencyConflict", "Reload the current draft before previewing inheritance.");
				registration = DraftDefinition(input.Draft, registration.Id);
			}
		}
		else if (input.Draft != null) registration = DraftDefinition(input.Draft, Guid.NewGuid());
		else throw new BusinessException(AdmissionFormInheritanceErrorCodes.InvalidPolicy, "Provide a Registration draft to preview.");
		if (registration.Purpose != AdmissionFormPurpose.Registration)
			throw new BusinessException(AdmissionFormInheritanceErrorCodes.InvalidPolicy, "Only Registration forms can inherit Inquiry fields.");
		var inquiry = await ReadDefinitionAsync(input.InquiryFormDefinitionId);
		var retainedArchivedSource = inquiry.Status == AdmissionFormStatus.Archived && persistedRegistration != null &&
			await IsRetainedArchivedSourceAsync(persistedRegistration, inquiry.Id);
		if (inquiry.Status != AdmissionFormStatus.Published && !retainedArchivedSource)
			throw new BusinessException(AdmissionFormInheritanceErrorCodes.IncompatibleSource, "Choose a published Inquiry version or a retained version captured on a connected admission route.");
		return Compose(registration, inquiry, requireApprovedSource: false);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionForms.ViewUsage)]
	public async Task<PagedResultDto<AdmissionFormInheritanceSourceSummaryDto>> GetSourcesAsync(Guid id, PagedResultRequestDto input)
	{
		var registration = await ReadDefinitionAsync(id);
		if (registration.Purpose != AdmissionFormPurpose.Registration)
			throw new BusinessException(AdmissionFormInheritanceErrorCodes.InvalidPolicy, "Only Registration forms have connected Inquiry sources.");
		var connections = await ReadSourceConnectionsAsync(registration);
		var approvedIds = registration.Inheritance?.Sources.Select(source => source.InquiryFormDefinitionId).ToHashSet() ?? [];
		var sourceIds = connections.Select(connection => connection.SourceId).Concat(approvedIds).Distinct().ToArray();
		List<AdmissionFormDefinition> sourceForms = sourceIds.Length == 0 ? [] : await forms.GetListAsync(form => sourceIds.Contains(form.Id) && form.TenantId == CurrentTenant.Id);
		var page = sourceForms.OrderBy(form => form.Name).ThenByDescending(form => form.Version)
			.Skip(Math.Max(0, input.SkipCount)).Take(Math.Clamp(input.MaxResultCount, 1, 100));
		return new(sourceForms.Count, page.Select(form => new AdmissionFormInheritanceSourceSummaryDto
		{
			InquiryFormDefinitionId = form.Id, Name = form.Name, Version = form.Version, IsApproved = approvedIds.Contains(form.Id),
			Connections = connections.Where(connection => connection.SourceId == form.Id)
				.Select(connection => new AdmissionFormInheritanceConnectionDto { OfferingId = connection.Id, OfferingName = connection.Name }).ToList()
		}).ToList());
	}

	private sealed record SourceConnection(Guid Id, string Name, Guid SourceId);

	private async Task<bool> IsRetainedArchivedSourceAsync(AdmissionFormDto registration, Guid sourceId)
	{
		// A draft's own submitted Sources list is not proof of a prior approval.
		var publishedFamily = await forms.GetListAsync(form => form.TenantId == CurrentTenant.Id && form.Code == registration.Code &&
			form.Purpose == AdmissionFormPurpose.Registration && (form.Status == AdmissionFormStatus.Published || form.Status == AdmissionFormStatus.Archived));
		if (publishedFamily.Any(form => AdmissionFormInheritanceMetadataStore.ReadPolicy(form) is { Enabled: true } policy &&
			policy.Sources.Any(source => source.InquiryFormDefinitionId == sourceId))) return true;
		return (await ReadSourceConnectionsAsync(registration, sourceId)).Count > 0;
	}

	private async Task<List<SourceConnection>> ReadSourceConnectionsAsync(AdmissionFormDto registration, Guid? capturedSourceOnly = null)
	{
		var familyIds = (await forms.GetListAsync(form => form.Code == registration.Code && form.TenantId == CurrentTenant.Id)).Select(form => form.Id).ToArray();
		var scoped = await applicationScope.GetScopedOfferingsAsync();
		var routes = await AsyncExecuter.ToListAsync(scoped.Where(offering => offering.ApplicationFormDefinitionId.HasValue &&
			familyIds.Contains(offering.ApplicationFormDefinitionId.Value))
			.Select(offering => new { offering.Id, offering.Name, offering.InquiryFormDefinitionId }));
		var connections = capturedSourceOnly.HasValue ? new List<SourceConnection>() : routes
			.Where(route => route.InquiryFormDefinitionId.HasValue).Select(route => new SourceConnection(route.Id, route.Name, route.InquiryFormDefinitionId!.Value)).ToList();
		if (routes.Count == 0) return connections;
		var routesById = routes.ToDictionary(route => route.Id);
		var routeIds = routesById.Keys.ToArray();
		var inquiries = await LazyServiceProvider.LazyGetRequiredService<IRepository<AdmissionInquiry, Guid>>().GetQueryableAsync();
		var history = inquiries.Where(inquiry => inquiry.TenantId == CurrentTenant.Id && !inquiry.IsDeleted &&
			inquiry.AdmissionOfferingId.HasValue && routeIds.Contains(inquiry.AdmissionOfferingId.Value));
		const int batchSize = 200;
		var skip = 0;
		while (true)
		{
			// FormResponseDefinitionId is an unmapped ExtraProperties accessor. Project only metadata,
			// extract its form reference after a bounded scoped read, and never return captured answers.
			var batch = await AsyncExecuter.ToListAsync(history.OrderBy(inquiry => inquiry.Id).Skip(skip).Take(batchSize)
				.Select(inquiry => new { OfferingId = inquiry.AdmissionOfferingId!.Value, Metadata = inquiry.ExtraProperties }));
			foreach (var row in batch)
			{
				if (!row.Metadata.TryGetValue("AdmissionFormResponseDefinitionId", out var stored) ||
					!Guid.TryParse(stored?.ToString(), out var sourceId) || sourceId == Guid.Empty ||
					(capturedSourceOnly.HasValue && sourceId != capturedSourceOnly.Value)) continue;
				var route = routesById[row.OfferingId];
				connections.Add(new(route.Id, route.Name, sourceId));
				if (capturedSourceOnly.HasValue) return connections;
			}
			if (batch.Count < batchSize) break;
			skip += batch.Count;
		}
		return connections.Distinct().ToList();
	}

	public async Task<List<AdmissionFormInheritanceIssueDto>> ValidatePolicyAsync(Guid registrationId)
	{
		var registration = await ReadDefinitionAsync(registrationId);
		if (registration.Inheritance?.Enabled != true) return [];
		if (registration.Inheritance.Sources.Count == 0)
			return [new() { Code = "NoApprovedSources", Message = "Approve at least one published Inquiry version before publishing inherited fields." }];
		var issues = new List<AdmissionFormInheritanceIssueDto>();
		foreach (var source in registration.Inheritance.Sources)
		{
			var inquiry = await ReadDefinitionAsync(source.InquiryFormDefinitionId);
			if (inquiry.Purpose != AdmissionFormPurpose.Inquiry || inquiry.Status is not (AdmissionFormStatus.Published or AdmissionFormStatus.Archived) ||
				(inquiry.Status == AdmissionFormStatus.Archived && !await IsRetainedArchivedSourceAsync(registration, inquiry.Id)))
				issues.Add(new() { Code = "InvalidSource", Message = "An approved source is not an available published Inquiry version." });
			else
			{
				var composed = Compose(registration, inquiry, true);
				issues.AddRange(composed.Issues);
				if (!composed.Definition.Sections.SelectMany(section => section.Fields).Any(field => field.FieldType != AdmissionFormFieldType.Static))
					issues.Add(new() { Code = "EmptyRegistration", Message = "Each connected Inquiry must supply Registration questions, or add a Registration-only question." });
				if (registration.LayoutType == AdmissionFormLayoutType.Stepper && composed.Definition.Sections.GroupBy(section => section.StepId ?? section.Id).Count() < 2)
					issues.Add(new() { Code = "InsufficientSteps", Message = "The resulting Registration form needs at least two steps for a stepper layout." });
			}
		}
		return issues;
	}

	public async Task<AdmissionFormDto> ReadDefinitionAsync(Guid id)
	{
		var form = await forms.FindAsync(form => form.Id == id && form.TenantId == CurrentTenant.Id) ??
			throw new EntityNotFoundException(typeof(AdmissionFormDefinition), id);
		return AdmissionFormAppService.Map(form,
			(await sections.GetListAsync(section => section.FormDefinitionId == id)).OrderBy(section => section.DisplayOrder).ToList(),
			(await fields.GetListAsync(field => field.FormDefinitionId == id)).OrderBy(field => field.DisplayOrder).ToList());
	}

	public static AdmissionFormInheritancePreviewDto Compose(AdmissionFormDto registration, AdmissionFormDto inquiry, bool requireApprovedSource)
	{
		if (inquiry.Purpose != AdmissionFormPurpose.Inquiry)
			return new() { Definition = registration, Issues = [new() { Code = "InvalidSource", Message = "Choose an Inquiry form as the source." }] };
		var structuralIssues = ValidatePreviewRules(registration).Concat(ValidatePreviewRules(inquiry)).ToList();
		if (structuralIssues.Count > 0) return new() { Definition = registration, InquiryFormDefinitionId = inquiry.Id, Issues = structuralIssues };
		var policy = Convert<AdmissionFormInheritancePolicy>(registration.Inheritance ?? new AdmissionFormInheritancePolicyDto());
		if (!requireApprovedSource)
		{
			policy.Enabled = true;
			if (policy.Sources.All(source => source.InquiryFormDefinitionId != inquiry.Id)) policy.Sources.Add(new() { InquiryFormDefinitionId = inquiry.Id });
		}
		var composition = AdmissionFormInheritanceComposer.Compose(Convert<AdmissionFormSchema>(registration), Convert<AdmissionFormSchema>(inquiry), policy);
		var definition = Convert<AdmissionFormDto>(composition.Definition);
		definition.Status = registration.Status;
		definition.ConcurrencyStamp = registration.ConcurrencyStamp;
		definition.Inheritance = registration.Inheritance;
		definition.SectionCount = definition.Sections.Count;
		var contextSections = Convert<List<AdmissionFormSectionDto>>(composition.InquiryContextSections);
		var issues = Convert<List<AdmissionFormInheritanceIssueDto>>(composition.Issues);
		if (issues.Count == 0) issues.AddRange(ValidatePreviewRules(definition, contextSections));
		return new()
		{
			Definition = definition, InquiryFormDefinitionId = inquiry.Id,
			InquiryContextSections = contextSections,
			Issues = issues
		};
	}

	private static List<AdmissionFormInheritanceIssueDto> ValidatePreviewRules(AdmissionFormDto form, IReadOnlyCollection<AdmissionFormSectionDto>? contextSections = null)
	{
		var validation = new AdmissionFormValidationResultDto();
		var validationSections = form.Sections.Concat(contextSections ?? []).ToList();
		var fields = validationSections.SelectMany(section => section.Fields).ToList();
		var codes = fields.Select(field => field.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
		if (fields.Any(field => string.IsNullOrWhiteSpace(field.Code)) || codes.Count != fields.Count)
			validation.Errors.Add(AdmissionFormErrorCodes.DuplicateFieldCode);
		if (fields.Any(field => string.IsNullOrWhiteSpace(field.Label) || field.FormDefinitionId == Guid.Empty || field.SectionId == Guid.Empty))
			validation.Errors.Add(AdmissionFormErrorCodes.InvalidDefinition);
		if (form.Sections.GroupBy(section => section.Code, StringComparer.OrdinalIgnoreCase).Any(group => group.Count() > 1))
			validation.Errors.Add("Admissions.FormDuplicateSectionCode");
		if (validation.Errors.Count > 0) return PreviewIssues(validation);
		var materializedFields = AdmissionEffectiveFormResolver.MaterializeFields(new AdmissionFormDto { Id = form.Id, Sections = validationSections });
		var definitionsByCode = materializedFields.ToDictionary(field => field.Code, StringComparer.OrdinalIgnoreCase);
		foreach (var section in validationSections)
		{
			AdmissionFormAppService.ValidateRule(section.VisibilityRuleJson, codes, section.Code, validation);
			AdmissionFormAppService.ValidateRule(section.StepVisibilityRuleJson, codes, section.Code, validation);
			foreach (var field in section.Fields)
			{
				AdmissionFormAppService.ValidateRule(field.VisibilityRuleJson, codes, field.Code, validation);
				AdmissionFormAppService.ValidateRule(field.RequiredRuleJson, codes, field.Code, validation);
				AdmissionFormAppService.ValidateValidationJson(field.ValidationJson, field.Code, codes, validation);
				if (field.FieldType is AdmissionFormFieldType.Select or AdmissionFormFieldType.MultiSelect or AdmissionFormFieldType.Radio)
				{
					AdmissionFormAppService.ValidateOptions(definitionsByCode[field.Code], codes, validation);
				}
			}
		}
		if (validation.Errors.Count == 0) AdmissionFormAppService.ValidateDependencyCycles(materializedFields, validation);
		return PreviewIssues(validation);
	}

	private static List<AdmissionFormInheritanceIssueDto> PreviewIssues(AdmissionFormValidationResultDto validation) =>
		validation.Errors.Distinct().Select(error => new AdmissionFormInheritanceIssueDto
		{ Code = "InvalidDefinition", Message = $"Review the form's field codes, choices and rules before enabling inheritance ({error})." }).ToList();

	private static AdmissionFormDto DraftDefinition(CreateUpdateAdmissionFormDto input, Guid id)
	{
		var node = JsonSerializer.SerializeToNode(input, JsonOptions)!.AsObject();
		node["id"] = id;
		foreach (var section in node["sections"]!.AsArray())
		{
			var sectionObject = section!.AsObject();
			if (sectionObject["id"] == null) sectionObject["id"] = Guid.NewGuid();
			sectionObject["formDefinitionId"] = id;
			foreach (var field in sectionObject["fields"]!.AsArray())
			{
				var fieldObject = field!.AsObject();
				if (fieldObject["id"] == null || fieldObject["id"]!.GetValue<Guid>() == Guid.Empty) fieldObject["id"] = Guid.NewGuid();
				if (fieldObject["bindingType"]?.GetValue<int>() == (int)AdmissionFormBindingType.Custom &&
					string.IsNullOrWhiteSpace(fieldObject["fieldIdentity"]?.GetValue<string>()))
					fieldObject["fieldIdentity"] = "custom:" + fieldObject["id"]!.GetValue<Guid>().ToString("D");
				fieldObject["sectionId"] = sectionObject["id"]!.DeepClone();
				fieldObject["formDefinitionId"] = id;
			}
		}
		return node.Deserialize<AdmissionFormDto>(JsonOptions)!;
	}

	private static T Convert<T>(object value) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value, JsonOptions), JsonOptions)!;
}
