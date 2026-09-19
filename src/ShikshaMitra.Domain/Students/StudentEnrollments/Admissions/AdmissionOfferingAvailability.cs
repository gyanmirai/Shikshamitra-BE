using System;
using System.Collections.Generic;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public sealed record AdmissionOfferingValidationError(string Code, string Message);

public sealed class AdmissionOfferingValidationResult
{
	public IReadOnlyList<AdmissionOfferingValidationError> Errors { get; }
	public bool IsValid => Errors.Count == 0;
	public AdmissionOfferingConfigurationStatus Status => IsValid
		? AdmissionOfferingConfigurationStatus.Ready
		: AdmissionOfferingConfigurationStatus.Incomplete;

	public AdmissionOfferingValidationResult(IReadOnlyList<AdmissionOfferingValidationError> errors)
	{
		Errors = errors;
	}
}

public sealed record AdmissionOfferingAvailability(
	bool IsAvailable,
	AdmissionOfferingAvailabilityStatus Status,
	bool CanCreateInquiry,
	bool CanStartApplication,
	string? ReasonCode,
	string? ReasonMessage);

public static class AdmissionOfferingAvailabilityEvaluator
{
	public static AdmissionOfferingAvailability Evaluate(
		AdmissionOffering offering,
		AdmissionCycle cycle,
		bool configurationReady,
		DateTimeOffset now)
	{
		if (!offering.IsActive)
		{
			return Unavailable(AdmissionOfferingAvailabilityStatus.Inactive,
				AdmissionErrorCodes.OfferingInactive, "This Admission Offering is inactive.");
		}

		if (!configurationReady)
		{
			return Unavailable(AdmissionOfferingAvailabilityStatus.ConfigurationIncomplete,
				AdmissionErrorCodes.OfferingConfigurationIncomplete, "This Admission Offering configuration is incomplete.");
		}

		if (cycle.Status is AdmissionCycleStatus.Closed or AdmissionCycleStatus.Archived)
		{
			return Unavailable(AdmissionOfferingAvailabilityStatus.Closed,
				AdmissionErrorCodes.OfferingClosed, "Applications for this Admission Offering are closed.");
		}

		if (cycle.Status != AdmissionCycleStatus.Published)
		{
			return Unavailable(AdmissionOfferingAvailabilityStatus.NotOpenYet,
				AdmissionErrorCodes.OfferingNotOpenYet, "The Admission Cycle has not been published.");
		}

		var today = AdmissionCycleWindowEvaluator.GetInstitutionDate(now.UtcDateTime);
		if (today < offering.GetEffectiveOpenDate(cycle.StartDate).Date)
		{
			return Unavailable(AdmissionOfferingAvailabilityStatus.NotOpenYet,
				AdmissionErrorCodes.OfferingNotOpenYet, "Applications for this Admission Offering are not open yet.");
		}

		if (today > offering.GetEffectiveCloseDate(cycle.EndDate).Date)
		{
			return Unavailable(AdmissionOfferingAvailabilityStatus.Closed,
				AdmissionErrorCodes.OfferingClosed, "Applications for this Admission Offering are closed.");
		}

		return new AdmissionOfferingAvailability(true, AdmissionOfferingAvailabilityStatus.Open,
			true, true, null, null);
	}

	private static AdmissionOfferingAvailability Unavailable(
		AdmissionOfferingAvailabilityStatus status,
		string reasonCode,
		string reasonMessage) => new(false, status, false, false, reasonCode, reasonMessage);
}
