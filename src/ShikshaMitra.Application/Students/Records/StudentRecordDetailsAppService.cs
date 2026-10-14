using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using ShikshaMitra.Students.StudentRecords;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.Records;

[RemoteService(false)]
[Authorize(ShikshaMitraSubmodulePermissions.Students.Records_StudentList.View)]
public class StudentRecordDetailsAppService(StudentRecordsAccessService access, StudentRecordCustomDetailsService customDetails,
	StudentRecordPayloadService payloads, IStudentRepository students, IPermissionChecker permissions,
	StudentRecordsPolicyResolver policies, StudentRecordFieldOptionsService fieldOptions, StudentRecordFormProjection formProjection) : ApplicationService, IStudentRecordDetailsAppService
{
	private static readonly string[] Sections = ["general", "family", "communication", "additional", "medical", "bank", "previous-school"];
	public async Task<StudentRecordDetailsDto> GetAsync(Guid studentId, Guid? academicYearId = null)
	{
		await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.Students.Records_StudentList.View);
		var record = await access.EnsureReadableAsync(studentId);
		var roles = await access.GetDatabaseRoleNamesAsync();
		var canCreate = await permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Students.Records_Changes.Create) &&
			await permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Students.Records_Changes.View);
		var canReadCompliance = await permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Students.IdentityCompliance.View);
		var result = new StudentRecordDetailsDto { StudentId = studentId, RecordsAccess = record.Access };
		if (record.Access != "Full") return result;
		var sources = await customDetails.ReadAsync(studentId);
		var formSources = JsonSerializer.Deserialize<List<StudentRecordRegistrationSourceDto>>(JsonSerializer.Serialize(sources, StudentRecordPayloadService.JsonOptions), StudentRecordPayloadService.JsonOptions)!;
		await formProjection.PopulateAsync(studentId, formSources, canCreate);
		foreach (var source in formSources) await fieldOptions.PopulateAsync(source, canCreate, fullForm: true);
		// The retained profile schema survives promotion; year context only filters the legacy journey evidence below.
		result.FormSources = formSources;
		var knownFlags = await AsyncExecuter.FirstAsync((await students.GetQueryableWithoutAutoIncludesAsync())
			.Where(x => x.Id == studentId && x.TenantId == CurrentTenant.Id).Select(x => new
			{
				Additional = x.AdditionalDetail != null && (x.AdditionalDetail.IsBPLStudent.HasValue || x.AdditionalDetail.IsSingleChild.HasValue ||
					x.AdditionalDetail.IsSingleParentChild.HasValue || x.AdditionalDetail.HaveDisability.HasValue),
				Medical = x.AdditionalDetail != null && x.AdditionalDetail.HaveDisability.HasValue,
				IsBplStudent = x.AdditionalDetail != null ? x.AdditionalDetail.IsBPLStudent : null,
				HaveDisability = x.AdditionalDetail != null ? x.AdditionalDetail.HaveDisability : null,
				IsSingleChild = x.AdditionalDetail != null ? x.AdditionalDetail.IsSingleChild : null,
				IsSingleParentChild = x.AdditionalDetail != null ? x.AdditionalDetail.IsSingleParentChild : null,
				IsLibraryMember = x.AdditionalDetail != null ? x.AdditionalDetail.IsLibraryMember : null,
				IsTransportMember = x.AdditionalDetail != null ? x.AdditionalDetail.IsTransportMember : null
			}));
		foreach (var section in Sections)
		{
			var masked = StudentRecordVisibility.IsMasked(record.Policy, section, roles) || section == "general" && !canReadCompliance;
			var row = new StudentRecordStructuredSectionDto { Section = section, IsMasked = masked, CanEdit = !masked && canCreate };
			if (!masked)
			{
				var data = JsonNode.Parse(await payloads.CaptureAsync(studentId, section))!.AsObject();
				if (section is "additional" or "medical") data["haveDisability"] = JsonSerializer.SerializeToNode(knownFlags.HaveDisability);
				if (section == "additional")
				{
					data["isBPLStudent"] = JsonSerializer.SerializeToNode(knownFlags.IsBplStudent);
					data["isSingleChild"] = JsonSerializer.SerializeToNode(knownFlags.IsSingleChild);
					data["isSingleParentChild"] = JsonSerializer.SerializeToNode(knownFlags.IsSingleParentChild);
					data["isLibraryMember"] = JsonSerializer.SerializeToNode(knownFlags.IsLibraryMember);
					data["isTransportMember"] = JsonSerializer.SerializeToNode(knownFlags.IsTransportMember);
				}
				row.Data = JsonSerializer.SerializeToElement(data);
				row.HasData = HasMeaningfulValue(row.Data.Value) || section == "additional" && knownFlags.Additional || section == "medical" && knownFlags.Medical;
			}
			result.StructuredSections.Add(row);
		}
		result.CustomDetailsMasked = await customDetails.IsMaskedAsync(studentId);
		var canEditAny = false;
		var yearPolicies = new Dictionary<Guid, StudentRecordsPolicy>();
		foreach (var source in sources)
		{
			if (!yearPolicies.TryGetValue(source.AcademicYearId, out var sourcePolicy))
				yearPolicies[source.AcademicYearId] = sourcePolicy = (await policies.ResolveAsync(source.AcademicYearId)).Policy;
			var masked = result.CustomDetailsMasked || StudentRecordVisibility.IsMasked(sourcePolicy, "custom-details", roles);
			if (masked)
			{
				source.Definition = null; source.OriginalValues.Clear(); source.CurrentValues.Clear();
				source.Differences.Clear(); source.EditableFieldCodes.Clear(); source.CapturedFieldCodes.Clear();
			}
			else
			{
				RedactProtectedEvidence(source, canReadCompliance);
				foreach (var difference in source.Differences) difference.CanReview &= canCreate;
				canEditAny |= source.EditableFieldCodes.Count > 0;
				await fieldOptions.PopulateAsync(source, canCreate);
			}
		}
		result.CustomDetailsEditable = canCreate && !result.CustomDetailsMasked && canEditAny;
		result.RegistrationSources = sources.Where(x => !academicYearId.HasValue || x.AcademicYearId == academicYearId).ToList();
		return result;
	}
	public async Task<List<AdmissionFormOptionDto>> GetFieldOptionsAsync(Guid studentId, StudentRecordFieldOptionsInput input)
	{
		await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.Students.Records_StudentList.View);
		await access.EnsureReadableAsync(studentId);
		var sources = await customDetails.ReadAsync(studentId);
		await formProjection.PopulateAsync(studentId, sources, true);
		var source = sources.SingleOrDefault(x => x.SubmissionId == input.SubmissionId)
			?? throw new BusinessException("Students:Records:InvalidCustomSource");
		var field = source.Definition!.Sections.SelectMany(x => x.Fields).SingleOrDefault(x => x.Code == input.FieldCode)
			?? throw new BusinessException("Students:Records:InvalidCustomField");
		if (source.FormMaskedFieldCodes.Contains(field.Code)) throw new BusinessException("Students:Records:RestrictedFields");
		if (!input.SelectedOnly)
		{
			await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.Students.Records_Changes.View);
			await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.Students.Records_Changes.Create);
			if (!source.FormEditableFieldCodes.Contains(field.Code)) throw new BusinessException("Students:Records:OwnedFieldChange");
		}
		return await fieldOptions.QueryAsync(source, field, input);
	}

	/// <summary>Exclude synthetic defaults and technical metadata from section availability.</summary>
	internal static bool HasMeaningfulValue(JsonElement value) => value.ValueKind switch
	{
		JsonValueKind.String => !string.IsNullOrWhiteSpace(value.GetString()),
		JsonValueKind.Number => true,
		JsonValueKind.True => true,
		JsonValueKind.Array => value.EnumerateArray().Any(HasMeaningfulValue),
		JsonValueKind.Object => value.EnumerateObject().Where(x => !TechnicalField(x.Name)).Any(x => HasMeaningfulValue(x.Value)),
		_ => false
	};
	private static bool TechnicalField(string name) => name.EndsWith("Id", StringComparison.OrdinalIgnoreCase) ||
		name.EndsWith("Ids", StringComparison.OrdinalIgnoreCase) || name is "status" or "category" or "studentRegistrationState" or
		"rteStatus" or "scholarshipStatus" or "aadhaarVerificationStatus" or "apaarVerificationStatus" or "apaarConsentStatus" or "udiseSyncStatus";
	private static void RedactProtectedEvidence(StudentRecordRegistrationSourceDto source, bool canReadCompliance)
	{
		foreach (var field in source.Definition!.Sections.SelectMany(x => x.Fields))
		{
			if (field.BindingKey == "Applicant.ProfilePhoto")
			{
				source.OriginalValues.Remove(field.Code); source.CurrentValues.Remove(field.Code);
			}
			if (field.BindingKey == "Identity.AadhaarNumber")
				foreach (var values in new[] { source.OriginalValues, source.CurrentValues })
					if (values.TryGetValue(field.Code, out var original))
					{
						var raw = StudentRecordCustomDetailsService.Unwrap(original);
						values[field.Code] = JsonSerializer.SerializeToElement(canReadCompliance && raw.Length >= 4 ? "********" + raw[^4..] : "Restricted");
					}
			if (!canReadCompliance && field.BindingKey is "Identity.ApaarId" or "Identity.PenNumber" or "Identity.UdiseStudentCode")
				foreach (var values in new[] { source.OriginalValues, source.CurrentValues })
					if (values.ContainsKey(field.Code)) values[field.Code] = JsonSerializer.SerializeToElement("Restricted");
		}
	}
}
