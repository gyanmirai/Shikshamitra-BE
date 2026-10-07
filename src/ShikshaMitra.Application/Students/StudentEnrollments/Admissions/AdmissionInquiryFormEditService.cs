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
		var definitions = formId.HasValue ? await fields.GetListAsync(field => field.FormDefinitionId == formId.Value) : [];
		if (input.FormValues != null)
		{
			if (!formId.HasValue || (await forms.FindAsync(formId.Value))?.Status is not (AdmissionFormStatus.Published or AdmissionFormStatus.Archived))
				throw new BusinessException(AdmissionApplicationErrorCodes.ConfigurationNotPublished);
			if (input.FormValues.GroupBy(value => value.FieldCode, StringComparer.OrdinalIgnoreCase).Any(group => group.Count() > 1))
				throw new BusinessException(AdmissionInquiryManagementErrorCodes.InvalidInput, "Submit each inquiry field once.");
			var codes = definitions.Select(field => field.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
			if (input.FormValues.Any(value => !codes.Contains(value.FieldCode)))
				throw new BusinessException(AdmissionApplicationErrorCodes.UnknownFormField);
			var values = input.FormValues.ToDictionary(value => value.FieldCode,
				value => AdmissionApplicationValueConverter.Unwrap(value.ValueJson), StringComparer.OrdinalIgnoreCase);
			var original = string.IsNullOrWhiteSpace(inquiry.FormResponseSnapshotJson) ? new Dictionary<string, string>() :
				JsonSerializer.Deserialize<Dictionary<string, string>>(inquiry.FormResponseSnapshotJson) ?? [];
			if (!changedOffering && applicant != null)
				foreach (var field in definitions.Where(field => field.BindingType == AdmissionFormBindingType.System && !string.IsNullOrWhiteSpace(field.BindingKey)))
					if (!original.ContainsKey(field.BindingKey!))
						original[field.BindingKey!] = JsonSerializer.Serialize(AdmissionFormBindingRegistry.GetValue(field.BindingKey!, applicant, guardian));
			if (!changedOffering && definitions.Where(field => field.ReadOnly).Any(field =>
				original.TryGetValue(string.IsNullOrWhiteSpace(field.BindingKey) ? field.Code : field.BindingKey!, out var raw) &&
				(!values.TryGetValue(field.Code, out var edited) || AdmissionApplicationValueConverter.Unwrap(raw) != edited)))
				throw new BusinessException(AdmissionInquiryManagementErrorCodes.InvalidInput, "Read-only inquiry fields cannot be changed.");
			var validation = validator.Validate(definitions, await sections.GetListAsync(section => section.FormDefinitionId == formId.Value), values);
			if (validation.Errors.Count > 0)
				throw new BusinessException(AdmissionApplicationErrorCodes.ValidationFailed).WithData("Fields", string.Join(", ", validation.FieldErrors.Keys));
			foreach (var definition in definitions.Where(field => field.BindingType == AdmissionFormBindingType.System && !string.IsNullOrWhiteSpace(field.BindingKey)))
			{
				if (applicant == null || !values.TryGetValue(definition.Code, out var value)) continue;
				if (definition.BindingKey == "Family.SiblingStudentIds")
				{
					var siblings = AdmissionStudentProfileMapping.ReadSiblings(value) ?? [];
					if (siblings.Contains(applicant.ExistingStudentId ?? applicant.ReservedStudentId) || !await dataSources.AreSiblingIdsAllowedAsync(siblings))
						throw new BusinessException("Admissions:InvalidSiblingSelection");
				}
				if (applicant.ExistingStudentId.HasValue && definition.BindingKey!.StartsWith("Guardian.", StringComparison.OrdinalIgnoreCase)) continue;
				if (string.IsNullOrWhiteSpace(value) && definition.BindingKey is "Applicant.FirstName" or "Guardian.FirstName" or "Guardian.Mobile")
					throw new BusinessException(AdmissionErrorCodes.InquiryIdentityIncomplete);
				if (string.IsNullOrWhiteSpace(value) && definition.BindingKey == "Applicant.DateOfBirth")
					applicant.UpdateProfile(applicant.FirstName, applicant.MiddleName, applicant.LastName, null, applicant.Gender,
						applicant.AddressLine1, applicant.AddressLine2, applicant.City, applicant.State, applicant.Country, applicant.PostalCode);
				else AdmissionFormBindingRegistry.ApplyValue(definition.BindingKey!, value, applicant, guardian);
			}
			var snapshot = input.FormValues.Join(definitions, value => value.FieldCode, field => field.Code,
				(value, field) => new { Key = string.IsNullOrWhiteSpace(field.BindingKey) ? field.Code : field.BindingKey!, value.ValueJson }, StringComparer.OrdinalIgnoreCase)
				.GroupBy(value => value.Key, StringComparer.OrdinalIgnoreCase).ToDictionary(group => group.Key, group => group.Last().ValueJson, StringComparer.OrdinalIgnoreCase);
			inquiry.RecordFormResponseSnapshot(JsonSerializer.Serialize(snapshot), formId);
			if (applicant != null) input.ApplicantName = string.Join(" ", new[] { applicant.FirstName, applicant.MiddleName, applicant.LastName }.Where(value => !string.IsNullOrWhiteSpace(value)));
			if (guardian != null && applicant?.ExistingStudentId.HasValue != true)
			{
				if (definitions.Any(field => field.BindingKey == "Guardian.Mobile")) input.Phone = guardian.Mobile;
				if (definitions.Any(field => field.BindingKey == "Guardian.Email")) input.Email = guardian.Email;
			}
		}
		else if (changedOffering)
			throw new BusinessException(AdmissionApplicationErrorCodes.ValidationFailed, "Complete the selected offering's inquiry form before changing the admission option.");
		if (applicant != null)
		{
			if (input.FormValues == null && !string.Equals(input.ApplicantName.Trim(), inquiry.ApplicantName, StringComparison.Ordinal))
				applicant.UpdateProfile(input.ApplicantName, null, null, applicant.DateOfBirth, applicant.Gender, applicant.AddressLine1, applicant.AddressLine2, applicant.City, applicant.State, applicant.Country, applicant.PostalCode);
			await applicants.UpdateAsync(applicant, false);
		}
		if (guardian != null && applicant?.ExistingStudentId.HasValue != true)
		{
			guardian.UpdateContact(guardian.FirstName, guardian.LastName, input.Phone, input.Email);
			await guardians.UpdateAsync(guardian, false);
		}
	}
}
