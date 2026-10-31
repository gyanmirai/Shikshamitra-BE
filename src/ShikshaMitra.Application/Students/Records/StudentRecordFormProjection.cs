using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using ShikshaMitra.Students.StudentRecords;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.Records;

/// <summary>Projects the retained form against canonical current storage without changing its original submission.</summary>
[Volo.Abp.RemoteService(false)]
public class StudentRecordFormProjection(StudentRecordCurrentRuleReader currentRules, StudentRecordsAccessService access,
	StudentRecordsPolicyResolver policies, StudentRecordRegistrationReader reader, IPermissionChecker permissions,
	IRepository<StudentRecordCustomProfile, Guid> profiles) : ApplicationService, ITransientDependency
{
	internal static IEnumerable<AdmissionFormFieldDto> Fields(StudentRecordRegistrationSourceDto source) =>
		source.Definition!.Sections.SelectMany(x => x.Fields).Where(x => x.FieldType != AdmissionFormFieldType.Static);
	internal static (string Section, string Path)? Target(string? key) => key is "Guardian.FirstName" or "Guardian.LastName"
		? ("family", "guardianDetails.fullName") : key == "Applicant.ProfilePhoto" ? ("photo", "photoUrl") : StudentRecordCurrentRuleReader.Target(key ?? "");
	internal static bool Protected(AdmissionFormFieldDto field) => field.BindingKey is "Applicant.ProfilePhoto" or
		"Identity.AadhaarNumber" or "Identity.ApaarId" or "Identity.PenNumber" or "Identity.UdiseStudentCode" or "Identity.PanNumber";
	public async Task<bool> IsAuditMaskedAsync(Guid studentId, string recordedJson)
	{
		var recorded = JsonSerializer.Deserialize<StudentRecordCustomDetailsPayload>(recordedJson, StudentRecordPayloadService.JsonOptions)!;
		var sources = await reader.GetAsync(studentId);
		await PopulateAsync(studentId, sources, false);
		foreach (var previous in recorded.Sources)
		{
			var source = sources.SingleOrDefault(x => x.SubmissionId == previous.SubmissionId);
			if (source == null) return true;
			if (source.FormMaskedFieldCodes.Any(code => previous.Values.TryGetValue(code, out var value) &&
				StudentRecordCustomDetailsService.HasValue(value) && StudentRecordCustomDetailsService.Unwrap(value) != "Restricted")) return true;
		}
		return false;
	}

	public async Task PopulateAsync(Guid studentId, List<StudentRecordRegistrationSourceDto> sources, bool canEdit)
	{
		var record = await access.EnsureReadableAsync(studentId);
		var roles = await access.GetDatabaseRoleNamesAsync();
		var compliance = await permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Students.IdentityCompliance.View);
		var baselineReadable = (await reader.GetSourceVisibilityAsync(studentId)).BaselineReadable;
		var customMasked = !baselineReadable || StudentRecordVisibility.IsMasked(record.Policy, "custom-details", roles);
		foreach (var year in await reader.GetSourceYearIdsAsync(studentId))
			customMasked |= StudentRecordVisibility.IsMasked((await policies.ResolveAsync(year)).Policy, "custom-details", roles);
		await PopulateCoreAsync(studentId, sources, canEdit, compliance, record.Access == "Full", record.Policy, roles, customMasked, null);
	}
	internal async Task PopulateForPortalAsync(Guid studentId, List<StudentRecordRegistrationSourceDto> sources, Func<string, Task<JsonObject>> readSection)
	{
		await LazyServiceProvider.LazyGetRequiredService<ShikshaMitra.Students.Portal.StudentPortalAccessService>().ResolveStudentAsync(studentId);
		var student = await LazyServiceProvider.LazyGetRequiredService<IStudentRepository>().GetAsync(studentId);
		var policy = (await policies.ResolveAsync(student.AcademicYearId)).Policy;
		var roles = await access.GetDatabaseRoleNamesAsync();
		var masked = StudentRecordVisibility.IsMasked(policy, "custom-details", roles);
		foreach (var year in sources.Select(s => s.AcademicYearId).Distinct())
			masked |= StudentRecordVisibility.IsMasked((await policies.ResolveAsync(year)).Policy, "custom-details", roles);
		await PopulateCoreAsync(studentId, sources, false,
			await permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Students.IdentityCompliance.View), true, policy, roles, masked, readSection);
	}
	private async Task PopulateCoreAsync(Guid studentId, List<StudentRecordRegistrationSourceDto> sources, bool canEdit, bool compliance,
		bool fullAccess, StudentRecordsPolicy recordPolicy, List<string> roles, bool customMasked, Func<string, Task<JsonObject>>? readSection)
	{
		foreach (var source in sources)
		{
			var policy = (await policies.ResolveAsync(source.AcademicYearId)).Policy;
			source.FormEditableFieldCodes.Clear(); source.FormMaskedFieldCodes.Clear();
			foreach (var field in Fields(source))
			{
				var target = field.BindingType == AdmissionFormBindingType.System ? Target(field.BindingKey) : null;
				var masked = !fullAccess || (target.HasValue
					? StudentRecordVisibility.IsMasked(recordPolicy, target.Value.Section, roles) || StudentRecordVisibility.IsMasked(policy, target.Value.Section, roles)
					: customMasked) || (!compliance && field.BindingKey is "Identity.AadhaarNumber" or "Identity.ApaarId" or "Identity.PenNumber" or "Identity.UdiseStudentCode" or "Identity.PanNumber");
				if (masked) { source.FormMaskedFieldCodes.Add(field.Code); continue; }
				var repeatableMapped = target.HasValue && source.Definition!.Sections.Single(s => s.Id == field.SectionId).IsRepeatable;
				if (canEdit && !field.ReadOnly && !Protected(field) && !repeatableMapped && StudentRecordCustomDetailsService.CanEditField(field) &&
					(target.HasValue || source.EditableFieldCodes.Contains(field.Code)))
					source.FormEditableFieldCodes.Add(field.Code);
			}
		}
		if (readSection == null) await currentRules.RefreshAsync(studentId, sources, allFields: true);
		else await currentRules.RefreshForPortalAsync(studentId, sources, readSection);
		var stored = await profiles.FindAsync(x => x.TenantId == CurrentTenant.Id && x.StudentId == studentId);
		var atoms = stored == null ? new Dictionary<string, JsonElement>() : JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(stored.ValuesJson) ?? [];
		JsonObject? family = null;
		foreach (var source in sources)
		{
			// Differences carry the same submitted/current answers and must follow the form's field redaction.
			source.Differences.RemoveAll(difference => source.FormMaskedFieldCodes.Contains(difference.FieldCode));
			var names = Fields(source).Where(f => !source.FormMaskedFieldCodes.Contains(f.Code) && f.BindingKey is "Guardian.FirstName" or "Guardian.LastName").ToList();
			if (names.Count > 0)
			{
				family ??= readSection != null ? await readSection("family") : JsonNode.Parse(await LazyServiceProvider.LazyGetRequiredService<StudentRecordPayloadService>().CaptureAsync(studentId, "family"))!.AsObject();
				var name = family["guardianDetails"]?["fullName"]?.GetValue<string>() ?? "";
				foreach (var field in names) source.CurrentValues[field.Code] = atoms.GetValueOrDefault(StudentRecordCustomDetailsService.Identity(source, field), source.OriginalValues.GetValueOrDefault(field.Code, JsonSerializer.SerializeToElement("")));
				var combined = string.Join(' ', names.OrderBy(f => f.BindingKey == "Guardian.FirstName" ? 0 : 1).Select(f => StudentRecordCustomDetailsService.Unwrap(source.CurrentValues[f.Code])).Where(v => !string.IsNullOrWhiteSpace(v)));
				if (combined != name)
					foreach (var field in names) source.CurrentValues[field.Code] = JsonSerializer.SerializeToElement(field.BindingKey == "Guardian.FirstName" ? name : "");
			}
			foreach (var field in Fields(source))
			{
				// A retained question may never have had a submitted answer; the current form still includes it.
				source.CurrentValues.TryAdd(field.Code, JsonSerializer.SerializeToElement<object?>(null));
				if (source.FormMaskedFieldCodes.Contains(field.Code))
				{
					field.DefaultValue = null;
					source.CurrentValues[field.Code] = JsonSerializer.SerializeToElement("Restricted");
					source.OriginalValues[field.Code] = JsonSerializer.SerializeToElement("Restricted");
				}
				else if (field.FieldType == AdmissionFormFieldType.File || field.BindingKey == "Applicant.ProfilePhoto" || IsPhotoFormat(field))
				{
					field.DefaultValue = null;
					foreach (var values in new[] { source.CurrentValues, source.OriginalValues })
						if (values.TryGetValue(field.Code, out var value)) values[field.Code] = BinarySummary(value);
					foreach (var difference in source.Differences.Where(x => x.FieldCode == field.Code))
					{
						if (difference.CurrentValue.HasValue) difference.CurrentValue = BinarySummary(difference.CurrentValue.Value);
						if (difference.SubmittedValue.HasValue) difference.SubmittedValue = BinarySummary(difference.SubmittedValue.Value);
					}
				}
				else if (field.BindingKey == "Identity.AadhaarNumber")
					foreach (var values in new[] { source.CurrentValues, source.OriginalValues })
						if (values.TryGetValue(field.Code, out var value))
						{ var raw = StudentRecordCustomDetailsService.Unwrap(value); values[field.Code] = JsonSerializer.SerializeToElement(raw.Length >= 4 ? "********" + raw[^4..] : "Restricted"); }
			}
		}
	}
	private static JsonElement BinarySummary(JsonElement value) => value.ValueKind == JsonValueKind.Array
		? JsonSerializer.SerializeToElement(value.EnumerateArray().Select(BinarySummary).ToArray())
		: JsonSerializer.SerializeToElement(StudentRecordCustomDetailsService.HasValue(value) ? "Attachment recorded" : null);
	private static bool IsPhotoFormat(AdmissionFormFieldDto field)
	{
		try
		{
			using var config = JsonDocument.Parse(field.ValidationJson ?? "{}");
			return config.RootElement.TryGetProperty("format", out var format) && format.ValueKind == JsonValueKind.String && format.GetString() == "photo";
		}
		catch (JsonException) { return false; }
	}
}
