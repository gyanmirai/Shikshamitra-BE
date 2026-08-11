using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Volo.Abp;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

internal static class AdmissionCrmRegistrationModelMapper
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

	public static AdmissionCrmRegistrationTemplateUpsertDto ToTemplateUpsert(AdmissionCrmRegistrationTemplate template) => new()
	{
		Name = template.Name,
		IsFallback = template.IsFallback,
		Applicability = Deserialize<AdmissionCrmPlaybookApplicabilityDto>(template.ApplicabilityJson),
		Sections = Deserialize<List<AdmissionCrmRegistrationSectionDto>>(template.SectionsJson),
		LinkExpiryDays = template.LinkExpiryDays,
		AllowPartialSave = template.AllowPartialSave,
		FeeRequired = template.FeeRequired,
		FeeAmount = template.FeeAmount,
		AutoIssueAtStage = template.AutoIssueAtStage,
		Notes = template.Notes
	};

	public static AdmissionCrmRegistrationTemplateDto ToTemplateDto(AdmissionCrmRegistrationTemplate template)
	{
		var input = ToTemplateUpsert(template);
		return new AdmissionCrmRegistrationTemplateDto
		{
			Id = template.Id, AcademicYearId = template.AcademicYearId, Version = template.Version, Status = template.Status,
			PublishedAt = template.PublishedAt, RetiredAt = template.RetiredAt,
			Name = input.Name, IsFallback = input.IsFallback, Applicability = input.Applicability, Sections = input.Sections,
			LinkExpiryDays = input.LinkExpiryDays, AllowPartialSave = input.AllowPartialSave, FeeRequired = input.FeeRequired,
			FeeAmount = input.FeeAmount, AutoIssueAtStage = input.AutoIssueAtStage, Notes = input.Notes
		};
	}

	public static AdmissionCrmFormNumberSeriesDto ToSeriesDto(AdmissionCrmFormNumberSeries series) => new()
	{
		Id = series.Id, AcademicYearId = series.AcademicYearId, Name = series.Name, Prefix = series.Prefix, Suffix = series.Suffix,
		Padding = series.Padding, NextNumber = series.NextNumber,
		Applicability = Deserialize<AdmissionCrmPlaybookApplicabilityDto>(series.ApplicabilityJson), IsActive = series.IsActive,
		NextPreview = series.Prefix + series.NextNumber.ToString($"D{series.Padding}") + series.Suffix
	};

	public static AdmissionCrmRegistrationApplicationDto ToApplicationDto(AdmissionCrmRegistrationApplication application)
	{
		var snapshot = Deserialize<AdmissionCrmRegistrationTemplateDto>(application.TemplateSnapshotJson);
		return new AdmissionCrmRegistrationApplicationDto
		{
			Id = application.Id, ConcurrencyStamp = application.ConcurrencyStamp, AcademicYearId = application.AcademicYearId,
			LeadId = application.LeadId, RegistrationTemplateId = application.RegistrationTemplateId,
			RegistrationTemplateVersion = application.RegistrationTemplateVersion, PlaybookId = application.PlaybookId,
			FormNumber = application.FormNumber, Status = application.Status, Sections = snapshot.Sections,
			Provenance = application.Provenance, DataEntryState = application.DataEntryState,
			PhysicalFormNumber = application.PhysicalFormNumber, PhysicalFormReceivedDate = application.PhysicalFormReceivedDate,
			PhysicalFormReceivedByUserId = application.PhysicalFormReceivedByUserId,
			ScannedFormAttachmentReference = application.ScannedFormAttachmentReference,
			Prefill = Deserialize<Dictionary<string, JsonElement>>(application.PrefillJson),
			Responses = Deserialize<Dictionary<string, JsonElement>>(application.ResponseJson),
			CorrectionFieldKeys = Deserialize<List<string>>(application.CorrectionJson),
			CompletionPercentage = application.CompletionPercentage, IssuedAt = application.IssuedAt,
			LinkExpiresAt = application.LinkExpiresAt, OpenedAt = application.OpenedAt, LastSavedAt = application.LastSavedAt,
			SubmittedAt = application.SubmittedAt, VerifiedAt = application.VerifiedAt, FeeAmount = application.FeeAmount,
			FeeStatus = application.FeeStatus, PaymentReference = application.PaymentReference, LifecycleReason = application.LifecycleReason
		};
	}

	public static Dictionary<string, object?> BuildPrefill(AdmissionInquiryLead lead) => new(StringComparer.OrdinalIgnoreCase)
	{
		["firstName"] = lead.FirstName, ["middleName"] = lead.MiddleName, ["lastName"] = lead.LastName,
		["dateOfBirth"] = lead.DateOfBirth?.ToString("yyyy-MM-dd"), ["parentGuardianName"] = lead.ParentGuardianName,
		["primaryContactNo"] = lead.PrimaryContactNo, ["email"] = lead.Email, ["desiredClassName"] = lead.DesiredClassName,
		["campus"] = lead.CampusBranchName, ["admissionRoute"] = lead.IntendedRouteNameSnapshot,
		["admissionRouteDefinitionId"] = lead.IntendedRouteDefinitionId,
		["admissionRouteDefinitionVersion"] = lead.IntendedRouteDefinitionVersion
	};

	public static HashSet<string> GetFieldKeys(AdmissionCrmRegistrationApplication application) =>
		Deserialize<AdmissionCrmRegistrationTemplateDto>(application.TemplateSnapshotJson).Sections
			.SelectMany(x => x.Fields).Select(x => x.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);

	public static bool SameApplicability(AdmissionCrmPlaybookApplicabilityDto left, AdmissionCrmPlaybookApplicabilityDto right) =>
		Same(left.Campus, right.Campus) && Same(left.DesiredClass, right.DesiredClass) && Same(left.Board, right.Board) &&
		Same(left.Medium, right.Medium) && Same(left.ApplicantCategory, right.ApplicantCategory) &&
		Same(left.AdmissionRound, right.AdmissionRound) && Same(left.AdmissionRoute, right.AdmissionRoute);

	public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, JsonOptions);
	public static T Deserialize<T>(string json) => JsonSerializer.Deserialize<T>(json, JsonOptions)
		?? throw new BusinessException("AdmissionCrm:InvalidStoredRegistrationConfiguration");
	private static bool Same(string? left, string? right) => string.Equals(left?.Trim(), right?.Trim(), StringComparison.OrdinalIgnoreCase);
}
