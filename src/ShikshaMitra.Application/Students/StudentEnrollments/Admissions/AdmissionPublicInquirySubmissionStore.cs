using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

internal sealed class AdmissionPublicInquirySubmissionStore(IAdmissionPublicInquirySubmissionRepository submissions) : ITransientDependency
{

	public static SubmitPublicAdmissionInquiryDto ProjectInput(SubmitPublicAdmissionInquiryDto input, AdmissionFormDto staff, AdmissionFormDto projected)
	{
		var result = JsonSerializer.Deserialize<SubmitPublicAdmissionInquiryDto>(JsonSerializer.Serialize(input))!;
		var retained = projected.Sections.SelectMany(section => section.Fields).Select(field => field.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
		var omitted = staff.Sections.SelectMany(section => section.Fields).Where(field => !retained.Contains(field.Code)).Select(field => field.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
		result.FormValues.RemoveAll(value => omitted.Contains(value.FieldCode));
		if (result.RequestId == Guid.Empty || result.FormValues.GroupBy(value => value.FieldCode, StringComparer.OrdinalIgnoreCase).Any(group => group.Count() > 1))
			throw Conflict();
		return result;
	}

	public static string Fingerprint(SubmitPublicAdmissionInquiryDto input, AdmissionFormDto schema)
	{
		var fields = AdmissionEffectiveFormResolver.MaterializeFields(schema).ToDictionary(field => field.Code, StringComparer.OrdinalIgnoreCase);
		string NormalizeAnswer(AdmissionFormValueDto value)
		{
			var json = value.ValueJson;
			if (fields.TryGetValue(value.FieldCode, out var field))
			{
				json = AdmissionFormEmailValueNormalizer.Normalize(field, json);
				if (field.FieldType == AdmissionFormFieldType.Phone)
					json = JsonSerializer.Serialize(NormalizePhone(AdmissionApplicationValueConverter.Unwrap(json)));
			}
			try { return NormalizeNode(JsonNode.Parse(json ?? "null"))?.ToJsonString() ?? "null"; }
			catch (JsonException) { return JsonSerializer.Serialize(json?.Trim() ?? string.Empty); }
		}
		var canonical = JsonSerializer.Serialize(new
		{
			Version = 1, input.AdmissionOfferingId, input.BoardId, input.MediumId, input.StandardId,
			ApplicantFirstName = Text(input.ApplicantFirstName), ApplicantLastName = Text(input.ApplicantLastName),
			GuardianFirstName = Text(input.GuardianFirstName), GuardianLastName = Text(input.GuardianLastName),
			GuardianPhone = NormalizePhone(input.GuardianPhone), GuardianEmail = Text(input.GuardianEmail).ToLowerInvariant(),
			ClassInterest = Text(input.ClassInterest), PreferredLanguageCode = Text(input.PreferredLanguageCode).ToLowerInvariant(), input.ConsentToContact,
			Answers = input.FormValues.OrderBy(value => value.FieldCode, StringComparer.OrdinalIgnoreCase)
				.Select(value => new { Code = value.FieldCode.ToUpperInvariant(), Value = NormalizeAnswer(value) }).ToList()
		});
		return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
	}

	public async Task<bool> IsReplayAsync(AdmissionPublicInquiryLink link, Guid? requestId, string fingerprint)
	{
		var saved = await submissions.FindFingerprintAsync(link.TenantId, link.AdmissionCycleId, link.Id, requestId, fingerprint);
		if (saved == null) return false;
		if (requestId.HasValue && saved != fingerprint) throw Conflict();
		return true;
	}

	public static void Capture(AdmissionInquiry inquiry, Guid linkId, Guid? requestId, string fingerprint)
	{
		inquiry.SetProperty(AdmissionPublicInquirySubmissionProperties.LinkId, linkId.ToString("N"));
		if (requestId.HasValue) inquiry.SetProperty(AdmissionPublicInquirySubmissionProperties.RequestId, requestId.Value.ToString("N"));
		inquiry.SetProperty(AdmissionPublicInquirySubmissionProperties.Fingerprint, fingerprint);
		inquiry.SetProperty("AdmissionPublicCaptureTrust", "unconfirmed");
	}

	private static string Text(string? value) => value?.Trim() ?? string.Empty;
	private static string NormalizePhone(string? value)
	{
		if (string.IsNullOrWhiteSpace(value)) return string.Empty;
		try { return AdmissionInquiryPhoneValidator.NormalizeAndValidateIndianMobile(value); }
		catch (BusinessException) { return value.Trim(); } // Ordinary form validation owns invalid input.
	}
	private static BusinessException Conflict() => new(AdmissionInquiryManagementErrorCodes.InvalidInput,
		"The submission could not be accepted. Refresh the form and submit it again.");
	private static JsonNode? NormalizeNode(JsonNode? node) => node switch
	{
		JsonObject obj => new JsonObject(obj.OrderBy(property => property.Key, StringComparer.Ordinal)
			.Select(property => new KeyValuePair<string, JsonNode?>(property.Key, NormalizeNode(property.Value)))),
		JsonArray array => new JsonArray(array.Select(NormalizeNode).ToArray()),
		JsonValue value when value.TryGetValue<string>(out var text) => JsonValue.Create(text.Trim()),
		_ => node?.DeepClone()
	};
}
