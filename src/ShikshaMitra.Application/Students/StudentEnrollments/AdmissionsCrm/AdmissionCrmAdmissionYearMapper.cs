using System;
using System.Collections.Generic;
using System.Text.Json;
using Volo.Abp;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

internal static class AdmissionCrmAdmissionYearMapper
{
	internal static void EnsureConcurrency(
		AdmissionCrmAdmissionYearSetup setup,
		string concurrencyStamp)
	{
		if (string.Equals(setup.ConcurrencyStamp, concurrencyStamp, StringComparison.Ordinal))
		{
			return;
		}

		throw new BusinessException("AdmissionCrm:AdmissionYearDraftChanged")
			.WithData("SetupId", setup.Id)
			.WithData("Version", setup.Version);
	}

	internal static AdmissionCrmAdmissionYearSetupDto ToDto(
		AdmissionCrmAdmissionYearSetup setup)
	{
		return new AdmissionCrmAdmissionYearSetupDto
		{
			Id = setup.Id,
			ConcurrencyStamp = setup.ConcurrencyStamp,
			AcademicYearId = setup.AcademicYearId,
			Version = setup.Version,
			Status = setup.Status,
			InquiryOpenDate = setup.InquiryOpenDate,
			InquiryCloseDate = setup.InquiryCloseDate,
			RegistrationCloseDate = setup.RegistrationCloseDate,
			ActivationTargetDate = setup.ActivationTargetDate,
			EntranceExamRequired = setup.EntranceExamRequired,
			FinanceVerificationRequired = setup.FinanceVerificationRequired,
			PlacementTiming = setup.PlacementTiming,
			DefaultOwnerUserId = setup.DefaultOwnerUserId,
			TargetStandardIds =
				JsonSerializer.Deserialize<List<Guid>>(setup.TargetStandardIdsJson, AdmissionCrmSetupFormat.JsonOptions) ?? [],
			RouteTypes =
				JsonSerializer.Deserialize<List<AdmissionCrmRouteType>>(setup.RouteTypesJson, AdmissionCrmSetupFormat.JsonOptions) ?? [],
			StepOwners =
				JsonSerializer.Deserialize<Dictionary<string, Guid?>>(setup.StepOwnersJson, AdmissionCrmSetupFormat.JsonOptions) ??
				new Dictionary<string, Guid?>(),
			PublishedAt = setup.PublishedAt,
			PublishedByUserId = setup.PublishedByUserId,
			ClosedAt = setup.ClosedAt,
			ClosedByUserId = setup.ClosedByUserId,
			CloseReason = setup.CloseReason
		};
	}

	internal static string GetLockKey(Guid? tenantId, Guid academicYearId) =>
		$"AdmissionCrm:AdmissionYear:{tenantId?.ToString("D") ?? "host"}:{academicYearId:D}";
}