using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using ShikshaMitra.Common;
using ShikshaMitra.Students.Enrollments.Registrations;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.Records;

/// <summary>Patches only changed retained bindings into their existing owning section, preserving unrepresented properties.</summary>
[RemoteService(false)]
public class StudentRecordFormMapping(IStudentRepository students, StudentRecordPayloadService payloads, IRepository<StudentMedicalRecord, Guid> medicalRecords,
	IRepository<StudentFamilyDetail, Guid> familyDetails)
	: ApplicationService, ITransientDependency
{
	public async Task ApplyAsync(Guid studentId, List<StudentRecordRegistrationSourceDto> sources, StudentRecordCustomDetailsPayload input)
	{
		var sections = new Dictionary<string, JsonObject>();
		var changedPaths = new Dictionary<string, string>();
		foreach (var source in sources)
		{
			var proposed = input.Sources.Single(x => x.SubmissionId == source.SubmissionId).Values;
			var changed = StudentRecordFormProjection.Fields(source).Where(f => source.FormEditableFieldCodes.Contains(f.Code) &&
				!StudentRecordCustomDetailsService.Equal(source.CurrentValues.GetValueOrDefault(f.Code), proposed[f.Code])).ToList();
			foreach (var field in changed)
			{
				var target = field.BindingType == AdmissionFormBindingType.System ? StudentRecordFormProjection.Target(field.BindingKey) : null;
				if (target == null) continue;
				if (!sections.TryGetValue(target.Value.Section, out var section))
					sections[target.Value.Section] = section = target.Value.Section == "general"
						? await CaptureGeneralAsync(studentId) : JsonNode.Parse(await payloads.CaptureAsync(studentId, target.Value.Section))!.AsObject();
				if (target.Value.Section == "family" && !changedPaths.Keys.Any(x => x.StartsWith("family:")))
				{
					var family = await familyDetails.FindAsync(x => x.Id == studentId && x.TenantId == CurrentTenant.Id);
					section["siblingStudentIds"] = JsonSerializer.SerializeToNode(family?.SiblingStudentIds);
				}
				var value = field.BindingKey is "Guardian.FirstName" or "Guardian.LastName"
					? JsonValue.Create(string.Join(' ', StudentRecordFormProjection.Fields(source).Where(f => f.BindingKey is "Guardian.FirstName" or "Guardian.LastName")
						.OrderBy(f => f.BindingKey == "Guardian.FirstName" ? 0 : 1).Select(f => StudentRecordCustomDetailsService.Unwrap(proposed[f.Code])).Where(v => !string.IsNullOrWhiteSpace(v))))
					: ConvertValue(field, proposed[field.Code]);
				var path = target.Value.Section + ":" + target.Value.Path;
				var serialized = value?.ToJsonString() ?? "null";
				if (changedPaths.TryGetValue(path, out var previous) && previous != serialized)
					throw new BusinessException("Students:Records:ConflictingCustomValues");
				changedPaths[path] = serialized;
				AdmissionStudentProfileReviewService.Set(section, target.Value.Path, value);
			}
		}
		var normalized = new Dictionary<string, string>();
		foreach (var item in sections) normalized[item.Key] = await payloads.ValidateAndNormalizeAsync(item.Key, JsonSerializer.SerializeToElement(item.Value), studentId);
		foreach (var item in normalized)
			if (item.Key == "general") await ApplyGeneralAsync(studentId, item.Value, changedPaths.Keys.Where(x => x.StartsWith("general:")).Select(x => x[8..]).ToHashSet());
			else if (item.Key == "medical") await ApplyMedicalAsync(studentId, item.Value);
			else if (item.Key == "additional") await ApplyAdditionalAsync(studentId, item.Value, changedPaths.Keys.Where(x => x.StartsWith("additional:")).Select(x => x[11..]).ToHashSet());
			else await payloads.ApplyAsync(studentId, item.Key, item.Value);
	}

	private async Task<JsonObject> CaptureGeneralAsync(Guid studentId)
	{
		var student = await students.GetRegistrationAggregateAsync(studentId) ?? throw new BusinessException("Students:Records:EnrollmentMissing");
		var detail = student.AdditionalDetail; var contact = student.ContactDetail;
		return JsonSerializer.SerializeToNode(new UpdateGeneralInfoDto
		{
			FormNumber = detail?.RegistrationFormNo ?? "", FirstName = student.FirstName ?? "", MiddleName = student.MiddleName, LastName = student.LastName ?? "",
			DateOfBirth = detail?.DateOfBirth ?? default, Gender = detail?.Gender ?? default, ProfilePictureUrl = detail?.ProfilePictureUrl,
			PrimaryContactNo = contact?.PrimaryContactNo ?? "", SecondaryContactNo = contact?.SecondaryContactNo, Email = contact?.Email,
			AadharNo = detail?.AdharNo, ApparId = detail?.ApparId, PanNo = detail?.PanNo, PennNo = detail?.PennNo,
			Religion = detail?.Religion, FamilyCast = detail?.FamilyCast ?? "", CastCategory = detail?.CastCategory ?? default,
			Nationality = detail?.Nationality, BirthPlace = detail?.BirthPlace, MotherTongue = detail?.MotherTongue, InquiryPreferenceId = detail?.InquiryPreferenceId,
			Address1 = contact?.Address1, Address2 = contact?.Address2, Address3 = contact?.Address3, City = contact?.City ?? "",
			State = contact?.State ?? new State("", ""), Country = contact?.Country ?? new Country("", ""), Pincode = contact?.Pincode,
			PreviousSchool = detail?.PreviousSchool, LastClassPassed = detail?.LastClassPassed,
			AcademicMediumId = student.AcademicMediumId, StandardId = student.StandardId, DivisionId = student.DivisionId
		}, StudentRecordPayloadService.JsonOptions)!.AsObject();
	}
	private async Task ApplyGeneralAsync(Guid studentId, string json, HashSet<string> paths)
	{
		var input = JsonSerializer.Deserialize<UpdateGeneralInfoDto>(json, StudentRecordPayloadService.JsonOptions)!;
		var student = await students.GetRegistrationAggregateAsync(studentId) ?? throw new BusinessException("Students:Records:EnrollmentMissing");
		if (paths.Overlaps(["firstName", "middleName", "lastName"])) student.UpdateGeneralInformation(input.FirstName, input.MiddleName, input.LastName);
		var detail = student.AdditionalDetail;
		if (detail != null && paths.Overlaps(["dateOfBirth", "gender", "religion", "familyCast", "castCategory", "nationality", "birthPlace", "motherTongue", "previousSchool", "lastClassPassed"]))
		{
			var date = paths.Contains("dateOfBirth") ? input.DateOfBirth : detail.DateOfBirth;
			var gender = paths.Contains("gender") ? input.Gender : detail.Gender;
			if (!date.HasValue || !gender.HasValue) throw new BusinessException("Students:Records:InvalidCustomValues");
			detail.UpdateGeneralInformation(date.Value, gender.Value, detail.ProfilePictureUrl, detail.AdharNo, detail.ApparId, detail.PanNo, detail.PennNo,
				paths.Contains("religion") ? input.Religion : detail.Religion, paths.Contains("familyCast") ? input.FamilyCast : detail.FamilyCast,
				paths.Contains("castCategory") ? input.CastCategory : detail.CastCategory, paths.Contains("nationality") ? input.Nationality : detail.Nationality,
				paths.Contains("birthPlace") ? input.BirthPlace : detail.BirthPlace, paths.Contains("motherTongue") ? input.MotherTongue : detail.MotherTongue,
				paths.Contains("previousSchool") ? input.PreviousSchool : detail.PreviousSchool, paths.Contains("lastClassPassed") ? input.LastClassPassed : detail.LastClassPassed,
				detail.StudentRegistrationState, detail.InquiryPreferenceId, detail.RegistrationFormNo ?? "");
		}
		if (paths.Overlaps(["primaryContactNo", "secondaryContactNo", "email", "address1", "address2", "address3", "city", "state", "country", "pincode"]))
			student.ApplyVerifiedRegistrationContact(input.PrimaryContactNo, input.SecondaryContactNo, input.Email, input.Address1, input.Address2,
				input.Address3, input.City, input.State, input.Country, input.Pincode);
		await students.UpdateAsync(student, autoSave: true);
	}
	private async Task ApplyMedicalAsync(Guid studentId, string json)
	{
		var input = JsonSerializer.Deserialize<UpdateMedicalRecordsDto>(json, StudentRecordPayloadService.JsonOptions)!;
		var medical = await medicalRecords.FindAsync(studentId);
		var isNew = medical == null; medical ??= new StudentMedicalRecord(studentId, CurrentTenant.Id);
		medical.UpdateMedicalRecords(input.WeightKg, input.HeightCm, input.BodyMassIndex, input.PulseRate, input.Hemoglobin, input.Allergies,
			input.ChildImmunization, input.CovidVaccinationDoses, input.ImmunizationRemarks, input.BloodGroup, input.ChronicConditions, input.CurrentMedications);
		if (isNew) await medicalRecords.InsertAsync(medical, autoSave: true); else await medicalRecords.UpdateAsync(medical, autoSave: true);
	}
	private async Task ApplyAdditionalAsync(Guid studentId, string json, HashSet<string> paths)
	{
		var input = JsonSerializer.Deserialize<UpdateAdditionalDetailsDto>(json, StudentRecordPayloadService.JsonOptions)!;
		var student = await students.GetRegistrationAggregateAsync(studentId) ?? throw new BusinessException("Students:Records:EnrollmentMissing");
		var detail = student.AdditionalDetail ?? throw new BusinessException("Students:Records:InvalidCustomValues");
		detail.UpdateAdditionalDetails(paths.Contains("isBPLStudent") ? input.IsBPLStudent : detail.IsBPLStudent, input.BPLCardNo, input.SecondLanguage,
			paths.Contains("haveDisability") ? input.HaveDisability : detail.HaveDisability, input.DisabilityDetails,
			input.EmergencyRelativeName, input.EmergencyRelation, input.EmergencyContactNo,
			paths.Contains("isSingleChild") ? input.IsSingleChild : detail.IsSingleChild,
			paths.Contains("isSingleParentChild") ? input.IsSingleParentChild : detail.IsSingleParentChild, detail.Category, detail.IsLibraryMember, detail.IsTransportMember, retainDisabilityDetails: true);
		await students.UpdateAsync(student, autoSave: true);
	}
	private static JsonNode? ConvertValue(AdmissionFormFieldDto field, JsonElement value)
	{
		var raw = StudentRecordCustomDetailsService.Unwrap(value);
		if (field.BindingKey is "Address.Country" or "PermanentAddress.Country") return JsonSerializer.SerializeToNode(AdmissionStudentProfileMapping.CountryValue(raw), StudentRecordPayloadService.JsonOptions);
		if (field.BindingKey is "Address.State" or "PermanentAddress.State") return JsonSerializer.SerializeToNode(AdmissionStudentProfileMapping.StateValue(raw), StudentRecordPayloadService.JsonOptions);
		if (field.BindingKey is "Address.City" or "PermanentAddress.City") return JsonValue.Create(AdmissionStudentProfileMapping.LocationName(raw));
		if (string.IsNullOrWhiteSpace(raw)) return null;
		return field.FieldType switch
		{
			AdmissionFormFieldType.Number => JsonValue.Create(decimal.Parse(raw, CultureInfo.InvariantCulture)),
			AdmissionFormFieldType.Checkbox => JsonValue.Create(bool.Parse(raw)),
			AdmissionFormFieldType.Date => JsonValue.Create(DateTimeOffset.Parse(raw, CultureInfo.InvariantCulture)),
			AdmissionFormFieldType.MultiSelect => JsonNode.Parse(raw), _ => JsonValue.Create(raw)
		};
	}
}
