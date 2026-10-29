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
public class AdmissionFormAppService(
	IRepository<AdmissionFormDefinition, Guid> formRepository,
	IRepository<AdmissionFormSectionDefinition, Guid> sectionRepository,
	IRepository<AdmissionFormFieldDefinition, Guid> fieldRepository,
	IRepository<AdmissionOffering, Guid> offeringRepository,
	IRepository<AdmissionApplication, Guid> applicationRepository,
	IRepository<AdmissionPublicInquiryLink, Guid> publicInquiryLinkRepository,
	AdmissionFormDataSourceRegistry dataSourceRegistry)
	: ApplicationService, IAdmissionFormAppService
{
	private static readonly IReadOnlyList<AdmissionFormFieldMetadataDto> FieldTypes =
	[
		new() { FieldType = AdmissionFormFieldType.Text, DisplayName = "Text", Description = "Single-line text.", SupportsBinding = true },
		new() { FieldType = AdmissionFormFieldType.Textarea, DisplayName = "Textarea", Description = "Multi-line text.", SupportsBinding = true },
		new() { FieldType = AdmissionFormFieldType.Number, DisplayName = "Number", Description = "Numeric value.", SupportsBinding = true },
		new() { FieldType = AdmissionFormFieldType.Email, DisplayName = "Email", Description = "Email address.", SupportsBinding = true },
		new() { FieldType = AdmissionFormFieldType.Phone, DisplayName = "Phone", Description = "Phone number.", SupportsBinding = true },
		new() { FieldType = AdmissionFormFieldType.Date, DisplayName = "Date", Description = "Calendar date.", SupportsBinding = true },
		new() { FieldType = AdmissionFormFieldType.Select, DisplayName = "Select", Description = "One option from a list.", SupportsOptions = true, SupportsBinding = true },
		new() { FieldType = AdmissionFormFieldType.MultiSelect, DisplayName = "Multi-select", Description = "Multiple options from a list.", SupportsOptions = true, SupportsBinding = true },
		new() { FieldType = AdmissionFormFieldType.Radio, DisplayName = "Radio", Description = "One option shown as radio buttons.", SupportsOptions = true },
		new() { FieldType = AdmissionFormFieldType.Checkbox, DisplayName = "Checkbox", Description = "Boolean choice.", SupportsBinding = true },
		new() { FieldType = AdmissionFormFieldType.File, DisplayName = "File", Description = "File placeholder for the upload integration.", SupportsBinding = false, IsRuntimeReady = false },
		new() { FieldType = AdmissionFormFieldType.AddressGroup, DisplayName = "Address group", Description = "Structured address group.", SupportsBinding = false },
		new() { FieldType = AdmissionFormFieldType.Static, DisplayName = "Static text", Description = "Non-input informational content.", SupportsBinding = false }
	];
	private static readonly string[] RuleOperators = [
		"Equals", "NotEquals", "Contains", "NotContains", "StartsWith", "EndsWith", "IsEmpty", "IsNotEmpty",
		"GreaterThan", "GreaterThanOrEqual", "LessThan", "LessThanOrEqual", "In", "NotIn"
	];

	private static void EnsureSupportedPurpose(AdmissionFormPurpose purpose)
	{
		if (purpose is not (AdmissionFormPurpose.Inquiry or AdmissionFormPurpose.Registration))
			throw new BusinessException(AdmissionFormErrorCodes.InvalidDefinition,
				"Only Inquiry and Registration forms are supported.");
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionForms.View)]
	public async Task<PagedResultDto<AdmissionFormDto>> GetListAsync(AdmissionFormListInput input)
	{
		var query = await formRepository.GetQueryableAsync();
		query = query.Where(form =>
			(form.Purpose == AdmissionFormPurpose.Inquiry || form.Purpose == AdmissionFormPurpose.Registration) &&
			(!input.Status.HasValue || form.Status == input.Status) &&
			(!input.Purpose.HasValue || form.Purpose == input.Purpose) &&
			(!input.Version.HasValue || form.Version == input.Version) &&
			(string.IsNullOrWhiteSpace(input.Search) || form.Name.Contains(input.Search) || form.Code.Contains(input.Search)));
		var sectionQuery = await sectionRepository.GetQueryableAsync();
		if (!string.IsNullOrEmpty(input.ColumnFilters))
		{
			var filterRows = AdmissionFormColumnFilters.Apply(query.Select(form => new AdmissionFormColumnFilterRow
			{
				Id = form.Id,
				Name = form.Name,
				Version = form.Version,
				SectionCount = sectionQuery.Count(section => section.FormDefinitionId == form.Id)
			}), input.ColumnFilters);
			query = query.Where(form => filterRows.Any(row => row.Id == form.Id));
		}
		var count = await AsyncExecuter.CountAsync(query);
		var rows = await AsyncExecuter.ToListAsync(query.OrderByDescending(form => form.CreationTime)
			.Skip(input.SkipCount).Take(input.MaxResultCount));
		var rowIds = rows.Select(form => form.Id).ToList();
		var sectionCounts = rowIds.Count == 0
			? new Dictionary<Guid, int>()
			: (await AsyncExecuter.ToListAsync(sectionQuery
				.Where(section => rowIds.Contains(section.FormDefinitionId))))
				.GroupBy(section => section.FormDefinitionId)
				.ToDictionary(group => group.Key, group => group.Count());
		var items = rows.Select(form =>
		{
			var dto = Map(form, [], []);
			dto.SectionCount = sectionCounts.GetValueOrDefault(form.Id);
			return dto;
		}).ToList();
		return new PagedResultDto<AdmissionFormDto>(count, items);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionForms.View)]
	public async Task<AdmissionFormDto> GetAsync(Guid id) => await MapAsync(await GetFormAsync(id));

	// Only scoped staff intake owners call this; unpublished management data remains protected.
	internal async Task<AdmissionFormDto> GetPublishedIntakeAsync(Guid id)
	{
		var form = await GetFormAsync(id);
		if (form.Status != AdmissionFormStatus.Published || form.Purpose != AdmissionFormPurpose.Inquiry)
			throw new BusinessException(AdmissionApplicationErrorCodes.ConfigurationNotPublished);
		return await MapAsync(form);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionForms.View)]
	public Task<AdmissionFormDto> PreviewAsync(Guid id) => GetAsync(id);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionForms.Manage)]
	public async Task<AdmissionFormDto> CreateAsync(CreateUpdateAdmissionFormDto input)
	{
		EnsureSupportedPurpose(input.Purpose);
		var id = GuidGenerator.Create();
		var code = AdmissionSettingsCode.ForCreate(input.Code, "FORM", id);
		await EnsureCodeAvailableAsync(code, null);
		var form = new AdmissionFormDefinition(id, CurrentTenant.Id, input.Name, code,
			input.LayoutType, description: input.Description, fieldsPerRow: input.FieldsPerRow, purpose: input.Purpose);
		AdmissionFormMetadataStore.WriteExperience(form, input);
		AdmissionFormMetadataStore.AddAudit(form, "Created", CurrentUser.Id);
		await formRepository.InsertAsync(form, autoSave: false);
		await ReplaceChildrenAsync(form.Id, input);
		await CurrentUnitOfWork!.SaveChangesAsync();
		return await MapAsync(form);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionForms.Manage)]
	public async Task<AdmissionFormDto> UpdateAsync(Guid id, CreateUpdateAdmissionFormDto input)
	{
		EnsureSupportedPurpose(input.Purpose);
		var form = await GetFormAsync(id);
		EnsureConcurrency(form, input.ConcurrencyStamp);
		var code = AdmissionSettingsCode.ForUpdate(input.Code, form.Code);
		if (!string.Equals(code, form.Code, StringComparison.Ordinal))
			await EnsureCodeAvailableAsync(code, id);
		form.UpdateDetails(input.Name, code, input.LayoutType, input.Description, input.FieldsPerRow, input.Purpose);
		AdmissionFormMetadataStore.WriteExperience(form, input);
		AdmissionFormMetadataStore.AddAudit(form, "Updated", CurrentUser.Id);
		await ReplaceChildrenAsync(id, input);
		await formRepository.UpdateAsync(form, autoSave: false);
		await CurrentUnitOfWork!.SaveChangesAsync();
		return await MapAsync(form);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionForms.Manage)]
	public async Task<AdmissionFormDto> CloneAsync(Guid id, CloneAdmissionFormDto input)
	{
		var source = await GetFormAsync(id);
		EnsureSupportedPurpose(source.Purpose);
		var cloneId = GuidGenerator.Create();
		var code = AdmissionSettingsCode.ForCreate(input.Code, "FORM", cloneId);
		var name = string.IsNullOrWhiteSpace(input.Name) ? $"{source.Name} copy" : input.Name;
		await EnsureCodeAvailableAsync(code, null);
		var clone = new AdmissionFormDefinition(cloneId, CurrentTenant.Id, name, code,
			source.LayoutType, description: input.Description ?? source.Description, fieldsPerRow: source.FieldsPerRow, purpose: source.Purpose);
		AdmissionFormMetadataStore.CopyExperience(source, clone);
		AdmissionFormMetadataStore.AddAudit(clone, "Cloned", CurrentUser.Id, $"Source form {source.Id}");
		await formRepository.InsertAsync(clone, autoSave: false);
		await CopyChildrenAsync(source.Id, clone.Id);
		await CurrentUnitOfWork!.SaveChangesAsync();
		return await MapAsync(clone);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionForms.CreateVersion)]
	public async Task<AdmissionFormDto> CreateNewVersionAsync(Guid id)
	{
		var source = await GetFormAsync(id);
		EnsureSupportedPurpose(source.Purpose);
		if (source.Status == AdmissionFormStatus.Draft) throw new BusinessException(AdmissionFormErrorCodes.DraftRequired);
		var query = await formRepository.GetQueryableAsync();
		var versions = await AsyncExecuter.ToListAsync(query.Where(form => form.Code == source.Code));
		if (versions.Any(form => form.Status == AdmissionFormStatus.Draft)) throw new BusinessException(AdmissionFormErrorCodes.DraftRequired);
		var version = new AdmissionFormDefinition(GuidGenerator.Create(), CurrentTenant.Id, source.Name, source.Code,
			source.LayoutType, versions.Max(form => form.Version) + 1, source.Id, source.Description, source.FieldsPerRow, source.Purpose);
		AdmissionFormMetadataStore.CopyExperience(source, version);
		AdmissionFormMetadataStore.AddAudit(version, "VersionCreated", CurrentUser.Id, $"Based on version {source.Version}");
		await formRepository.InsertAsync(version, autoSave: false);
		await CopyChildrenAsync(source.Id, version.Id);
		await CurrentUnitOfWork!.SaveChangesAsync();
		return await MapAsync(version);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionForms.Delete)]
	public async Task DeleteAsync(Guid id, string? concurrencyStamp = null)
	{
		var form = await GetFormAsync(id);
		EnsureConcurrency(form, concurrencyStamp);
		if (form.Status != AdmissionFormStatus.Draft) throw new BusinessException(AdmissionFormErrorCodes.DraftRequired);
		var usage = await GetUsageAsync(id);
		if (!usage.CanDelete)
			throw new BusinessException(AdmissionFormErrorCodes.FormInUse)
				.WithData(nameof(usage.OfferingCount), usage.OfferingCount)
				.WithData(nameof(usage.ApplicationCount), usage.ApplicationCount)
				.WithData(nameof(usage.PublicLinkCount), usage.PublicLinkCount);
		await formRepository.DeleteAsync(form, autoSave: true);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionForms.Archive)]
	public async Task<AdmissionFormDto> ArchiveAsync(Guid id, string? concurrencyStamp = null)
	{
		var form = await GetFormAsync(id);
		EnsureConcurrency(form, concurrencyStamp);
		form.Archive();
		AdmissionFormMetadataStore.AddAudit(form, "Archived", CurrentUser.Id);
		await formRepository.UpdateAsync(form, autoSave: true);
		return await MapAsync(form);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionForms.View)]
	public async Task<AdmissionFormValidationResultDto> ValidateAsync(Guid id)
	{
		await GetFormAsync(id);
		return await ValidateDefinitionAsync(id);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionForms.Publish)]
	public async Task<AdmissionFormDto> PublishAsync(Guid id, string? concurrencyStamp = null)
	{
		var form = await GetFormAsync(id);
		EnsureConcurrency(form, concurrencyStamp);
		var validation = await ValidateDefinitionAsync(id);
		if (!validation.IsValid) throw new BusinessException(AdmissionFormErrorCodes.InvalidDefinition).WithData("Errors", string.Join(";", validation.Errors));
		form.Publish(CurrentUser.Id);
		AdmissionFormMetadataStore.AddAudit(form, "Published", CurrentUser.Id);
		await formRepository.UpdateAsync(form, autoSave: true);
		return await MapAsync(form);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionForms.View)]
	public async Task<IReadOnlyList<AdmissionFormAuditEntryDto>> GetAuditAsync(Guid id)
	{
		var form = await GetFormAsync(id);
		return AdmissionFormMetadataStore.ReadAudit(form).OrderByDescending(entry => entry.OccurredAt).ToList();
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionForms.View)]
	public async Task<IReadOnlyList<AdmissionFormVersionSummaryDto>> GetVersionsAsync(Guid id)
	{
		var form = await GetFormAsync(id);
		var query = await formRepository.GetQueryableAsync();
		var versions = await AsyncExecuter.ToListAsync(query.Where(item => item.Code == form.Code).OrderByDescending(item => item.Version));
		return versions.Select(item => new AdmissionFormVersionSummaryDto
		{
			Id = item.Id, Version = item.Version, Status = item.Status, CreationTime = item.CreationTime,
			PublishedAt = item.PublishedAt, ParentVersionId = item.ParentVersionId
		}).ToList();
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionForms.View)]
	public async Task<AdmissionFormVersionComparisonDto> CompareVersionsAsync(Guid id, Guid otherId)
	{
		var from = await GetFormAsync(id);
		var to = await GetFormAsync(otherId);
		if (!string.Equals(from.Code, to.Code, StringComparison.OrdinalIgnoreCase))
			throw new BusinessException("Admissions.FormVersionMismatch", "Only versions of the same form can be compared.");
		var fromDto = await MapAsync(from);
		var toDto = await MapAsync(to);
		return new AdmissionFormVersionComparisonDto { FromId = id, ToId = otherId, Changes = Compare(fromDto, toDto) };
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionForms.ViewUsage)]
	public async Task<AdmissionFormUsageDto> GetUsageAsync(Guid id)
	{
		await GetFormAsync(id);
		var offerings = await offeringRepository.GetQueryableAsync();
		var applications = await applicationRepository.GetQueryableAsync();
		var publicLinks = await publicInquiryLinkRepository.GetQueryableAsync();
		var offeringCount = await AsyncExecuter.CountAsync(offerings.Where(offering => offering.InquiryFormDefinitionId == id || offering.ApplicationFormDefinitionId == id));
		var applicationCount = await AsyncExecuter.CountAsync(applications.Where(application => application.FormDefinitionId == id));
		var publicLinkCount = await AsyncExecuter.CountAsync(publicLinks.Where(link => link.InquiryFormDefinitionId == id));
		return new AdmissionFormUsageDto { FormDefinitionId = id, OfferingCount = offeringCount, ApplicationCount = applicationCount, PublicLinkCount = publicLinkCount, CanDelete = offeringCount == 0 && applicationCount == 0 && publicLinkCount == 0 };
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionForms.View)]
	public Task<IReadOnlyList<AdmissionFormFieldMetadataDto>> GetFieldTypesAsync() => Task.FromResult(FieldTypes);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionForms.View)]
	public Task<IReadOnlyList<AdmissionFormSystemFieldMetadataDto>> GetSystemFieldsAsync() => Task.FromResult<IReadOnlyList<AdmissionFormSystemFieldMetadataDto>>(AdmissionFormBindingRegistry.Metadata.Values.ToList());

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionForms.View)]
	public Task<IReadOnlyList<AdmissionFormDataSourceMetadataDto>> GetDataSourcesAsync() => Task.FromResult(AdmissionFormDataSourceRegistry.Metadata);

	public async Task<IReadOnlyList<AdmissionFormOptionDto>> GetOptionsAsync(string sourceKey, Guid? parentId = null, Guid? academicYearId = null,
		string? searchTerm = null, int skipCount = 0, int maxResultCount = 20, List<Guid>? selectedIds = null)
	{
		await LazyServiceProvider.LazyGetRequiredService<AdmissionFormOptionsAccessService>().EnsureReadAsync(sourceKey);
		return await dataSourceRegistry.GetOptionsAsync(sourceKey, parentId, academicYearId, searchTerm, skipCount, maxResultCount, selectedIds);
	}

	private static List<string> Compare(AdmissionFormDto from, AdmissionFormDto to)
	{
		var changes = new List<string>();
		if (!string.Equals(from.Name, to.Name, StringComparison.Ordinal)) changes.Add($"Form name: '{from.Name}' to '{to.Name}'");
		if (!string.Equals(from.Description, to.Description, StringComparison.Ordinal)) changes.Add("Form description changed");
		if (from.Purpose != to.Purpose) changes.Add($"Purpose changed from {from.Purpose} to {to.Purpose}");
		if (from.LayoutType != to.LayoutType) changes.Add($"Layout changed from {from.LayoutType} to {to.LayoutType}");
		if (from.FieldsPerRow != to.FieldsPerRow) changes.Add($"Desktop columns changed from {from.FieldsPerRow} to {to.FieldsPerRow}");
		if (!from.SupportedLanguages.SequenceEqual(to.SupportedLanguages, StringComparer.OrdinalIgnoreCase)) changes.Add("Supported languages changed");
		var fromSections = from.Sections.ToDictionary(section => section.Code, StringComparer.OrdinalIgnoreCase);
		var toSections = to.Sections.ToDictionary(section => section.Code, StringComparer.OrdinalIgnoreCase);
		foreach (var code in toSections.Keys.Except(fromSections.Keys, StringComparer.OrdinalIgnoreCase)) changes.Add($"Section added: {code}");
		foreach (var code in fromSections.Keys.Except(toSections.Keys, StringComparer.OrdinalIgnoreCase)) changes.Add($"Section removed: {code}");
		foreach (var code in fromSections.Keys.Intersect(toSections.Keys, StringComparer.OrdinalIgnoreCase))
		{
			var before = fromSections[code];
			var after = toSections[code];
			if (!string.Equals(before.Name, after.Name, StringComparison.Ordinal)) changes.Add($"Section renamed: {code}");
			if (before.IsRepeatable != after.IsRepeatable || before.MinEntries != after.MinEntries || before.MaxEntries != after.MaxEntries) changes.Add($"Repeat settings changed: {code}");
			var beforeFields = before.Fields.ToDictionary(field => field.Code, StringComparer.OrdinalIgnoreCase);
			var afterFields = after.Fields.ToDictionary(field => field.Code, StringComparer.OrdinalIgnoreCase);
			foreach (var fieldCode in afterFields.Keys.Except(beforeFields.Keys, StringComparer.OrdinalIgnoreCase)) changes.Add($"Field added: {code}.{fieldCode}");
			foreach (var fieldCode in beforeFields.Keys.Except(afterFields.Keys, StringComparer.OrdinalIgnoreCase)) changes.Add($"Field removed: {code}.{fieldCode}");
			foreach (var fieldCode in beforeFields.Keys.Intersect(afterFields.Keys, StringComparer.OrdinalIgnoreCase))
			{
				var beforeField = beforeFields[fieldCode];
				var afterField = afterFields[fieldCode];
				if (beforeField.Label != afterField.Label || beforeField.FieldType != afterField.FieldType || beforeField.Required != afterField.Required || beforeField.OptionsJson != afterField.OptionsJson)
					changes.Add($"Field changed: {code}.{fieldCode}");
			}
		}
		if (changes.Count == 0) changes.Add("No structural differences");
		return changes;
	}

	private async Task<AdmissionFormValidationResultDto> ValidateDefinitionAsync(Guid id)
	{
		var form = await GetFormAsync(id);
		var sections = await GetSectionsAsync(id);
		var fields = await GetFieldsAsync(id);
		var result = new AdmissionFormValidationResultDto();
		var experience = AdmissionFormMetadataStore.ReadExperience(form);
		if (experience.SupportedLanguages.Count == 0 || !experience.SupportedLanguages.Contains(experience.DefaultLanguage, StringComparer.OrdinalIgnoreCase)) result.Errors.Add("Admissions.FormInvalidLanguages");
		if (experience.SupportedLanguages.Count > 10 || experience.SupportedLanguages.Any(language => !Regex.IsMatch(language, "^[a-z]{2,3}(-[a-z0-9]{2,8})?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))) result.Errors.Add("Admissions.FormInvalidLanguages");
		if (sections.Count == 0) result.Errors.Add(AdmissionFormErrorCodes.NoSections);
		if (sections.GroupBy(section => section.Code, StringComparer.OrdinalIgnoreCase).Any(group => group.Count() > 1)) result.Errors.Add("Admissions.FormDuplicateSectionCode");
		if (sections.Any(section => !Regex.IsMatch(section.Code, "^[A-Z][A-Z0-9_-]*$", RegexOptions.CultureInvariant))) result.Errors.Add("Admissions.FormInvalidSectionCode");
		if (form.LayoutType == AdmissionFormLayoutType.Stepper)
		{
			var steps = sections.GroupBy(section => section.StepId ?? section.Id).Select(group => group.First()).ToList();
			if (steps.Count < 2) result.Errors.Add("Admissions.FormStepperRequiresTwoSteps");
			var stepCodes = steps.Select(step => step.StepCode ?? step.Code).ToList();
			if (stepCodes.Any(code => !Regex.IsMatch(code, "^[A-Z][A-Z0-9_-]*$", RegexOptions.CultureInvariant))) result.Errors.Add("Admissions.FormInvalidStepCode");
			if (stepCodes.GroupBy(code => code, StringComparer.OrdinalIgnoreCase).Any(group => group.Count() > 1)) result.Errors.Add("Admissions.FormDuplicateStepCode");
		}
		if (fields.Count == 0) result.Errors.Add("Admissions.FormNoFields");
		if (fields.GroupBy(field => field.Code, StringComparer.OrdinalIgnoreCase).Any(group => group.Count() > 1)) result.Errors.Add(AdmissionFormErrorCodes.DuplicateFieldCode);
		if (fields.Any(field => !Regex.IsMatch(field.Code, "^[A-Z][A-Z0-9_-]*$", RegexOptions.CultureInvariant))) result.Errors.Add("Admissions.FormInvalidFieldCode");
		var fieldCodes = fields.Select(field => field.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
		var boundKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (var section in sections)
		{
			ValidateRule(section.VisibilityRuleJson, fieldCodes, $"Section:{section.Code}", result);
			if (experience.Sections.TryGetValue(section.Code, out var sectionExperience) && sectionExperience.IsRepeatable)
			{
				if (sectionExperience.MinEntries < 0 || sectionExperience.MaxEntries < Math.Max(1, sectionExperience.MinEntries) || sectionExperience.MaxEntries > 25)
					result.Errors.Add($"Admissions.FormInvalidRepeatRange:{section.Code}");
				if (fields.Any(field => field.SectionId == section.Id && field.BindingType == AdmissionFormBindingType.System))
					result.Errors.Add($"Admissions.FormRepeatableSystemBinding:{section.Code}");
			}
		}
		foreach (var step in sections.GroupBy(section => section.StepId ?? section.Id).Select(group => group.First()))
			ValidateRule(step.StepVisibilityRuleJson, fieldCodes, $"Step:{step.StepCode ?? step.Code}", result);
		foreach (var field in fields)
		{
			var fieldMetadata = FieldTypes.FirstOrDefault(metadata => metadata.FieldType == field.FieldType);
			if (fieldMetadata is null) result.Errors.Add($"{AdmissionFormErrorCodes.InvalidFieldType}:{field.Code}");
			else if (!fieldMetadata.IsRuntimeReady) result.Errors.Add($"UnsupportedRuntimeField:{field.Code}");
			if (field.FieldType == AdmissionFormFieldType.Static) continue;
			if (field.BindingType == AdmissionFormBindingType.System)
			{
				if (string.IsNullOrWhiteSpace(field.BindingKey) || !AdmissionFormBindingRegistry.Metadata.TryGetValue(field.BindingKey, out var metadata)) result.Errors.Add($"{AdmissionFormErrorCodes.InvalidSystemBinding}:{field.Code}");
				else if (metadata.FieldType != field.FieldType) result.Errors.Add($"{AdmissionFormErrorCodes.InvalidBinding}:{field.Code}");
				else if (!boundKeys.Add(field.BindingKey)) result.Errors.Add($"{AdmissionFormErrorCodes.DuplicateSystemBinding}:{field.Code}");
			}
			else if (field.BindingType != AdmissionFormBindingType.Custom || string.IsNullOrWhiteSpace(field.BindingKey)) result.Errors.Add($"{AdmissionFormErrorCodes.InvalidBinding}:{field.Code}");
			if (field.FieldType is AdmissionFormFieldType.Select or AdmissionFormFieldType.MultiSelect or AdmissionFormFieldType.Radio) ValidateOptions(field, fieldCodes, result);
			ValidateRule(field.VisibilityRuleJson, fieldCodes, field.Code, result);
			ValidateRule(field.RequiredRuleJson, fieldCodes, $"Required:{field.Code}", result);
			ValidateValidationJson(field.ValidationJson, field.Code, fieldCodes, result);
		}
		ValidateDependencyCycles(fields, result);
		return result;
	}

	internal static void ValidateOptions(AdmissionFormFieldDefinition field, HashSet<string> knownFields, AdmissionFormValidationResultDto result)
	{
		if (string.IsNullOrWhiteSpace(field.OptionsJson)) { result.Errors.Add($"{AdmissionFormErrorCodes.InvalidOptions}:{field.Code}"); return; }
		try
		{
			using var document = JsonDocument.Parse(field.OptionsJson);
			var root = document.RootElement;
			if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("source", out var source) && source.ValueKind == JsonValueKind.String)
			{
				if (!AdmissionFormDataSourceRegistry.IsAllowed(source.GetString())) result.Errors.Add($"{AdmissionFormErrorCodes.InvalidOptions}:{field.Code}");
				if (root.TryGetProperty("parentField", out var parentField) && parentField.ValueKind == JsonValueKind.String && !knownFields.Contains(parentField.GetString() ?? string.Empty)) result.Errors.Add($"{AdmissionFormErrorCodes.InvalidRuleReference}:{field.Code}");
				return;
			}
			if (root.ValueKind != JsonValueKind.Array || root.GetArrayLength() == 0)
			{
				result.Errors.Add($"{AdmissionFormErrorCodes.InvalidOptions}:{field.Code}");
				return;
			}
			var values = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			foreach (var option in root.EnumerateArray())
			{
				var value = option.ValueKind == JsonValueKind.String
					? option.GetString()
					: option.ValueKind == JsonValueKind.Object && option.TryGetProperty("value", out var valueProperty) && valueProperty.ValueKind == JsonValueKind.String
						? valueProperty.GetString()
						: null;
				if (string.IsNullOrWhiteSpace(value) || !values.Add(value)) result.Errors.Add($"{AdmissionFormErrorCodes.InvalidOptions}:{field.Code}");
				if (option.ValueKind == JsonValueKind.Object && option.TryGetProperty("label", out var labelProperty) &&
					(labelProperty.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(labelProperty.GetString())))
					result.Errors.Add($"{AdmissionFormErrorCodes.InvalidOptions}:{field.Code}");
			}
		}
		catch (JsonException) { result.Errors.Add($"{AdmissionFormErrorCodes.InvalidOptions}:{field.Code}"); }
	}

	private static void ValidateRule(string? rule, HashSet<string> knownFields, string owner, AdmissionFormValidationResultDto result)
	{
		if (string.IsNullOrWhiteSpace(rule)) return;
		try { using var document = JsonDocument.Parse(rule); ValidateRuleNode(document.RootElement, knownFields, owner, result); }
		catch (Exception exception) when (exception is JsonException or InvalidOperationException) { result.Errors.Add($"InvalidRuleJson:{owner}"); }
	}

	private static void ValidateRuleNode(JsonElement node, HashSet<string> knownFields, string owner, AdmissionFormValidationResultDto result)
	{
		if (node.ValueKind != JsonValueKind.Object)
		{
			result.Errors.Add($"InvalidRuleJson:{owner}");
			return;
		}
		if (node.TryGetProperty("conditions", out var conditions) && conditions.ValueKind == JsonValueKind.Array)
		{
			if (!node.TryGetProperty("operator", out var groupOperator) || !new[] { "ALL", "ANY" }.Contains(groupOperator.GetString(), StringComparer.OrdinalIgnoreCase) || conditions.GetArrayLength() == 0) result.Errors.Add($"InvalidRuleOperator:{owner}");
			foreach (var condition in conditions.EnumerateArray()) ValidateRuleNode(condition, knownFields, owner, result);
			return;
		}
		var field = node.TryGetProperty("field", out var fieldProperty) ? fieldProperty.GetString() : null;
		var operatorName = node.TryGetProperty("operator", out var operatorProperty) ? operatorProperty.GetString() : null;
		if (string.IsNullOrWhiteSpace(field) || !knownFields.Contains(field)) result.Errors.Add($"{AdmissionFormErrorCodes.InvalidRuleReference}:{owner}");
		if (string.IsNullOrWhiteSpace(operatorName) || !RuleOperators.Contains(operatorName, StringComparer.OrdinalIgnoreCase)) result.Errors.Add($"InvalidRuleOperator:{owner}");
		if (!string.Equals(operatorName, "IsEmpty", StringComparison.OrdinalIgnoreCase) && !string.Equals(operatorName, "IsNotEmpty", StringComparison.OrdinalIgnoreCase) && !node.TryGetProperty("value", out _)) result.Errors.Add($"InvalidRuleValue:{owner}");
	}

	private static void ValidateJsonObject(string? json, string owner, AdmissionFormValidationResultDto result)
	{
		if (string.IsNullOrWhiteSpace(json)) return;
		try { using var document = JsonDocument.Parse(json); if (document.RootElement.ValueKind != JsonValueKind.Object) result.Errors.Add($"InvalidJson:{owner}"); }
		catch (JsonException) { result.Errors.Add($"InvalidJson:{owner}"); }
	}

	internal static void ValidateValidationJson(string? json, string code, HashSet<string> knownFields, AdmissionFormValidationResultDto result)
	{
		if (string.IsNullOrWhiteSpace(json)) return;
		try
		{
			using var document = JsonDocument.Parse(json);
			var root = document.RootElement;
			if (root.ValueKind != JsonValueKind.Object) { result.Errors.Add($"{AdmissionFormErrorCodes.InvalidValidationRule}:{code}"); return; }
			if (root.TryGetProperty("min", out var min) && root.TryGetProperty("max", out var max) && min.TryGetDecimal(out var minValue) && max.TryGetDecimal(out var maxValue) && minValue > maxValue)
				result.Errors.Add($"{AdmissionFormErrorCodes.InvalidValidationRule}:{code}");
			if (root.TryGetProperty("minLength", out var minLength) && root.TryGetProperty("maxLength", out var maxLength) && minLength.TryGetInt32(out var minLengthValue) && maxLength.TryGetInt32(out var maxLengthValue) && minLengthValue > maxLengthValue)
				result.Errors.Add($"{AdmissionFormErrorCodes.InvalidValidationRule}:{code}");
			if (root.TryGetProperty("regex", out var regex) && regex.ValueKind == JsonValueKind.String)
				try { _ = new Regex(regex.GetString() ?? string.Empty); } catch (ArgumentException) { result.Errors.Add($"{AdmissionFormErrorCodes.InvalidValidationRule}:{code}"); }
			if (root.TryGetProperty("minDate", out var minDate) && root.TryGetProperty("maxDate", out var maxDate) && DateTimeOffset.TryParse(minDate.GetString(), out var minDateValue) && DateTimeOffset.TryParse(maxDate.GetString(), out var maxDateValue) && minDateValue.Date > maxDateValue.Date)
				result.Errors.Add($"{AdmissionFormErrorCodes.InvalidValidationRule}:{code}");
			ValidateFieldFormat(root, code, knownFields, result);
		}
		catch (JsonException) { result.Errors.Add($"{AdmissionFormErrorCodes.InvalidValidationRule}:{code}"); }
	}

	private static void ValidateFieldFormat(JsonElement root, string code, HashSet<string> knownFields, AdmissionFormValidationResultDto result)
	{
		if (!root.TryGetProperty("format", out var formatProperty)) return;
		if (formatProperty.ValueKind != JsonValueKind.String) { result.Errors.Add($"{AdmissionFormErrorCodes.InvalidValidationRule}:{code}"); return; }
		var format = formatProperty.GetString();
		if (!new[] { "email", "phone", "aadhaar", "pan", "pincode", "country", "state", "city", "photo" }.Contains(format, StringComparer.OrdinalIgnoreCase))
		{
			result.Errors.Add($"{AdmissionFormErrorCodes.InvalidValidationRule}:{code}");
			return;
		}
		if (string.Equals(format, "state", StringComparison.OrdinalIgnoreCase) || string.Equals(format, "city", StringComparison.OrdinalIgnoreCase))
			ValidateFieldReference(root, "countryField", code, knownFields, result);
		if (string.Equals(format, "city", StringComparison.OrdinalIgnoreCase))
			ValidateFieldReference(root, "stateField", code, knownFields, result);
	}

	private static void ValidateFieldReference(JsonElement root, string propertyName, string code, HashSet<string> knownFields, AdmissionFormValidationResultDto result)
	{
		if (!root.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String ||
			string.IsNullOrWhiteSpace(property.GetString()) || !knownFields.Contains(property.GetString()!))
			result.Errors.Add($"{AdmissionFormErrorCodes.InvalidRuleReference}:{code}");
	}

	private static void ValidateDependencyCycles(IEnumerable<AdmissionFormFieldDefinition> fields, AdmissionFormValidationResultDto result)
	{
		var graph = fields.ToDictionary(field => field.Code, _ => new HashSet<string>(StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase);
		foreach (var field in fields)
		{
			CollectDependencies(field.VisibilityRuleJson, field.Code, graph);
			CollectDependencies(field.RequiredRuleJson, field.Code, graph);
		}
		var visiting = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (var code in graph.Keys)
			if (HasCycle(code, graph, visiting, visited)) { result.Errors.Add(AdmissionFormErrorCodes.DependencyCycle); return; }
	}

	private static void CollectDependencies(string? rule, string owner, Dictionary<string, HashSet<string>> graph)
	{
		if (string.IsNullOrWhiteSpace(rule)) return;
		try
		{
			using var document = JsonDocument.Parse(rule);
			CollectDependencyNode(document.RootElement, owner, graph);
		}
		catch (JsonException) { }
	}

	private static void CollectDependencyNode(JsonElement node, string owner, Dictionary<string, HashSet<string>> graph)
	{
		if (node.TryGetProperty("field", out var field) && field.ValueKind == JsonValueKind.String && graph.ContainsKey(field.GetString() ?? string.Empty)) graph[owner].Add(field.GetString()!);
		if (node.TryGetProperty("conditions", out var conditions) && conditions.ValueKind == JsonValueKind.Array)
			foreach (var condition in conditions.EnumerateArray()) CollectDependencyNode(condition, owner, graph);
	}

	private static bool HasCycle(string code, Dictionary<string, HashSet<string>> graph, HashSet<string> visiting, HashSet<string> visited)
	{
		if (visiting.Contains(code)) return true;
		if (!visited.Add(code)) return false;
		visiting.Add(code);
		foreach (var dependency in graph[code]) if (HasCycle(dependency, graph, visiting, visited)) return true;
		visiting.Remove(code);
		return false;
	}

	private async Task ReplaceChildrenAsync(Guid formId, CreateUpdateAdmissionFormDto input)
	{
		foreach (var field in await GetFieldsAsync(formId)) await fieldRepository.DeleteAsync(field, autoSave: false);
		foreach (var section in await GetSectionsAsync(formId)) await sectionRepository.DeleteAsync(section, autoSave: false);
		foreach (var sectionInput in input.Sections)
		{
			var sectionId = GuidGenerator.Create();
			await sectionRepository.InsertAsync(new AdmissionFormSectionDefinition(sectionId, formId, sectionInput.Code, sectionInput.Name, sectionInput.DisplayOrder, sectionInput.VisibilityRuleJson,
				sectionInput.StepId, sectionInput.StepCode, sectionInput.StepName, sectionInput.StepDisplayOrder, sectionInput.StepVisibilityRuleJson), autoSave: false);
			foreach (var field in sectionInput.Fields) await fieldRepository.InsertAsync(CreateField(formId, sectionId, field), autoSave: false);
		}
	}

	private async Task CopyChildrenAsync(Guid sourceId, Guid targetId)
	{
		var sourceSections = await GetSectionsAsync(sourceId);
		var sourceFields = await GetFieldsAsync(sourceId);
		var sectionMap = new Dictionary<Guid, Guid>();
		foreach (var sourceSection in sourceSections)
		{
			var sectionId = GuidGenerator.Create(); sectionMap[sourceSection.Id] = sectionId;
			await sectionRepository.InsertAsync(new AdmissionFormSectionDefinition(sectionId, targetId, sourceSection.Code, sourceSection.Name, sourceSection.DisplayOrder, sourceSection.VisibilityRuleJson,
				sourceSection.StepId, sourceSection.StepCode, sourceSection.StepName, sourceSection.StepDisplayOrder, sourceSection.StepVisibilityRuleJson), autoSave: false);
		}
		foreach (var sourceField in sourceFields)
			await fieldRepository.InsertAsync(new AdmissionFormFieldDefinition(GuidGenerator.Create(), targetId, sectionMap[sourceField.SectionId], sourceField.Code, sourceField.Label, sourceField.FieldType, sourceField.BindingType, sourceField.BindingKey, sourceField.Required, sourceField.ReadOnly, sourceField.DisplayOrder, sourceField.Placeholder, sourceField.HelpText, sourceField.DefaultValue, sourceField.ValidationJson, sourceField.VisibilityRuleJson, sourceField.OptionsJson, sourceField.RequiredRuleJson), autoSave: false);
	}

	private AdmissionFormFieldDefinition CreateField(Guid formId, Guid sectionId, CreateUpdateAdmissionFormFieldDto input) => new(GuidGenerator.Create(), formId, sectionId, input.Code, input.Label, input.FieldType, input.BindingType, input.BindingKey, input.Required, input.ReadOnly, input.DisplayOrder, input.Placeholder, input.HelpText, input.DefaultValue, input.ValidationJson, input.VisibilityRuleJson, input.OptionsJson, input.RequiredRuleJson);
	private async Task EnsureCodeAvailableAsync(string code, Guid? excludingId) { var normalized = code.Trim().ToUpperInvariant(); var query = await formRepository.GetQueryableAsync(); if (await AsyncExecuter.AnyAsync(query.Where(form => form.Code == normalized && (!excludingId.HasValue || form.Id != excludingId.Value)))) throw new BusinessException(AdmissionFormErrorCodes.CodeAlreadyExists); }
	private async Task<AdmissionFormDefinition> GetFormAsync(Guid id) => await formRepository.FindAsync(id) ?? throw new EntityNotFoundException(typeof(AdmissionFormDefinition), id);
	private async Task<List<AdmissionFormSectionDefinition>> GetSectionsAsync(Guid id) => await AsyncExecuter.ToListAsync((await sectionRepository.GetQueryableAsync()).Where(section => section.FormDefinitionId == id).OrderBy(section => section.DisplayOrder));
	private async Task<List<AdmissionFormFieldDefinition>> GetFieldsAsync(Guid id) => await AsyncExecuter.ToListAsync((await fieldRepository.GetQueryableAsync()).Where(field => field.FormDefinitionId == id).OrderBy(field => field.DisplayOrder));
	private async Task<AdmissionFormDto> MapAsync(AdmissionFormDefinition form)
	{
		var fields = await GetFieldsAsync(form.Id);
		return MapWithRequiredRules(form, await GetSectionsAsync(form.Id), fields);
	}

	private static AdmissionFormDto MapWithRequiredRules(AdmissionFormDefinition form, List<AdmissionFormSectionDefinition> sections, List<AdmissionFormFieldDefinition> fields)
	{
		var dto = Map(form, sections, fields);
		var rules = fields.ToDictionary(field => field.Code, field => field.RequiredRuleJson, StringComparer.OrdinalIgnoreCase);
		foreach (var field in dto.Sections.SelectMany(section => section.Fields))
			if (rules.TryGetValue(field.Code, out var rule)) field.RequiredRuleJson = rule;
		return dto;
	}
	private static AdmissionFormDto Map(AdmissionFormDefinition form, List<AdmissionFormSectionDefinition> sections, List<AdmissionFormFieldDefinition> fields)
	{
		var experience = AdmissionFormMetadataStore.ReadExperience(form);
		return new AdmissionFormDto
		{
			Id = form.Id, Name = form.Name, Code = form.Code, Description = form.Description,
			Version = form.Version, Status = form.Status, LayoutType = form.LayoutType, Purpose = form.Purpose,
			FieldsPerRow = form.FieldsPerRow, DefaultLanguage = experience.DefaultLanguage,
			SupportedLanguages = experience.SupportedLanguages,
			ParentVersionId = form.ParentVersionId, PublishedAt = form.PublishedAt, PublishedBy = form.PublishedBy,
			ConcurrencyStamp = form.ConcurrencyStamp, CreationTime = form.CreationTime, CreatorId = form.CreatorId,
			LastModificationTime = form.LastModificationTime, LastModifierId = form.LastModifierId,
			SectionCount = sections.Count,
			Sections = sections.Select(section =>
			{
				experience.Sections.TryGetValue(section.Code, out var sectionExperience);
				return new AdmissionFormSectionDto
				{
					Id = section.Id, FormDefinitionId = section.FormDefinitionId, Code = section.Code,
					Name = section.Name, DisplayOrder = section.DisplayOrder, VisibilityRuleJson = section.VisibilityRuleJson,
					StepId = section.StepId, StepCode = section.StepCode, StepName = section.StepName,
					StepDisplayOrder = section.StepDisplayOrder, StepVisibilityRuleJson = section.StepVisibilityRuleJson,
					IsRepeatable = sectionExperience?.IsRepeatable ?? false,
					MinEntries = sectionExperience?.MinEntries ?? 1,
					MaxEntries = sectionExperience?.MaxEntries ?? 1,
					EntryLabel = sectionExperience?.EntryLabel,
					Translations = sectionExperience?.Translations ?? new(StringComparer.OrdinalIgnoreCase),
					Fields = fields.Where(field => field.SectionId == section.Id).Select(field => new AdmissionFormFieldDto
					{
						Id = field.Id, FormDefinitionId = field.FormDefinitionId, SectionId = field.SectionId,
						Code = field.Code, Label = field.Label, FieldType = field.FieldType, BindingType = field.BindingType,
						BindingKey = field.BindingKey, Placeholder = field.Placeholder, HelpText = field.HelpText,
						DefaultValue = field.DefaultValue, OptionsJson = field.OptionsJson, Required = field.Required,
						ReadOnly = field.ReadOnly, DisplayOrder = field.DisplayOrder, ValidationJson = field.ValidationJson,
						VisibilityRuleJson = field.VisibilityRuleJson, RequiredRuleJson = field.RequiredRuleJson,
						Translations = sectionExperience?.Fields.GetValueOrDefault(field.Code) ?? new(StringComparer.OrdinalIgnoreCase)
					}).ToList()
				};
			}).ToList()
		};
	}
	private static void EnsureConcurrency(AdmissionFormDefinition form, string? stamp) { if (string.IsNullOrWhiteSpace(stamp) || !string.Equals(form.ConcurrencyStamp, stamp, StringComparison.Ordinal)) throw new BusinessException("Admissions.FormConcurrencyConflict", "The form changed after it was opened. Reload and try again."); }
}
