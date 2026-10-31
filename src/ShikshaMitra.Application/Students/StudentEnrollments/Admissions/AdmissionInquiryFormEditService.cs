using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

[RemoteService(false)]
public class AdmissionInquiryFormEditService(
	IRepository<AdmissionFormDefinition, Guid> forms,
	IRepository<AdmissionFormFieldDefinition, Guid> fields,
	IRepository<AdmissionFormSectionDefinition, Guid> sections,
	IRepository<AdmissionApplicant, Guid> applicants,
	IRepository<AdmissionFamily, Guid> families,
	IRepository<AdmissionGuardian, Guid> guardians,
	AdmissionApplicationFormValidator validator,
	AdmissionFormDataSourceRegistry dataSources) : ApplicationService, ITransientDependency
{
	public async Task ApplyAsync(AdmissionInquiry inquiry, UpdateAdmissionInquiryDto input, AdmissionOffering offering)
	{
		var changedOffering = inquiry.AdmissionOfferingId != offering.Id;
		var formId = changedOffering ? offering.InquiryFormDefinitionId : inquiry.FormResponseDefinitionId ?? offering.InquiryFormDefinitionId;
		var applicant = inquiry.ApplicantId.HasValue ? await applicants.FindAsync(inquiry.ApplicantId.Value) : null;
		var family = inquiry.FamilyId.HasValue ? await families.FindAsync(inquiry.FamilyId.Value) : null;
		var guardian = family?.PrimaryGuardianId.HasValue == true ? await guardians.FindAsync(family.PrimaryGuardianId.Value) : null;
		var confirmedSibling = applicant != null && AdmissionSiblingParentSelection.ReadConfirmation(applicant) != null;
		var definitions = formId.HasValue ? await fields.GetListAsync(field => field.FormDefinitionId == formId.Value) : [];
		if (applicant != null && AdmissionFirstParentContactMapping.CanMap(applicant) && inquiry.FormResponseDefinitionId.HasValue)
		{
			var savedDefinitions = inquiry.FormResponseDefinitionId == formId ? definitions :
				await fields.GetListAsync(field => field.FormDefinitionId == inquiry.FormResponseDefinitionId.Value);
			AdmissionFirstParentContactMapping.Apply(applicant,
				AdmissionFirstParentContactMapping.ReadInquiryAnswers(inquiry, savedDefinitions), recoverSavedAnswers: true);
		}
		if (input.FormValues != null)
		{
			if (!formId.HasValue || (await forms.FindAsync(formId.Value))?.Status is not (AdmissionFormStatus.Published or AdmissionFormStatus.Archived))
				throw new BusinessException(AdmissionApplicationErrorCodes.ConfigurationNotPublished);
			if (input.FormValues.GroupBy(value => value.FieldCode, StringComparer.OrdinalIgnoreCase).Any(group => group.Count() > 1))
				throw new BusinessException(AdmissionInquiryManagementErrorCodes.InvalidInput, "Submit each inquiry field once.");
			var codes = definitions.Select(field => field.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
			if (input.FormValues.Any(value => !codes.Contains(value.FieldCode)))
				throw new BusinessException(AdmissionApplicationErrorCodes.UnknownFormField);
			foreach (var answer in input.FormValues)
			{
				var field = definitions.First(field => string.Equals(field.Code, answer.FieldCode, StringComparison.OrdinalIgnoreCase));
				answer.ValueJson = AdmissionFormEmailValueNormalizer.Normalize(field, answer.ValueJson);
			}
			var values = input.FormValues.ToDictionary(value => value.FieldCode,
				value => AdmissionApplicationValueConverter.Unwrap(value.ValueJson), StringComparer.OrdinalIgnoreCase);
			var original = string.IsNullOrWhiteSpace(inquiry.FormResponseSnapshotJson) ? new Dictionary<string, string>() :
				JsonSerializer.Deserialize<Dictionary<string, string>>(inquiry.FormResponseSnapshotJson) ?? [];
			var formSections = await sections.GetListAsync(section => section.FormDefinitionId == formId.Value);
			var visibleFieldCodes = validator.GetVisibleInquiryFieldCodes(definitions, formSections, values);
			if (!changedOffering && applicant != null)
				foreach (var field in definitions.Where(field => field.BindingType == AdmissionFormBindingType.System && !string.IsNullOrWhiteSpace(field.BindingKey)))
					if (!original.ContainsKey(field.BindingKey!))
						original[field.BindingKey!] = JsonSerializer.Serialize(AdmissionFormBindingRegistry.GetValue(field.BindingKey!, applicant, guardian));
			if (!changedOffering && definitions.Where(field => field.ReadOnly).Any(field =>
				original.TryGetValue(string.IsNullOrWhiteSpace(field.BindingKey) ? field.Code : field.BindingKey!, out var raw) &&
				(values.TryGetValue(field.Code, out var edited) ? AdmissionApplicationValueConverter.Unwrap(raw) != edited : visibleFieldCodes.Contains(field.Code))))
				throw new BusinessException(AdmissionInquiryManagementErrorCodes.InvalidInput, "Read-only inquiry fields cannot be changed.");
			var validation = validator.ValidateInquiry(definitions, formSections, values);
			var cycle = await LazyServiceProvider.LazyGetRequiredService<IRepository<AdmissionCycle, Guid>>().GetAsync(offering.AdmissionCycleId);
			await LazyServiceProvider.LazyGetRequiredService<AdmissionInquiryAcademicOptionsService>()
				.ValidateAsync(definitions.Where(field => visibleFieldCodes.Contains(field.Code)).ToList(), values, offering, cycle.AcademicYearId, false);
			if (validation.Errors.Count > 0)
				throw new BusinessException(AdmissionApplicationErrorCodes.ValidationFailed).WithData("Fields", string.Join(", ", validation.FieldErrors.Keys));
			var mappingValues = new Dictionary<string, string>(values, StringComparer.OrdinalIgnoreCase);
			foreach (var hidden in definitions.Where(field => !visibleFieldCodes.Contains(field.Code)))
			{
				mappingValues.Remove(hidden.Code);
				values.Remove(hidden.Code);
			}
			if (!changedOffering)
				foreach (var hidden in definitions.Where(field => field.ReadOnly && !visibleFieldCodes.Contains(field.Code) && !values.ContainsKey(field.Code)))
				{
					var key = string.IsNullOrWhiteSpace(hidden.BindingKey) ? hidden.Code : hidden.BindingKey!;
					if (original.TryGetValue(key, out var raw)) mappingValues[hidden.Code] = AdmissionApplicationValueConverter.Unwrap(raw);
				}
			var contact = AdmissionInquiryConfiguredFields.ResolveContact(definitions, mappingValues,
				applicant == null ? null : AdmissionPrimaryContactSelection.ReadRelationship(applicant), allowLegacySplitContact: !changedOffering, activeFieldCodes: visibleFieldCodes);
			if (confirmedSibling && contact.Relationship != null && contact.Relationship != AdmissionSiblingParentSelection.ReadRelationship(applicant!))
				throw new BusinessException(AdmissionInquiryManagementErrorCodes.InvalidInput, "The confirmed sibling parent cannot be replaced through an inquiry form.");
			foreach (var definition in definitions.Where(field => field.BindingType == AdmissionFormBindingType.System && !string.IsNullOrWhiteSpace(field.BindingKey)))
			{
				if (applicant == null || !values.TryGetValue(definition.Code, out var value)) continue;
				if (definition.BindingKey == "Family.SiblingStudentIds")
				{
					var siblings = AdmissionStudentProfileMapping.ReadSiblings(value) ?? [];
					if (siblings.Contains(applicant.ExistingStudentId ?? applicant.ReservedStudentId) || !await dataSources.AreSiblingIdsAllowedAsync(siblings))
						throw new BusinessException("Admissions:InvalidSiblingSelection");
				}
				if ((applicant.ExistingStudentId.HasValue || confirmedSibling) && definition.BindingKey!.StartsWith("Guardian.", StringComparison.OrdinalIgnoreCase)) continue;
				if (confirmedSibling && definition.BindingKey!.StartsWith(AdmissionSiblingParentSelection.ReadRelationship(applicant) == "father" ? "Father." : "Mother.", StringComparison.OrdinalIgnoreCase)) continue;
				if (definition.BindingKey is "Applicant.Name" or "Applicant.FirstName" or "Applicant.MiddleName" or "Applicant.LastName") continue;
				if (string.IsNullOrWhiteSpace(value) && definition.BindingKey is "Applicant.FirstName" or "Guardian.FirstName" or "Guardian.Mobile")
					throw new BusinessException(AdmissionErrorCodes.InquiryIdentityIncomplete);
				if (string.IsNullOrWhiteSpace(value) && definition.BindingKey == "Applicant.DateOfBirth")
					applicant.UpdateProfile(applicant.FirstName, applicant.MiddleName, applicant.LastName, null, applicant.Gender,
						applicant.AddressLine1, applicant.AddressLine2, applicant.City, applicant.State, applicant.Country, applicant.PostalCode);
				else AdmissionFormBindingRegistry.ApplyValue(definition.BindingKey!, value, applicant, guardian);
			}
			if (applicant != null) AdmissionInquiryConfiguredFields.ApplyApplicantName(applicant, definitions, mappingValues);
			if (applicant != null && guardian != null && !applicant.ExistingStudentId.HasValue && !confirmedSibling && contact.IsConfigured)
			{
				var previousRole = AdmissionPrimaryContactSelection.ReadRelationship(applicant);
				var canReuseSavedContact = !changedOffering &&
					(AdmissionInquiryConfiguredFields.Find(definitions, "Inquiry.ParentRelationship") == null || contact.Relationship != null) &&
					(contact.Relationship == null || contact.Relationship == previousRole);
				var name = !contact.NameIsConfigured && canReuseSavedContact ? guardian.FirstName : contact.Name;
				var enteredMobile = !contact.MobileIsConfigured && canReuseSavedContact ? guardian.Mobile : contact.Mobile;
				if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(enteredMobile))
					throw new BusinessException(AdmissionErrorCodes.InquiryIdentityIncomplete, "Complete the configured primary parent name and mobile.");
				var mobile = AdmissionInquiryPhoneValidator.NormalizeAndValidateIndianMobile(enteredMobile);
				var roleChanged = previousRole != null && contact.Relationship != null && previousRole != contact.Relationship;
				var savedEmail = roleChanged ? AdmissionStudentProfileMapping.Read(applicant, contact.Relationship == "father" ? "Father.Email" : contact.Relationship == "mother" ? "Mother.Email" : "Guardian.Email") : guardian.Email;
				var email = contact.EmailIsConfigured ? contact.Email : savedEmail;
				var lastName = contact.LastNameIsConfigured || contact.WholeNameIsConfigured || roleChanged ? contact.LastName : guardian.LastName;
				guardian.UpdateContact(name, lastName, mobile, email);
				input.Phone = mobile;
				input.Email = contact.EmailIsConfigured || roleChanged ? email : inquiry.Email;
				if (contact.Relationship != null)
				{
					var fullName = string.Join(" ", new[] { name, lastName }.Where(value => !string.IsNullOrWhiteSpace(value)));
					if (AdmissionPrimaryContactSelection.HasAdditionalMainGuardian(applicant) && AdmissionInquiryConfiguredFields.Find(definitions, "Inquiry.ParentRelationship") == null &&
						AdmissionSiblingParentSelection.IsRelationship(contact.Relationship))
						AdmissionSiblingParentSelection.CaptureParent(applicant, contact.Relationship, fullName, mobile, email);
					else AdmissionPrimaryContactSelection.Capture(applicant, contact.Relationship, fullName, mobile, email,
						input.GuardianResponsibilityConfirmed, CurrentUser.Id, Clock.Now, false);
				}
			}
			var operational = new CreateAdmissionInquiryDto { Source = input.Source, SourceDetail = input.SourceDetail, PreferredLanguageCode = input.PreferredLanguageCode };
			AdmissionInquiryConfiguredFields.ApplyOperationalValues(definitions, mappingValues, operational, false);
			input.Source = operational.Source;
			input.SourceDetail = operational.SourceDetail;
			input.PreferredLanguageCode = operational.PreferredLanguageCode;
			if (applicant != null && !AdmissionPrimaryContactSelection.HasSelection(applicant))
				AdmissionFirstParentContactMapping.Apply(applicant, definitions
					.Where(field => field.BindingType == AdmissionFormBindingType.System && field.BindingKey != null && values.ContainsKey(field.Code))
					.GroupBy(field => field.BindingKey!, StringComparer.OrdinalIgnoreCase)
					.ToDictionary(group => group.Key, group => values[group.Last().Code], StringComparer.OrdinalIgnoreCase), guardian);
			var snapshot = input.FormValues.Join(definitions, value => value.FieldCode, field => field.Code,
				(value, field) => new { Key = string.IsNullOrWhiteSpace(field.BindingKey) ? field.Code : field.BindingKey!, value.ValueJson }, StringComparer.OrdinalIgnoreCase)
				.GroupBy(value => value.Key, StringComparer.OrdinalIgnoreCase).ToDictionary(group => group.Key, group => group.Last().ValueJson, StringComparer.OrdinalIgnoreCase);
			if (!changedOffering)
				foreach (var hidden in definitions.Where(field => !visibleFieldCodes.Contains(field.Code) && !values.ContainsKey(field.Code)))
				{
					var key = string.IsNullOrWhiteSpace(hidden.BindingKey) ? hidden.Code : hidden.BindingKey!;
					if (original.TryGetValue(key, out var raw)) snapshot.TryAdd(key, raw);
				}
			inquiry.RecordFormResponseSnapshot(JsonSerializer.Serialize(snapshot), formId);
			if (applicant != null) input.ApplicantName = string.Join(" ", new[] { applicant.FirstName, applicant.MiddleName, applicant.LastName }.Where(value => !string.IsNullOrWhiteSpace(value)));
			if (guardian != null && applicant?.ExistingStudentId.HasValue != true && !confirmedSibling)
			{
				if (definitions.Any(field => field.BindingKey == "Guardian.Mobile")) input.Phone = guardian.Mobile;
				if (definitions.Any(field => field.BindingKey == "Guardian.Email")) input.Email = guardian.Email;
			}
		}
		else if (changedOffering)
			throw new BusinessException(AdmissionApplicationErrorCodes.ValidationFailed, "Complete the selected offering's inquiry form before changing the admission option.");
		if (applicant != null)
		{
			if (input.FormValues == null && input.GuardianResponsibilityConfirmed && AdmissionPrimaryContactSelection.ReadRelationship(applicant) == "guardian" && guardian != null)
				AdmissionPrimaryContactSelection.Capture(applicant, "guardian", string.Join(" ", new[] { guardian.FirstName, guardian.LastName }.Where(value => !string.IsNullOrWhiteSpace(value))),
					input.Phone, input.Email, true, CurrentUser.Id, Clock.Now, false);
			if (input.FormValues == null && !string.Equals(input.ApplicantName.Trim(), inquiry.ApplicantName, StringComparison.Ordinal))
				applicant.UpdateProfile(input.ApplicantName, null, null, applicant.DateOfBirth, applicant.Gender, applicant.AddressLine1, applicant.AddressLine2, applicant.City, applicant.State, applicant.Country, applicant.PostalCode);
			await applicants.UpdateAsync(applicant, false);
		}
		if (guardian != null && applicant?.ExistingStudentId.HasValue != true && !confirmedSibling)
		{
			guardian.UpdateContact(guardian.FirstName, guardian.LastName, input.Phone, input.Email);
			if (applicant != null && input.FormValues == null)
			{
				AdmissionFirstParentContactMapping.Apply(applicant, new Dictionary<string, string>
				{
					["Guardian.Mobile"] = input.Phone, ["Guardian.Email"] = input.Email ?? string.Empty
				}, guardian);
				await applicants.UpdateAsync(applicant, false);
			}
			await guardians.UpdateAsync(guardian, false);
		}
		if (applicant != null) AdmissionFamilyContactCapture.CopyToInquiry(applicant, inquiry);
	}
}
