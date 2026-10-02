using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using ShikshaMitra.Students.Enrollments.Registrations;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Validation;

namespace ShikshaMitra.Students.Records;

/// <summary>Reuses section contracts and validators; it does not create another student schema.</summary>
public class StudentRecordPayloadService(
	IStudentGeneralInfoAppService general, IStudentFamilyInfoAppService family,
	IStudentCommunicationInfoAppService communication, IStudentAdditionalDetailsAppService additional,
	IStudentMedicalRecordsAppService medical, IStudentPreviousSchoolDetailsAppService previous,
	IStudentBankDetailsAppService bank, IStudentRepository students, IObjectValidator validator,
	StudentRecordApplyContext context, StudentRecordPlacementService placement,
	IRepository<StudentRecordPhotoDraft, Guid> photoDrafts,
	IRepository<StudentRecordChange, Guid> changes) : ITransientDependency
{
	public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
	{
		UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
		Converters = { new JsonStringEnumConverter() }
	};
	private static readonly JsonSerializerOptions ValueJsonOptions = new(JsonSerializerDefaults.Web);
	public static readonly string[] Sections = ["general", "family", "communication", "additional", "medical", "bank", "previous-school", "placement", "status", "photo"];
	public static string ActionFor(string section) => section switch
	{
		"general" => "Identity", "family" => "Guardian", "communication" => "Contact",
		"additional" => "Additional", "medical" => "Medical", "bank" => "Bank",
		"previous-school" => "PreviousSchool", "placement" => "Placement", "status" => "Status", "photo" => "Photo",
		_ => throw new BusinessException("Students:Records:InvalidSection")
	};
	public async Task<string> CaptureAsync(Guid studentId, string section)
	{
		if (section == "photo")
		{
			var student = await students.GetAsync(studentId);
			return Canonical(JsonSerializer.Serialize(new { photoDraftId = (Guid?)null, currentBlobName = student.PhotoBlobName,
				photoUrl = student.PhotoBlobName == null ? null : $"/api/students/records/{studentId}/photo" }, ValueJsonOptions));
		}
		object value = section switch
		{
			"general" => await general.GetRegistrationGeneralDetailsByIdAsync(studentId),
			"family" => await family.GetFamilyInfoAsync(studentId),
			"communication" => await communication.GetCommunicationInfoAsync(studentId),
			"additional" => await additional.GetAdditionalDetailsAsync(studentId),
			"medical" => await medical.GetMedicalRecordsAsync(studentId),
			"bank" => await bank.GetBankDetailsAsync(studentId),
			"previous-school" => await previous.GetPreviousSchoolDetailsAsync(studentId),
			"placement" => await placement.GetAsync(studentId),
			"status" => new StudentRecordStatusPayload { Status = (await students.GetAsync(studentId)).Status },
			_ => throw new BusinessException("Students:Records:InvalidSection")
		};
		var node = JsonSerializer.SerializeToNode(value, ValueJsonOptions)!.AsObject();
		if (section == "general")
		{
			node["formNumber"] = node["formNo"]?.DeepClone() ?? JsonValue.Create(string.Empty);
			node["aadharNo"] = node["adharNo"]?.DeepClone();
			node["apparId"] = node["apparId"]?.DeepClone();
			var student = await students.GetRegistrationAggregateAsync(studentId);
			node["divisionId"] = JsonSerializer.SerializeToNode(student!.DivisionId);
			node["profilePictureUrl"] = JsonSerializer.SerializeToNode(student.AdditionalDetail?.ProfilePictureUrl);
		}
		var output = new JsonObject();
		foreach (var prop in PayloadType(section).GetProperties().OrderBy(x => x.Name, StringComparer.Ordinal))
		{
			var name = JsonNamingPolicy.CamelCase.ConvertName(prop.Name);
			output[name] = node[name]?.DeepClone();
		}
		return output.ToJsonString(JsonOptions);
	}
	public async Task<string> ValidateAndNormalizeAsync(string section, JsonElement payload)
	{
		if (payload.ValueKind != JsonValueKind.Object || payload.GetRawText().Length > 65536)
			throw new BusinessException("Students:Records:InvalidPayload");
		try
		{
			var input = JsonSerializer.Deserialize(payload.GetRawText(), PayloadType(section), JsonOptions)
				?? throw new BusinessException("Students:Records:InvalidPayload");
			await validator.ValidateAsync(input);
			return Canonical(JsonSerializer.Serialize(input, ValueJsonOptions));
		}
		catch (JsonException) { throw new BusinessException("Students:Records:InvalidPayload"); }
	}
	public Task ValidateImpactAsync(Guid studentId, string section, string json) => section == "placement" ? placement.ValidateImpactAsync(studentId, Deserialize<StudentRecordPlacementPayload>(json)) : Task.CompletedTask;
	public async Task ApplyAsync(Guid studentId, string section, string json, Guid? changeRequestId = null)
	{
		using var applying = context.Begin(studentId, section);
		switch (section)
		{
			case "general": await general.UpdateGeneralInfoAsync(studentId, Deserialize<UpdateGeneralInfoDto>(json)); break;
			case "family": await family.UpdateFamilyInfoAsync(studentId, Deserialize<UpdateFamilyInfoDto>(json)); break;
			case "communication": await communication.UpdateCommunicationInfoAsync(studentId, Deserialize<UpdateCommunicationInfoDto>(json)); break;
			case "additional": await additional.UpdateAdditionalDetailsAsync(studentId, Deserialize<UpdateAdditionalDetailsDto>(json)); break;
			case "medical": await medical.UpdateMedicalRecordsAsync(studentId, Deserialize<UpdateMedicalRecordsDto>(json)); break;
			case "bank": await bank.UpdateBankDetailsAsync(studentId, Deserialize<UpdateBankDetailsDto>(json)); break;
			case "previous-school": await previous.UpdatePreviousSchoolDetailsAsync(studentId, Deserialize<UpdatePreviousSchoolDetailsDto>(json)); break;
			case "placement": await placement.ApplyAsync(studentId, Deserialize<StudentRecordPlacementPayload>(json)); break;
			case "status": await placement.ApplyStatusAsync(studentId, Deserialize<StudentRecordStatusPayload>(json)); break;
			case "photo":
				var photo = Deserialize<StudentRecordPhotoPayload>(json);
				var student = await students.GetAsync(studentId);
				var change = changeRequestId.HasValue ? await changes.FindAsync(c => c.Id == changeRequestId && c.TenantId == student.TenantId && c.StudentId == studentId && c.Section == "photo") : null;
				if (change == null || change.Status is not ("Approved" or "Scheduled") || Canonical(change.PayloadJson) != Canonical(json))
					throw new BusinessException("Students:Records:InvalidPhotoDraft");
				var draft = await photoDrafts.GetAsync(x => x.Id == photo.PhotoDraftId && x.StudentId == studentId && x.TenantId == student.TenantId);
				if (draft.UploadedBy != change.RequestedBy) throw new BusinessException("Students:Records:InvalidPhotoDraft");
				student.SetPhotoBlobName(draft.BlobName);
				await students.UpdateAsync(student);
				break;
			default: throw new BusinessException("Students:Records:InvalidSection");
		}
	}
	public static string Hash(string json) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Canonical(json))));
	public static string Canonical(string json)
	{
		var node = JsonNode.Parse(json);
		return Sort(node)?.ToJsonString(JsonOptions) ?? "null";
	}
	private static JsonNode? Sort(JsonNode? node) => node switch
	{
		JsonObject obj => new JsonObject(obj.OrderBy(x => x.Key, StringComparer.Ordinal)
			.Select(x => new KeyValuePair<string, JsonNode?>(x.Key, Sort(x.Value)))),
		JsonArray array => new JsonArray(array.Select(Sort).ToArray()),
		_ => node?.DeepClone()
	};
	private static T Deserialize<T>(string json) => JsonSerializer.Deserialize<T>(json, JsonOptions)!;
	private static Type PayloadType(string section) => section switch
	{
		"general" => typeof(UpdateGeneralInfoDto), "family" => typeof(UpdateFamilyInfoDto),
		"communication" => typeof(UpdateCommunicationInfoDto), "additional" => typeof(UpdateAdditionalDetailsDto),
		"medical" => typeof(UpdateMedicalRecordsDto), "bank" => typeof(UpdateBankDetailsDto),
		"previous-school" => typeof(UpdatePreviousSchoolDetailsDto), "placement" => typeof(StudentRecordPlacementPayload),
		"status" => typeof(StudentRecordStatusPayload), "photo" => typeof(StudentRecordPhotoPayload), _ => throw new BusinessException("Students:Records:InvalidSection")
	};
	public async Task EnsurePhotoDraftAsync(Guid studentId, Guid requester, string json)
	{
		var payload = Deserialize<StudentRecordPhotoPayload>(json);
		var student = await students.GetAsync(studentId);
		var draft = await photoDrafts.FindAsync(x => x.Id == payload.PhotoDraftId && x.StudentId == studentId && x.TenantId == student.TenantId && x.UploadedBy == requester);
		if (draft == null || draft.ExpiresAt < DateTimeOffset.UtcNow)
			throw new BusinessException("Students:Records:InvalidPhotoDraft");
	}
}
