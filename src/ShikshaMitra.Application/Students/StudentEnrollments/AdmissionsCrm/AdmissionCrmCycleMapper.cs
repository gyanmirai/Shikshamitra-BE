using System;
using System.Linq;
using ShikshaMitra.TenantSettings.AcademicYears;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

internal static class AdmissionCrmCycleMapper
{
	internal static AdmissionCrmCycleListItemDto Build(
		AcademicYear year,
		AdmissionCrmCycleConfiguration? configuration,
		int applicantCount,
		AdmissionCrmSetupStatusDto? readiness)
	{
		var readinessStatus = readiness?.Status ?? GetReadiness(configuration);
		return new AdmissionCrmCycleListItemDto
		{
			AcademicYearId = year.Id,
			CycleConfigurationId = configuration?.Id,
			CycleName = configuration?.CycleName ?? $"{year.Name} Admission Year",
			AcademicYearName = year.Name,
			AcademicYearStartDate = year.StartDate,
			AcademicYearEndDate = year.EndDate,
			AdmissionWindowStart = null,
			AdmissionWindowEnd = null,
			Status = GetCycleStatus(configuration, readinessStatus),
			Readiness = readinessStatus,
			ApplicantCount = applicantCount,
			OwnerUserId = null,
			OwnerDisplayName = null,
			LastUpdatedAt = configuration?.LastModificationTime ?? configuration?.CreationTime,
			RequiredActions = readiness?.Items
				.Where(item => item.AccessMode != AdmissionCrmSettingsAccessMode.Hidden &&
					item.Status == AdmissionCrmSetupReadiness.NeedsAttention)
				.Select(item => new AdmissionCrmCycleRequiredActionDto
				{
					Key = item.Key,
					Title = item.Title,
					Path = item.Path,
					BlockerCodes = item.BlockerCodes
				})
				.ToList() ?? []
		};
	}

	internal static AdmissionCrmSetupReadiness GetReadiness(AdmissionCrmCycleConfiguration? configuration)
	{
		if (configuration is null)
		{
			return AdmissionCrmSetupReadiness.NotStarted;
		}

		return !string.IsNullOrWhiteSpace(configuration.CycleName) && configuration.DefaultLeadSlaHours > 0
			? AdmissionCrmSetupReadiness.Ready
			: AdmissionCrmSetupReadiness.NeedsAttention;
	}

	internal static AdmissionCrmCycleStatus GetCycleStatus(
		AdmissionCrmCycleConfiguration? configuration,
		AdmissionCrmSetupReadiness readiness)
	{
		if (configuration is null)
		{
			return AdmissionCrmCycleStatus.Draft;
		}

		if (configuration.JourneyLocked)
		{
			return AdmissionCrmCycleStatus.Paused;
		}

		if (!configuration.IsActive)
		{
			return AdmissionCrmCycleStatus.Draft;
		}

		return readiness is AdmissionCrmSetupReadiness.Ready or AdmissionCrmSetupReadiness.Managed
			? AdmissionCrmCycleStatus.Open
			: AdmissionCrmCycleStatus.ReadinessReview;
	}
}