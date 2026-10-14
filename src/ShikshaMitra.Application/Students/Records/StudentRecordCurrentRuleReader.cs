using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.Records;

/// <summary>Condition dependencies use their current canonical section; submitted evidence is never changed.</summary>
[Volo.Abp.RemoteService(false)]
public class StudentRecordCurrentRuleReader(IStudentRepository students, IPermissionChecker permissions) : ApplicationService, ITransientDependency
{
	public async Task RefreshAsync(Guid studentId, List<StudentRecordRegistrationSourceDto> sources, bool allFields = false)
	{
		var values = new Dictionary<string, JsonObject>();
		var canReadCompliance = await permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Students.IdentityCompliance.View);
		foreach (var source in sources)
		{
			var referenced = allFields ? source.Definition!.Sections.SelectMany(x => x.Fields).Select(x => x.Code).ToHashSet(StringComparer.OrdinalIgnoreCase) : ReferencedFields(source);
			foreach (var field in source.Definition!.Sections.SelectMany(x => x.Fields)
				.Where(x => referenced.Contains(x.Code) && x.BindingType == AdmissionFormBindingType.System && (allFields || !source.CapturedFieldCodes.Contains(x.Code)) && (!allFields || !source.FormMaskedFieldCodes.Contains(x.Code))))
			{
				var target = Target(field.BindingKey ?? "");
				if (target == null || allFields && source.Definition!.Sections.Single(s => s.Id == field.SectionId).IsRepeatable)
				{
					// An atomic admission value (for example guardian first/last names) cannot be
					// reconstructed from a differently shaped current field without guessing.
					if (!allFields) source.CurrentValues[field.Code] = JsonSerializer.SerializeToElement<object?>(null); continue;
				}
				if (field.BindingKey is "Identity.AadhaarNumber" or "Identity.ApaarId" or "Identity.PenNumber" or "Identity.UdiseStudentCode" && !canReadCompliance)
				{
					source.CurrentValues[field.Code] = JsonSerializer.SerializeToElement<object?>(null); continue;
				}
				if (allFields && field.BindingKey == "Family.SiblingStudentIds")
				{
					var family = await LazyServiceProvider.LazyGetRequiredService<IRepository<StudentFamilyDetail, Guid>>().FindAsync(x => x.Id == studentId && x.TenantId == CurrentTenant.Id);
					source.CurrentValues[field.Code] = JsonSerializer.SerializeToElement(family?.SiblingStudentIds ?? []); continue;
				}
				if (!values.TryGetValue(target.Value.Section, out var section))
				{
					// Lazy lookup avoids a DI cycle: Capture's structured branches never call custom Read.
					section = target.Value.Section == "general" && !IsComplianceField(field.BindingKey)
						? await ReadBasicGeneralAsync(studentId)
						: JsonNode.Parse(await LazyServiceProvider.LazyGetRequiredService<StudentRecordPayloadService>().CaptureAsync(studentId, target.Value.Section))!.AsObject();
					values[target.Value.Section] = section;
				}
				if (target.Value.Section == "general" && IsComplianceField(field.BindingKey) && !section.ContainsKey("aadharNo"))
					section = JsonNode.Parse(await LazyServiceProvider.LazyGetRequiredService<StudentRecordPayloadService>().CaptureAsync(studentId, "general"))!.AsObject();
				JsonNode? node = section;
				foreach (var part in target.Value.Path.Split('.')) node = node is JsonObject obj ? obj[part] : null;
				source.CurrentValues[field.Code] = allFields && IsLocationField(field) && node is JsonObject
					? JsonSerializer.SerializeToElement(node.ToJsonString()) : AdmissionValue(field.BindingKey, node);
			}
		}
		if (!values.ContainsKey("additional") && !values.ContainsKey("medical")) return;
		var flags = await AsyncExecuter.FirstAsync((await students.GetQueryableWithoutAutoIncludesAsync()).Where(x => x.Id == studentId && x.TenantId == CurrentTenant.Id)
			.Select(x => new { Bpl = x.AdditionalDetail != null ? x.AdditionalDetail.IsBPLStudent : null,
				Disability = x.AdditionalDetail != null ? x.AdditionalDetail.HaveDisability : null,
				Single = x.AdditionalDetail != null ? x.AdditionalDetail.IsSingleChild : null,
				SingleParent = x.AdditionalDetail != null ? x.AdditionalDetail.IsSingleParentChild : null }));
		foreach (var source in sources)
			foreach (var field in source.Definition!.Sections.SelectMany(x => x.Fields).Where(x => !source.FormMaskedFieldCodes.Contains(x.Code) && (allFields || ReferencedFields(source).Contains(x.Code))))
			{
				bool? flag = field.BindingKey switch
				{ "Eligibility.IsBPL" => flags.Bpl, "Applicant.HasDisability" => flags.Disability,
					"Family.IsSingleChild" => flags.Single, "Family.IsSingleParentChild" => flags.SingleParent, _ => null };
				if (field.BindingKey is "Eligibility.IsBPL" or "Applicant.HasDisability" or "Family.IsSingleChild" or "Family.IsSingleParentChild")
					source.CurrentValues[field.Code] = JsonSerializer.SerializeToElement(flag);
			}
	}
	private static bool IsComplianceField(string? binding) => binding is "Identity.AadhaarNumber" or "Identity.ApaarId" or "Identity.PenNumber" or "Identity.UdiseStudentCode";
	private static bool IsLocationField(AdmissionFormFieldDto field)
	{
		try
		{
			using var config = JsonDocument.Parse(field.ValidationJson ?? "{}");
			return config.RootElement.TryGetProperty("format", out var format) && format.ValueKind == JsonValueKind.String && format.GetString() is "country" or "state";
		}
		catch (JsonException) { return false; }
	}
	internal async Task<JsonObject> ReadBasicGeneralAsync(Guid studentId)
	{
		var data = await AsyncExecuter.FirstAsync((await students.GetQueryableWithoutAutoIncludesAsync()).Where(x => x.TenantId == CurrentTenant.Id && x.Id == studentId)
			.Select(x => new
			{
				x.FirstName, x.MiddleName, x.LastName, DateOfBirth = x.AdditionalDetail != null ? x.AdditionalDetail.DateOfBirth : null,
				Gender = x.AdditionalDetail != null ? x.AdditionalDetail.Gender : null, Religion = x.AdditionalDetail != null ? x.AdditionalDetail.Religion : null,
				MotherTongue = x.AdditionalDetail != null ? x.AdditionalDetail.MotherTongue : null, CastCategory = x.AdditionalDetail != null ? x.AdditionalDetail.CastCategory : null,
				FamilyCast = x.AdditionalDetail != null ? x.AdditionalDetail.FamilyCast : null, Nationality = x.AdditionalDetail != null ? x.AdditionalDetail.Nationality : null,
				BirthPlace = x.AdditionalDetail != null ? x.AdditionalDetail.BirthPlace : null, PanNo = x.AdditionalDetail != null ? x.AdditionalDetail.PanNo : null,
				Email = x.ContactDetail != null ? x.ContactDetail.Email : null, PrimaryContactNo = x.ContactDetail != null ? x.ContactDetail.PrimaryContactNo : null, SecondaryContactNo = x.ContactDetail != null ? x.ContactDetail.SecondaryContactNo : null,
				Address1 = x.ContactDetail != null ? x.ContactDetail.Address1 : null, Address2 = x.ContactDetail != null ? x.ContactDetail.Address2 : null,
				Address3 = x.ContactDetail != null ? x.ContactDetail.Address3 : null, City = x.ContactDetail != null ? x.ContactDetail.City : null,
				State = x.ContactDetail != null ? x.ContactDetail.State : null, Country = x.ContactDetail != null ? x.ContactDetail.Country : null,
				Pincode = x.ContactDetail != null ? x.ContactDetail.Pincode : null
			}));
		return JsonSerializer.SerializeToNode(data, new JsonSerializerOptions(JsonSerializerDefaults.Web))!.AsObject();
	}
	internal static JsonElement AdmissionValue(string? bindingKey, JsonNode? node)
	{
		if (node is JsonValue value && value.TryGetValue<int>(out var number))
		{
			var enumType = bindingKey switch
			{
				"Applicant.Gender" => typeof(GenderEnum), "Applicant.Religion" => typeof(ReligionEnum),
				"Applicant.MotherTongue" => typeof(MotherTongueEnum), "Applicant.SocialCategory" => typeof(CastCategoryEnum), _ => null
			};
			if (enumType != null) return JsonSerializer.SerializeToElement(Enum.IsDefined(enumType, number) ? Enum.GetName(enumType, number) : null);
		}
		if (bindingKey is "Address.State" or "Address.Country" or "PermanentAddress.State" or "PermanentAddress.Country" && node is JsonObject location)
			return JsonSerializer.SerializeToElement(location["name"]);
		return JsonSerializer.SerializeToElement(node);
	}
	internal static HashSet<string> ReferencedFields(StudentRecordRegistrationSourceDto source)
	{
		var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (var rule in source.Definition?.Sections.SelectMany(section => new[] { section.VisibilityRuleJson, section.StepVisibilityRuleJson }
			.Concat(section.Fields.SelectMany(field => new[] { field.VisibilityRuleJson, field.RequiredRuleJson }))) ?? [])
		{
			if (string.IsNullOrWhiteSpace(rule)) continue;
			try { Add(JsonSerializer.Deserialize<JsonElement>(rule)); } catch (JsonException) { }
		}
		return result;
		void Add(JsonElement node)
		{
			if (node.ValueKind == JsonValueKind.Object)
			{
				if (node.TryGetProperty("field", out var field) && field.ValueKind == JsonValueKind.String) result.Add(field.GetString()!);
				foreach (var property in node.EnumerateObject()) Add(property.Value);
			}
			else if (node.ValueKind == JsonValueKind.Array) foreach (var child in node.EnumerateArray()) Add(child);
		}
	}
	internal static (string Section, string Path)? Target(string key) => AdmissionStudentProfileReviewService.Target(key) ?? key switch
	{
		"Applicant.FirstName" => ("general", "firstName"), "Applicant.MiddleName" => ("general", "middleName"),
		"Applicant.LastName" => ("general", "lastName"), "Applicant.DateOfBirth" => ("general", "dateOfBirth"), "Applicant.Gender" => ("general", "gender"),
		"Guardian.Mobile" => ("family", "guardianDetails.contactNo"), "Guardian.Email" => ("family", "guardianDetails.email"),
		"Identity.AadhaarNumber" => ("general", "aadharNo"), "Identity.ApaarId" => ("general", "apparId"),
		"Identity.PenNumber" => ("general", "pennNo"), "Identity.UdiseStudentCode" => ("general", "udiseStudentCode"),
		"Address.AddressLine1" => ("general", "address1"), "Address.AddressLine2" => ("general", "address2"),
		"Address.City" => ("general", "city"), "Address.State" => ("general", "state"), "Address.Country" => ("general", "country"),
		"Address.PostalCode" => ("general", "pincode"), _ => null
	};
}
