using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using ShikshaMitra.Students.Records;
using ShikshaMitra.Constants.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

/// <summary>Creates section change requests from accepted answers, preserving every property
/// not supplied by admissions. Student Records remains responsible for approvals and application.</summary>
[RemoteService(false)]
public class AdmissionStudentProfileReviewService(StudentRecordChangeAppService changes,
	StudentRecordPhotoAppService photos) : ApplicationService, ITransientDependency
{
	private static readonly IReadOnlyDictionary<string, (string Section, string Path)> Targets = new Dictionary<string, (string, string)>(StringComparer.OrdinalIgnoreCase)
	{
		["Identity.PanNumber"] = ("general", "panNo"), ["Applicant.Nationality"] = ("general", "nationality"),
		["Applicant.PlaceOfBirth"] = ("general", "birthPlace"), ["Applicant.Religion"] = ("general", "religion"),
		["Applicant.MotherTongue"] = ("general", "motherTongue"), ["Applicant.Caste"] = ("general", "familyCast"),
		["Applicant.SocialCategory"] = ("general", "castCategory"), ["Applicant.Email"] = ("general", "email"),
		["Contact.SecondaryPhone"] = ("general", "secondaryContactNo"), ["Address.AddressLine3"] = ("general", "address3"),
		["Family.IsSingleParentChild"] = ("additional", "isSingleParentChild"), ["Family.IsSingleChild"] = ("additional", "isSingleChild"),
		["Eligibility.IsBPL"] = ("additional", "isBPLStudent"), ["Eligibility.BPLCardNumber"] = ("additional", "bplCardNo"),
		["Academic.SecondLanguage"] = ("additional", "secondLanguage"), ["Applicant.HasDisability"] = ("additional", "haveDisability"),
		["Applicant.DisabilityDetails"] = ("additional", "disabilityDetails"),
		["Family.SiblingStudentIds"] = ("family", "siblingStudentIds"), ["Guardian.Occupation"] = ("family", "guardianDetails.occupation"),
		["PermanentAddress.Line1"] = ("communication", "permanentAddress.address1"), ["PermanentAddress.Line2"] = ("communication", "permanentAddress.address2"),
		["PermanentAddress.City"] = ("communication", "permanentAddress.city"), ["PermanentAddress.State"] = ("communication", "permanentAddress.state"),
		["PermanentAddress.Country"] = ("communication", "permanentAddress.country"), ["PermanentAddress.PostalCode"] = ("communication", "permanentAddress.pincode"),
		["PermanentAddress.SameAsCurrent"] = ("communication", "isPermanentSameAsLocal"),
		["Emergency.ContactName"] = ("communication", "emergencyContactName"), ["Emergency.ContactPhone"] = ("communication", "emergencyContactPhone"),
		["Emergency.Relationship"] = ("communication", "emergencyContactRelation"),
		["Medical.BloodGroup"] = ("medical", "bloodGroup"), ["Medical.Allergies"] = ("medical", "allergies"),
		["Medical.MedicalConditions"] = ("medical", "chronicConditions"), ["Medical.CurrentMedication"] = ("medical", "currentMedications"),
		["Medical.HeightCm"] = ("medical", "heightCm"), ["Medical.WeightKg"] = ("medical", "weightKg"),
		["Medical.ImmunisationSummary"] = ("medical", "childImmunization"),
		["PreviousSchool.Name"] = ("previous-school", "schoolName"), ["PreviousSchool.Address"] = ("previous-school", "schoolAddress"),
		["PreviousSchool.Board"] = ("previous-school", "board"), ["PreviousSchool.Medium"] = ("previous-school", "medium"),
		["PreviousSchool.LastClass"] = ("previous-school", "lastClassAttended"), ["PreviousSchool.TCNumber"] = ("previous-school", "tcNumber"),
		["PreviousSchool.IndexNumber"] = ("previous-school", "schoolIndexNumber"), ["PreviousSchool.DateOfLeaving"] = ("previous-school", "dateOfLeaving"),
		["PreviousSchool.Percentage"] = ("previous-school", "percentageObtained"), ["PreviousSchool.AcademicYear"] = ("previous-school", "academicYear"),
		["PreviousSchool.ReasonForLeaving"] = ("previous-school", "reasonForLeaving")
	};

	internal async Task SubmitAsync(AdmissionApplication application, AdmissionApplicant applicant, Student student)
	{
		await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.Students.Records_Changes.View);
		await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.Students.Records_Changes.Create);
		var answers = AdmissionAdditionalStudentFieldCatalog.Metadata.Values.Select(field => new
		{
			Field = field, Value = AdmissionStudentProfileMapping.Read(applicant, field.Key), Target = Target(field.Key)
		}).Where(item => !string.IsNullOrWhiteSpace(item.Value) && item.Target != null).ToList();
		foreach (var group in answers.GroupBy(answer => answer.Target!.Value.Section))
		{
			var snapshot = await changes.GetSnapshotAsync(student.Id, group.Key);
			var proposed = JsonNode.Parse(snapshot.Data.GetRawText())!.AsObject();
			foreach (var answer in group)
				if (SupportsValue(answer.Field, answer.Value!)) Set(proposed, answer.Target!.Value.Path, ConvertValue(answer.Field, answer.Value!));
			if (StudentRecordPayloadService.Hash(snapshot.Data.GetRawText()) == StudentRecordPayloadService.Hash(proposed.ToJsonString())) continue;
			await changes.CreateAdmissionReviewAsync(new()
			{
				StudentId = student.Id, Section = group.Key, ClientKey = $"admission-{application.Id:N}-{group.Key}",
				Reason = $"Admission {application.ApplicationNumber}: reviewed captured student details.", ExpectedSnapshot = snapshot.Snapshot,
				Payload = JsonSerializer.SerializeToElement(proposed)
			});
		}
		var photo = AdmissionStudentProfileMapping.Read(applicant, "Applicant.ProfilePhoto");
		if (!string.IsNullOrWhiteSpace(photo))
		{
			var content = AdmissionStudentPhoto.Decode(photo);
			var snapshot = await changes.GetSnapshotAsync(student.Id, "photo");
			var draft = await photos.UploadForAdmissionReviewAsync(student.Id, new()
			{
				FileName = "photo" + content.Extension, Content = content.Content,
				ContentType = content.Extension == ".png" ? "image/png" : "image/jpeg"
			});
			await changes.CreateAdmissionReviewAsync(new()
			{
				StudentId = student.Id, Section = "photo", ClientKey = $"admission-{application.Id:N}-photo",
				Reason = $"Admission {application.ApplicationNumber}: reviewed student photo.", ExpectedSnapshot = snapshot.Snapshot,
				Payload = JsonSerializer.SerializeToElement(new StudentRecordPhotoPayload { PhotoDraftId = draft.Id })
			});
		}
	}

	internal static (string Section, string Path)? Target(string key)
	{
		if (Targets.TryGetValue(key, out var target)) return target;
		var parts = key.Split('.');
		if (parts.Length != 2 || parts[0] is not ("Father" or "Mother")) return null;
		var name = parts[1] switch { "Name" => "fullName", "Mobile" => "contactNo", "Email" => "email",
			"Occupation" => "occupation", "Qualification" => "qualification", "Designation" => "designation", "Employer" => "organization", _ => null };
		return name == null ? null : ("family", parts[0].ToLowerInvariant() + "Details." + name);
	}
	private static JsonNode? ConvertValue(AdmissionFormSystemFieldMetadataDto field, string value)
	{
		if (field.Key == "PermanentAddress.Country") return JsonSerializer.SerializeToNode(AdmissionStudentProfileMapping.CountryValue(value), StudentRecordPayloadService.JsonOptions);
		if (field.Key == "PermanentAddress.State") return JsonSerializer.SerializeToNode(AdmissionStudentProfileMapping.StateValue(value), StudentRecordPayloadService.JsonOptions);
		if (field.Key == "PermanentAddress.City") return JsonValue.Create(AdmissionStudentProfileMapping.LocationName(value));
		return field.FieldType switch
		{
			AdmissionFormFieldType.Checkbox => JsonValue.Create(bool.Parse(value)),
			AdmissionFormFieldType.Number => JsonValue.Create(decimal.Parse(value, CultureInfo.InvariantCulture)),
			AdmissionFormFieldType.Date => JsonValue.Create(DateTimeOffset.Parse(value, CultureInfo.InvariantCulture)),
			AdmissionFormFieldType.MultiSelect => JsonNode.Parse(value), _ => JsonValue.Create(value)
		};
	}
	internal static bool SupportsValue(AdmissionFormSystemFieldMetadataDto field, string value)
	{
		if (field.Key == "Applicant.Religion") return Enum.TryParse<ReligionEnum>(value, true, out var religion) && Enum.IsDefined(religion);
		if (field.Key == "Applicant.MotherTongue") return Enum.TryParse<MotherTongueEnum>(value, true, out var tongue) && Enum.IsDefined(tongue);
		if (field.Key == "Applicant.SocialCategory") return Enum.TryParse<CastCategoryEnum>(value, true, out var category) && Enum.IsDefined(category);
		return field.FieldType switch
		{
			AdmissionFormFieldType.Checkbox => bool.TryParse(value, out _),
			AdmissionFormFieldType.Number => decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out _),
			AdmissionFormFieldType.Date => DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out _), _ => true
		};
	}
	internal static void Set(JsonObject json, string path, JsonNode? value)
	{
		var parts = path.Split('.');
		for (var index = 0; index < parts.Length - 1; index++)
		{
			if (json[parts[index]] is not JsonObject nested) json[parts[index]] = nested = new JsonObject();
			json = nested;
		}
		json[parts[^1]] = value;
	}
}
