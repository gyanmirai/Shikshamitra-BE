using System.Collections.Generic;
using System.Text.Json;
using Volo.Abp;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

internal static class AdmissionCrmPublicRegistrationMapper
{
	internal static AdmissionCrmRegistrationTemplateDto DeserializeTemplate(string json) =>
		JsonSerializer.Deserialize<AdmissionCrmRegistrationTemplateDto>(
			json, AdmissionCrmPublicFormat.JsonOptions) ??
		throw new BusinessException("AdmissionCrm:InvalidStoredRegistrationConfiguration");

	internal static AdmissionCrmRegistrationApplicationDto ToDto(AdmissionCrmRegistrationApplication application)
	{
		var snapshot = DeserializeTemplate(application.TemplateSnapshotJson);
		return new AdmissionCrmRegistrationApplicationDto
		{
			Id = application.Id,
			ConcurrencyStamp = application.ConcurrencyStamp,
			AcademicYearId = application.AcademicYearId,
			LeadId = application.LeadId,
			RegistrationTemplateId = application.RegistrationTemplateId,
			RegistrationTemplateVersion = application.RegistrationTemplateVersion,
			PlaybookId = application.PlaybookId,
			FormNumber = application.FormNumber,
			Status = application.Status,
			Sections = snapshot.Sections,
			Prefill = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
				application.PrefillJson, AdmissionCrmPublicFormat.JsonOptions) ?? [],
			Responses = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
				application.ResponseJson, AdmissionCrmPublicFormat.JsonOptions) ?? [],
			CorrectionFieldKeys = JsonSerializer.Deserialize<List<string>>(
				application.CorrectionJson, AdmissionCrmPublicFormat.JsonOptions) ?? [],
			CompletionPercentage = application.CompletionPercentage,
			IssuedAt = application.IssuedAt,
			LinkExpiresAt = application.LinkExpiresAt,
			OpenedAt = application.OpenedAt,
			LastSavedAt = application.LastSavedAt,
			SubmittedAt = application.SubmittedAt,
			VerifiedAt = application.VerifiedAt,
			FeeAmount = application.FeeAmount,
			FeeStatus = application.FeeStatus,
			PaymentReference = application.PaymentReference,
			LifecycleReason = application.LifecycleReason
		};
	}
}
