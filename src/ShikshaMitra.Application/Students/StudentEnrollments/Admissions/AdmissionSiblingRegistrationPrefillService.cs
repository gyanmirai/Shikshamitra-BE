using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

/// <summary>Optional target-only snapshot suggestions from an explicitly confirmed, currently accessible sibling.</summary>
[RemoteService(false)]
public class AdmissionSiblingRegistrationPrefillService(
	IRepository<AdmissionFamily, Guid> families,
	IRepository<AdmissionFormChangeHistory, Guid> histories,
	IStudentRepository students,
	AdmissionApplicantScopeAccessService applicantScope,
	AdmissionInquiryScopeAccessService inquiryScope,
	StudentScopeAccessService studentScope,
	AdmissionApplicationFormValidator formValidator,
	IPermissionChecker permissions) : ApplicationService, ITransientDependency
{
	public async Task<AdmissionApplicationFormDto> ProjectAsync(AdmissionApplicant target, AdmissionFormInstance instance,
		AdmissionFormDto definition, List<AdmissionFormValueDto> storedValues, bool initial = false)
	{
		var result = new AdmissionApplicationFormDto { Values = storedValues.Select(value => new AdmissionFormValueDto
			{ FieldCode = value.FieldCode, ValueJson = value.ValueJson }).ToList() };
		if (target.TenantId != CurrentTenant.Id || instance.TenantId != CurrentTenant.Id ||
			instance.Status != AdmissionFormInstanceStatus.Draft || instance.SubmittedAt.HasValue) return result;
		var confirmation = AdmissionSiblingParentSelection.ReadConfirmation(target);
		if (confirmation == null || confirmation.SourceApplicantId == target.Id || confirmation.ConfirmedBy == Guid.Empty) return result;
		var family = await families.FindAsync(target.FamilyId);
		if (family == null || family.TenantId != CurrentTenant.Id || family.Status != AdmissionFamilyStatus.Active) return result;
		Guid? sourceApplicationId = null;
		if (await permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.View))
			sourceApplicationId = await AsyncExecuter.FirstOrDefaultAsync((await applicantScope.GetApplicationsAsync(confirmation.SourceApplicantId))
				.OrderByDescending(application => application.CreationTime).Select(application => (Guid?)application.Id));
		var canReadSource = sourceApplicationId.HasValue;
		if (!canReadSource && await permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Students.AdmissionInquiries.View))
			canReadSource = await AsyncExecuter.AnyAsync((await inquiryScope.GetScopedQueryAsync()).Where(inquiry => inquiry.ApplicantId == confirmation.SourceApplicantId));
		if (!canReadSource) return result;
		AdmissionApplicant source;
		try { source = await applicantScope.GetAsync(confirmation.SourceApplicantId); }
		catch (EntityNotFoundException) { return result; }
		if (source.FamilyId != target.FamilyId || source.TenantId != CurrentTenant.Id) return result;
		var sourceProfiles = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
		var studentId = source.ExistingStudentId ?? source.ReservedStudentId;
		var studentQuery = (await students.GetQueryableWithoutAutoIncludesAsync()).Where(student => student.TenantId == CurrentTenant.Id && student.Id == studentId);
		if (await AsyncExecuter.AnyAsync(studentQuery))
		{
			var scopedStudents = await studentScope.ApplyCurrentUserScopeAsync(studentQuery);
			var current = await AsyncExecuter.FirstOrDefaultAsync(scopedStudents.Select(student => new { student.FamilyDetail }));
			if (current == null) return result;
			foreach (var role in new[] { "Father", "Mother" })
			{
				var parent = role == "Father" ? current.FamilyDetail?.FatherDetails : current.FamilyDetail?.MotherDetails;
				if (parent == null) continue;
				sourceProfiles[role + ".Name"] = parent.FullName;
				sourceProfiles[role + ".Mobile"] = parent.ContactNo;
				sourceProfiles[role + ".Email"] = parent.Email;
				sourceProfiles[role + ".Occupation"] = parent.Occupation;
				sourceProfiles[role + ".Qualification"] = parent.Qualification;
				sourceProfiles[role + ".Designation"] = parent.Designation;
				sourceProfiles[role + ".Employer"] = parent.Organization;
			}
		}
		else
		{
			if (source.ExistingStudentId.HasValue) return result;
			foreach (var key in AdmissionSiblingRegistrationPrefillPolicy.ParentBindings)
				sourceProfiles[key] = AdmissionStudentProfileMapping.Read(source, key);
		}
		result.FamilyPrefill = new AdmissionSiblingFamilyPrefillDto
		{
			SourceApplicantId = source.Id,
			SourceApplicantName = string.Join(" ", new[] { source.FirstName, source.MiddleName, source.LastName }.Where(value => !string.IsNullOrWhiteSpace(value))),
			SourceApplicationId = sourceApplicationId,
			Relationship = confirmation.Relationship
		};
		var history = initial ? [] : await histories.GetListAsync(item => item.FormInstanceId == instance.Id && item.TenantId == CurrentTenant.Id);
		if (history.Any(item => item.ChangeType is "FormCorrected" or "Submitted" or "Reopened")) return result;
		var authored = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (var save in history.Where(item => item.ChangeType == "DraftSaved"))
		{
			try { authored.UnionWith(JsonSerializer.Deserialize<string[]>(save.ChangedFieldsJson ?? "[]") ?? []); }
			catch (JsonException) { return result; } // Ambiguous old authored history must never resurrect a clear.
		}
		AdmissionSiblingRegistrationPrefillPolicy.Merge(target, definition, sourceProfiles, confirmation.Relationship, authored, result);
		AdmissionSiblingRegistrationPrefillPolicy.Constrain(definition, storedValues, result, formValidator);
		return result;
	}

	internal Task RecordAsync(AdmissionFormInstance instance, AdmissionSiblingFamilyPrefillDto source, IEnumerable<string> fieldCodes, bool clientReported = false) =>
		histories.InsertAsync(new AdmissionFormChangeHistory(GuidGenerator.Create(), CurrentTenant.Id, instance.Id, instance.ApplicationId,
			"FamilyPrefilled", JsonSerializer.Serialize(new { source.SourceApplicantId, source.SourceApplicationId, source.Relationship,
				FieldCodes = fieldCodes.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(code => code).ToArray(), ClientReported = clientReported }), CurrentUser.Id), false);
}

internal static class AdmissionSiblingRegistrationPrefillPolicy
{
	internal static readonly string[] ParentBindings = (from role in new[] { "Father", "Mother" }
		from field in new[] { "Name", "Mobile", "Email", "Occupation", "Qualification", "Designation", "Employer" }
		select role + "." + field).ToArray();
	private static readonly HashSet<string> Allowed = new(ParentBindings, StringComparer.OrdinalIgnoreCase);
	private static readonly HashSet<string> GuardianAliases = new(["Guardian.FirstName", "Guardian.LastName", "Guardian.Mobile", "Guardian.Email", "Guardian.Occupation"], StringComparer.OrdinalIgnoreCase);
	internal static bool CanAutomaticallyFill(AdmissionFormFieldDto field) => !field.ReadOnly && field.BindingType == AdmissionFormBindingType.System &&
		field.BindingKey != null && (Allowed.Contains(field.BindingKey) || GuardianAliases.Contains(field.BindingKey)) &&
		field.FieldType is AdmissionFormFieldType.Text or AdmissionFormFieldType.Textarea or AdmissionFormFieldType.Email or AdmissionFormFieldType.Phone or AdmissionFormFieldType.Select or AdmissionFormFieldType.Radio &&
		!(field.FieldType is AdmissionFormFieldType.Select or AdmissionFormFieldType.Radio && AdmissionFormMasterDataValidator.TrySource(field.OptionsJson, out _, out _));

	internal static void Merge(AdmissionApplicant target, AdmissionFormDto definition, IReadOnlyDictionary<string, string?> source,
		string relationship, IReadOnlySet<string> authored, AdmissionApplicationFormDto result)
	{
		var fields = definition.Sections.Where(section => !section.IsRepeatable).SelectMany(section => section.Fields).ToList();
		var selected = relationship == "father" ? "Father" : "Mother";
		var genericParent = CanUseSelectedParentAliases(target, fields, result.Values, relationship, source);
		var conflicts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (var role in new[] { "Father", "Mother" })
		{
			var sourceHasIdentity = false;
			var targetHasIdentity = false;
			var matchingIdentity = false;
			foreach (var attribute in new[] { "Name", "Mobile", "Email" })
			{
				var key = role + "." + attribute;
				var sourceValue = source.GetValueOrDefault(key);
				var targetValues = fields.Where(field => field.BindingType == AdmissionFormBindingType.System && string.Equals(field.BindingKey, key, StringComparison.OrdinalIgnoreCase))
					.Select(field => Read(result.Values, field.Code)).Append(AdmissionStudentProfileMapping.Read(target, key)).ToList();
				if (targetValues.Any(value => Different(attribute, value, sourceValue))) conflicts.Add(role);
				sourceHasIdentity |= !Empty(sourceValue);
				targetHasIdentity |= targetValues.Any(value => !Empty(value));
				matchingIdentity |= !Empty(sourceValue) && targetValues.Any(value => !Empty(value) && !Different(attribute, value, sourceValue));
			}
			// Confirmation establishes the selected role. Another role still needs a saved
			// source identity, and captured target identity must actually overlap with it.
			if (role != selected && (!sourceHasIdentity || targetHasIdentity && !matchingIdentity)) conflicts.Add(role);
		}
		foreach (var field in fields.Where(field => !field.ReadOnly && field.BindingType == AdmissionFormBindingType.System && field.BindingKey != null))
		{
			var key = field.BindingKey!;
			var generic = GuardianAliases.Contains(key);
			if (generic && !genericParent) continue;
			if (!Allowed.Contains(key) && !generic || authored.Contains(field.Code) || !Empty(Read(result.Values, field.Code)) ||
				!Empty(AdmissionStudentProfileMapping.Read(target, key))) continue;
			var role = generic ? selected : key.Split('.')[0];
			if (conflicts.Contains(role)) continue;
			var sourceKey = generic ? selected + "." + key.Split('.')[1] : key;
			// These two editable bindings write the same selected parent's profession.
			// An authored clear or saved value in either alias owns that answer.
			if (sourceKey == selected + ".Occupation")
			{
				var aliases = fields.Where(item => item.BindingType == AdmissionFormBindingType.System &&
					(item.BindingKey == selected + ".Occupation" || genericParent && item.BindingKey == "Guardian.Occupation"));
				if (aliases.Any(item => authored.Contains(item.Code) || !Empty(Read(result.Values, item.Code)) &&
					!result.AutoFilledFieldCodes.Contains(item.Code)) ||
					!Empty(AdmissionStudentProfileMapping.Read(target, selected + ".Occupation")) ||
					genericParent && !Empty(AdmissionStudentProfileMapping.Read(target, "Guardian.Occupation"))) continue;
			}
			string? value;
			if (generic && key is "Guardian.FirstName" or "Guardian.LastName")
			{
				var parts = (source.GetValueOrDefault(selected + ".Name") ?? string.Empty).Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
				value = key == "Guardian.FirstName" ? parts.FirstOrDefault() : parts.ElementAtOrDefault(1);
			}
			else value = source.GetValueOrDefault(sourceKey);
			if (Empty(value)) continue;
			var existing = result.Values.FirstOrDefault(item => string.Equals(item.FieldCode, field.Code, StringComparison.OrdinalIgnoreCase));
			if (existing == null) result.Values.Add(new() { FieldCode = field.Code, ValueJson = JsonSerializer.Serialize(value) });
			else existing.ValueJson = JsonSerializer.Serialize(value);
			result.AutoFilledFieldCodes.Add(field.Code);
		}
	}

	// Generic answers can be aliases for the confirmed parent only while their saved/current
	// identity agrees. A separately captured Guardian is a person, not an occupation alias.
	internal static bool CanUseSelectedParentAliases(AdmissionApplicant target, IEnumerable<AdmissionFormFieldDto> fields,
		IEnumerable<AdmissionFormValueDto> values, string relationship, IReadOnlyDictionary<string, string?>? source = null)
	{
		if (!AdmissionSiblingParentSelection.IsRelationship(relationship)) return false;
		var selected = relationship == "father" ? "Father" : "Mother";
		var confirmation = AdmissionSiblingParentSelection.ReadConfirmation(target);
		string? Selected(string suffix) => new[] { source?.GetValueOrDefault(selected + "." + suffix),
			AdmissionStudentProfileMapping.Read(target, selected + "." + suffix), suffix switch
			{ "Name" => confirmation?.Name, "Mobile" => confirmation?.Mobile, "Email" => confirmation?.Email, _ => null } }
			.FirstOrDefault(value => !Empty(value));
		var name = (Selected("Name") ?? string.Empty).Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
		bool Conflict(string attribute, string? actual, string? expected) => !Empty(actual) && (Empty(expected) || Different(attribute, actual, expected));
		var savedValues = values.ToList();
		var independentIdentity = false;
		foreach (var field in fields.Where(field => field.BindingType == AdmissionFormBindingType.System))
		{
			var actual = Read(savedValues, field.Code);
			var conflict = field.BindingKey switch
			{
				"Guardian.FirstName" => Conflict("Name", actual, name.FirstOrDefault()),
				"Guardian.LastName" => Conflict("Name", actual, name.ElementAtOrDefault(1)),
				"Guardian.Mobile" => Conflict("Mobile", actual, Selected("Mobile")),
				"Guardian.Email" => Conflict("Email", actual, Selected("Email")),
				"Guardian.AlternateName" => Conflict("Name", actual, Selected("Name")),
				_ => false
			};
			if (conflict) return false;
			independentIdentity |= field.BindingKey == "Guardian.AlternateName" && !Empty(actual);
		}
		foreach (var suffix in new[] { "Name", "AlternateName", "Mobile", "Email" })
		{
			var actual = AdmissionStudentProfileMapping.Read(target, "Guardian." + suffix);
			if (Conflict(suffix == "AlternateName" ? "Name" : suffix, actual, Selected(suffix == "AlternateName" ? "Name" : suffix))) return false;
			independentIdentity |= !Empty(actual);
		}
		return Empty(target.GetProperty<string>(StudentFamilyContactSelection.GuardianRelationshipKey)) || independentIdentity;
	}

	internal static void Constrain(AdmissionFormDto definition, List<AdmissionFormValueDto> storedValues,
		AdmissionApplicationFormDto result, AdmissionApplicationFormValidator validator)
	{
		if (result.AutoFilledFieldCodes.Count == 0) return;
		var original = storedValues.ToDictionary(value => value.FieldCode, value => AdmissionApplicationValueConverter.Unwrap(value.ValueJson), StringComparer.OrdinalIgnoreCase);
		var visible = AdmissionEffectiveFormResolver.VisibleFieldCodes(definition, original, validator);
		var fields = AdmissionEffectiveFormResolver.MaterializeFields(definition);
		var sections = AdmissionEffectiveFormResolver.MaterializeSections(definition);
		foreach (var code in result.AutoFilledFieldCodes.ToArray())
		{
			var field = fields.Single(field => field.Code == code);
			var candidate = new Dictionary<string, string>(original, StringComparer.OrdinalIgnoreCase) { [code] = Read(result.Values, code) ?? string.Empty };
			var supported = field.FieldType is AdmissionFormFieldType.Text or AdmissionFormFieldType.Textarea or AdmissionFormFieldType.Email or
				AdmissionFormFieldType.Phone or AdmissionFormFieldType.Select or AdmissionFormFieldType.Radio;
			var dynamicChoice = field.FieldType is AdmissionFormFieldType.Select or AdmissionFormFieldType.Radio &&
				AdmissionFormMasterDataValidator.TrySource(field.OptionsJson, out _, out _);
			if (supported && !dynamicChoice && visible.Contains(code) && !validator.Validate(fields, sections, candidate).FieldErrors.ContainsKey(code)) continue;
			result.AutoFilledFieldCodes.Remove(code);
			result.Values.RemoveAll(value => string.Equals(value.FieldCode, code, StringComparison.OrdinalIgnoreCase));
			var old = storedValues.FirstOrDefault(value => string.Equals(value.FieldCode, code, StringComparison.OrdinalIgnoreCase));
			if (old != null) result.Values.Add(new() { FieldCode = old.FieldCode, ValueJson = old.ValueJson });
		}
	}

	private static string? Read(IEnumerable<AdmissionFormValueDto> values, string code) =>
		AdmissionApplicationValueConverter.Unwrap(values.FirstOrDefault(value => string.Equals(value.FieldCode, code, StringComparison.OrdinalIgnoreCase))?.ValueJson);
	private static bool Empty(string? value) => string.IsNullOrWhiteSpace(value) || value == "null";
	private static bool Different(string attribute, string? target, string? source)
	{
		if (Empty(target) || Empty(source)) return false;
		string Normalize(string value) => attribute == "Mobile" ? NormalizePhone(value) :
			string.Join(" ", value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToUpperInvariant();
		return Normalize(target!) != Normalize(source!);
	}
	private static string NormalizePhone(string value)
	{
		var digits = new string(value.Where(char.IsDigit).ToArray());
		return digits.Length == 12 && digits.StartsWith("91", StringComparison.Ordinal) ? digits[2..] : digits;
	}
}
