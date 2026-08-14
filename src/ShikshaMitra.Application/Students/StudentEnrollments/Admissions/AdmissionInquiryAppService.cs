using System;
using System.Collections.Generic;
using System.Linq;
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
public class AdmissionInquiryAppService(
	IRepository<AdmissionInquiry, Guid> inquiryRepository,
	IRepository<AdmissionCycle, Guid> cycleRepository,
	IRepository<AdmissionFamily, Guid> familyRepository,
	IRepository<AdmissionGuardian, Guid> guardianRepository,
	IRepository<AdmissionApplicant, Guid> applicantRepository,
	IRepository<AdmissionOffering, Guid> offeringRepository,
	IRepository<AdmissionFormDefinition, Guid> formRepository,
	IRepository<AdmissionFormFieldDefinition, Guid> formFieldRepository,
	IRepository<AdmissionActivity, Guid> activityRepository) : ApplicationService, IAdmissionInquiryAppService
{
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionInquiries.View)]
	public async Task<PagedResultDto<AdmissionInquiryDto>> GetListAsync(AdmissionInquiryListInput input)
	{
		var query = await inquiryRepository.GetQueryableAsync();
		query = query.Where(x => (!input.AdmissionCycleId.HasValue || x.AdmissionCycleId == input.AdmissionCycleId) &&
			(!input.AdmissionOfferingId.HasValue || x.AdmissionOfferingId == input.AdmissionOfferingId) &&
			(!input.Status.HasValue || x.Status == input.Status) &&
			(!input.Source.HasValue || x.Source == input.Source) &&
			(!input.AssignedCounsellorId.HasValue || x.AssignedCounsellorId == input.AssignedCounsellorId) &&
			(string.IsNullOrWhiteSpace(input.Search) || x.ApplicantName.Contains(input.Search) || x.NormalizedPhone.Contains(input.Search)));
		var count = await AsyncExecuter.CountAsync(query);
		var rows = await AsyncExecuter.ToListAsync(query.OrderByDescending(x => x.CreationTime).Skip(input.SkipCount).Take(input.MaxResultCount));
		return new PagedResultDto<AdmissionInquiryDto>(count, rows.Select(Map).ToList());
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionInquiries.View)]
	public async Task<AdmissionInquiryDto> GetAsync(Guid id) => Map(await GetInquiryAsync(id));

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionInquiries.Create)]
	public async Task<AdmissionInquiryDto> CreateAsync(CreateAdmissionInquiryDto input)
	{
		if (await cycleRepository.FindAsync(input.AdmissionCycleId) == null)
			throw new BusinessException(AdmissionErrorCodes.InquiryCycleNotFound);
		var values = input.FormValues.ToDictionary(x => x.FieldCode, x => UnwrapValue(x.ValueJson), StringComparer.OrdinalIgnoreCase);
		var inquiryFields = new List<AdmissionFormFieldDefinition>();
		if (input.AdmissionOfferingId.HasValue)
		{
			var offering = await offeringRepository.FindAsync(input.AdmissionOfferingId.Value);
			if (offering?.InquiryFormDefinitionId is Guid inquiryFormId)
			{
				var form = await formRepository.FindAsync(inquiryFormId); var fields = await formFieldRepository.GetListAsync(x => x.FormDefinitionId == inquiryFormId); inquiryFields = fields;
				if (form == null || form.Status != AdmissionFormStatus.Published) throw new BusinessException(AdmissionApplicationErrorCodes.ConfigurationNotPublished);
				var missing = fields.Where(x => x.Required && IsVisible(x.VisibilityRuleJson, values) && (!values.TryGetValue(x.Code, out var value) || string.IsNullOrWhiteSpace(value)) && string.IsNullOrWhiteSpace(x.DefaultValue)).Select(x => x.Code).ToList();
				if (missing.Count > 0) throw new BusinessException(AdmissionApplicationErrorCodes.ValidationFailed).WithData("Fields", string.Join(", ", missing));
			}
		}
		input.ApplicantFirstName = ResolveBinding(values, inquiryFields, "Applicant.FirstName", input.ApplicantFirstName) ?? input.ApplicantFirstName;
		input.ApplicantLastName = ResolveBinding(values, inquiryFields, "Applicant.LastName", input.ApplicantLastName);
		input.GuardianFirstName = ResolveBinding(values, inquiryFields, "Guardian.FirstName", input.GuardianFirstName) ?? input.GuardianFirstName;
		input.GuardianPhone = ResolveBinding(values, inquiryFields, "Guardian.Mobile", input.GuardianPhone) ?? input.GuardianPhone;
		input.GuardianEmail = ResolveBinding(values, inquiryFields, "Guardian.Email", input.GuardianEmail);
		input.ApplicantName = ResolveBinding(values, inquiryFields, "Applicant.Name", input.ApplicantName) ?? input.ApplicantName;
		if (string.IsNullOrWhiteSpace(input.ApplicantName))
			input.ApplicantName = string.Join(" ", new[] { input.ApplicantFirstName, input.ApplicantLastName }.Where(x => !string.IsNullOrWhiteSpace(x)));
		var phone = string.Concat((string.IsNullOrWhiteSpace(input.Phone) ? input.GuardianPhone : input.Phone).Where(char.IsDigit));
		var query = await inquiryRepository.GetQueryableAsync();
		if (!input.ExistingFamilyId.HasValue && !input.CreateSeparateFamily && await AsyncExecuter.AnyAsync(query.Where(x => x.AdmissionCycleId == input.AdmissionCycleId && x.NormalizedPhone == phone)))
			throw new BusinessException(AdmissionErrorCodes.InquiryDuplicate);
		var family = input.ExistingFamilyId.HasValue ? await familyRepository.FindAsync(input.ExistingFamilyId.Value) : null;
		if (input.ExistingFamilyId.HasValue && family == null) throw new BusinessException(AdmissionApplicantErrorCodes.ApplicantFamilyNotFound);
		family ??= new AdmissionFamily(GuidGenerator.Create(), CurrentTenant.Id, $"FAM/{DateTime.UtcNow:yyyy}/{GuidGenerator.Create():N}"[..20]);
		var guardian = family.PrimaryGuardianId.HasValue ? await guardianRepository.FindAsync(family.PrimaryGuardianId.Value) : null;
		var newGuardian = guardian == null;
		if (newGuardian) { guardian = new AdmissionGuardian(GuidGenerator.Create(), CurrentTenant.Id, family.Id, input.GuardianFirstName, input.GuardianLastName, input.GuardianPhone, input.GuardianEmail, AdmissionGuardianRelationship.Parent, true); family.SetPrimaryGuardian(guardian.Id); }
		var applicant = new AdmissionApplicant(GuidGenerator.Create(), CurrentTenant.Id, family.Id, input.ApplicantFirstName, null, input.ApplicantLastName, input.ApplicantDateOfBirth, null, null);
		if (!input.ExistingFamilyId.HasValue) await familyRepository.InsertAsync(family, autoSave: false);
		if (newGuardian) await guardianRepository.InsertAsync(guardian!, autoSave: false);
		await applicantRepository.InsertAsync(applicant, autoSave: false);
		var inquiry = new AdmissionInquiry(GuidGenerator.Create(), CurrentTenant.Id, input.AdmissionCycleId, input.AdmissionOfferingId,
			input.ApplicantName, phone, input.Email, input.Source, input.SourceDetail);
		inquiry.LinkIdentity(family.Id, applicant.Id);
		await inquiryRepository.InsertAsync(inquiry, autoSave: true);
		await activityRepository.InsertAsync(new AdmissionActivity(GuidGenerator.Create(), CurrentTenant.Id, "InquiryCreated", "Inquiry created", applicant.Id, null, inquiry.Id), autoSave: true);
		return Map(inquiry);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionInquiries.Manage)]
	public async Task<AdmissionInquiryDto> SetStatusAsync(Guid id, AdmissionInquiryStatus status, Guid? duplicateOfId = null)
	{
		var inquiry = await GetInquiryAsync(id);
		if (status == AdmissionInquiryStatus.Duplicate && (!duplicateOfId.HasValue || duplicateOfId == id || await inquiryRepository.FindAsync(duplicateOfId.Value) == null))
			throw new BusinessException(AdmissionErrorCodes.InquiryDuplicateTargetInvalid);
		inquiry.SetStatus(status, duplicateOfId);
		await inquiryRepository.UpdateAsync(inquiry, autoSave: true);
		await activityRepository.InsertAsync(new AdmissionActivity(GuidGenerator.Create(), CurrentTenant.Id, "InquiryStatusChanged", $"Inquiry status changed to {status}", inquiry.ApplicantId, null, inquiry.Id), autoSave: true);
		return Map(inquiry);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionInquiries.Manage)]
	public async Task<AdmissionInquiryDto> MarkContactedAsync(Guid id)
	{
		var inquiry = await GetInquiryAsync(id);
		inquiry.MarkContacted(DateTime.UtcNow);
		await inquiryRepository.UpdateAsync(inquiry, autoSave: true);
		await activityRepository.InsertAsync(new AdmissionActivity(GuidGenerator.Create(), CurrentTenant.Id, "InquiryContacted", "Inquiry marked as contacted", inquiry.ApplicantId, null, inquiry.Id), autoSave: true);
		return Map(inquiry);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionInquiries.Manage)]
	public async Task<System.Collections.Generic.List<AdmissionInquiryDuplicateMatchDto>> DuplicateCheckAsync(AdmissionInquiryDuplicateCheckInput input)
	{
		var phone = string.Concat((input.GuardianPhone ?? string.Empty).Where(char.IsDigit));
		var guardians = await guardianRepository.GetListAsync(x => x.Mobile == phone || (!string.IsNullOrWhiteSpace(input.GuardianEmail) && x.Email == input.GuardianEmail!.Trim().ToLowerInvariant()));
		var applicants = await applicantRepository.GetListAsync(x => (!string.IsNullOrWhiteSpace(input.ApplicantName) && (x.FirstName + " " + (x.LastName ?? string.Empty)).Trim().Contains(input.ApplicantName)) && (!input.ApplicantDateOfBirth.HasValue || x.DateOfBirth == input.ApplicantDateOfBirth.Value.Date));
		var familyIds = guardians.Select(x => x.FamilyId).Concat(applicants.Select(x => x.FamilyId)).Distinct().ToList();
		var allApplicants = await applicantRepository.GetListAsync(x => familyIds.Contains(x.FamilyId));
		var familyGuardians = await guardianRepository.GetListAsync(x => familyIds.Contains(x.FamilyId) && x.IsPrimaryContact);
		return allApplicants.Select(x => { var guardian = familyGuardians.FirstOrDefault(g => g.FamilyId == x.FamilyId); return new AdmissionInquiryDuplicateMatchDto { ApplicantId = x.Id, FamilyId = x.FamilyId, ApplicantName = string.Join(" ", new[] { x.FirstName, x.LastName }.Where(y => !string.IsNullOrWhiteSpace(y))), GuardianPhone = guardian?.Mobile, GuardianEmail = guardian?.Email, DateOfBirth = x.DateOfBirth }; }).ToList();
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionInquiries.Assign)]
	public async Task<AdmissionInquiryDto> AssignCounsellorAsync(Guid id, AssignAdmissionCounsellorDto input)
	{
		var inquiry = await GetInquiryAsync(id);
		Guid counsellorId;
		if (input.Mode == AdmissionCounsellorAssignmentMode.Manual)
		{
			counsellorId = input.CounsellorId ?? throw new BusinessException(AdmissionErrorCodes.InquiryCounsellorRequired);
		}
		else
		{
			if (input.CandidateCounsellorIds.Count == 0) throw new BusinessException(AdmissionErrorCodes.InquiryCounsellorCandidatesRequired);
			var assigned = await AsyncExecuter.ToListAsync((await inquiryRepository.GetQueryableAsync()).Where(x => x.AssignedCounsellorId.HasValue && input.CandidateCounsellorIds.Contains(x.AssignedCounsellorId.Value)).GroupBy(x => x.AssignedCounsellorId).Select(x => new { Id = x.Key!.Value, Count = x.Count() }));
			counsellorId = input.CandidateCounsellorIds.OrderBy(x => assigned.FirstOrDefault(a => a.Id == x)?.Count ?? 0).ThenBy(x => x).First();
		}
		inquiry.AssignCounsellor(counsellorId); await inquiryRepository.UpdateAsync(inquiry, false); await activityRepository.InsertAsync(new AdmissionActivity(GuidGenerator.Create(), CurrentTenant.Id, "CounsellorAssigned", $"Counsellor assigned: {counsellorId}", inquiry.ApplicantId, null, inquiry.Id), true); return Map(inquiry);
	}

	private async Task<AdmissionInquiry> GetInquiryAsync(Guid id) => await inquiryRepository.FindAsync(id) ?? throw new EntityNotFoundException(typeof(AdmissionInquiry), id);
	private static AdmissionInquiryDto Map(AdmissionInquiry x) => new()
	{
		Id = x.Id, InquiryNumber = x.InquiryNumber, AdmissionCycleId = x.AdmissionCycleId, AdmissionOfferingId = x.AdmissionOfferingId, FamilyId = x.FamilyId, ApplicantId = x.ApplicantId, AssignedCounsellorId = x.AssignedCounsellorId,
		ApplicantName = x.ApplicantName, NormalizedPhone = x.NormalizedPhone, Email = x.Email,
		Source = x.Source, SourceDetail = x.SourceDetail, Status = x.Status, DuplicateOfId = x.DuplicateOfId,
		ContactedAt = x.ContactedAt, LastActivityAt = x.LastActivityAt, CreationTime = x.CreationTime, CreatorId = x.CreatorId,
		LastModificationTime = x.LastModificationTime, LastModifierId = x.LastModifierId
	};
	private static string UnwrapValue(string? value) { if (string.IsNullOrWhiteSpace(value) || value == "null") return string.Empty; try { using var json = System.Text.Json.JsonDocument.Parse(value); return json.RootElement.ValueKind == System.Text.Json.JsonValueKind.String ? json.RootElement.GetString() ?? string.Empty : json.RootElement.ToString(); } catch { return value; } }
	private static string? ValueOr(IReadOnlyDictionary<string, string> values, string key, string? fallback) => values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : fallback;
	private static string? ResolveBinding(IReadOnlyDictionary<string, string> values, IEnumerable<AdmissionFormFieldDefinition> fields, string bindingKey, string? fallback) { var code = fields.FirstOrDefault(x => string.Equals(x.BindingKey, bindingKey, StringComparison.OrdinalIgnoreCase))?.Code; return code == null ? fallback : ValueOr(values, code, fallback); }
	private static bool IsVisible(string? ruleJson, IReadOnlyDictionary<string, string> values) { if (string.IsNullOrWhiteSpace(ruleJson)) return true; try { using var document = System.Text.Json.JsonDocument.Parse(ruleJson); return EvaluateRule(document.RootElement, values); } catch { return false; } }
	private static bool EvaluateRule(System.Text.Json.JsonElement rule, IReadOnlyDictionary<string, string> values) { if (rule.TryGetProperty("conditions", out var conditions) && conditions.ValueKind == System.Text.Json.JsonValueKind.Array) { var results = conditions.EnumerateArray().Select(x => EvaluateRule(x, values)); return string.Equals(rule.GetProperty("operator").GetString(), "ANY", StringComparison.OrdinalIgnoreCase) ? results.Any(x => x) : results.All(x => x); } var field = rule.TryGetProperty("field", out var fieldProperty) ? fieldProperty.GetString() : null; var actual = field != null && values.TryGetValue(field, out var value) ? value : string.Empty; var expected = rule.TryGetProperty("value", out var expectedProperty) ? expectedProperty.ToString().Trim('"') : string.Empty; return (rule.TryGetProperty("operator", out var operatorProperty) ? operatorProperty.GetString() : string.Empty)?.ToLowerInvariant() switch { "equals" => string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase), "isnotempty" => !string.IsNullOrWhiteSpace(actual), "isempty" => string.IsNullOrWhiteSpace(actual), "contains" => actual.Contains(expected, StringComparison.OrdinalIgnoreCase), "notequals" => !string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase), _ => true };
}
}
