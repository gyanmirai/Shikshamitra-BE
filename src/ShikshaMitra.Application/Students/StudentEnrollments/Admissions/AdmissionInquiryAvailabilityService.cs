using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public sealed record AdmissionInquiryAvailability(bool CanCreateInquiry, string? Reason)
{
	public AdmissionOfferingValidationResult? ConfigurationBlocker { get; init; }
}

[RemoteService(false)]
public class AdmissionInquiryAvailabilityService(
	IRepository<AdmissionCycle, Guid> cycles,
	AdmissionOfferingConfigurationValidator configuration) : ApplicationService, ITransientDependency
{
	// Offering scope is resolved by the staff runtime or by the public link before this shared check.
	public Task<AdmissionInquiryAvailability> ReadAsync(AdmissionOffering offering) => ReadCoreAsync(offering, enforceUserScope: true);

	// Only used after a public inquiry link has resolved this offering in its tenant/cycle/profile.
	internal Task<AdmissionInquiryAvailability> ReadForPublicAsync(AdmissionOffering offering) => ReadCoreAsync(offering, enforceUserScope: false);

	private async Task<AdmissionInquiryAvailability> ReadCoreAsync(AdmissionOffering offering, bool enforceUserScope)
	{
		var cycle = await cycles.GetAsync(offering.AdmissionCycleId);
		var validation = enforceUserScope ? await configuration.ValidateStoredAsync(offering) : await configuration.ValidateStoredForPublicAsync(offering);
		var availability = AdmissionOfferingAvailabilityEvaluator.Evaluate(offering, cycle,
			validation.IsValid, new DateTimeOffset(Clock.Now.ToUniversalTime()));
		if (availability.Status == AdmissionOfferingAvailabilityStatus.ConfigurationIncomplete)
			availability = availability with { ReasonMessage = enforceUserScope
				? string.Join(" ", validation.Errors.Select(error => error.Message).Distinct())
				: "Admission setup for this class needs attention. Contact the school for help." };
		var result = ReadEvaluated(offering, availability);
		return enforceUserScope && availability.Status == AdmissionOfferingAvailabilityStatus.ConfigurationIncomplete
			? result with { ConfigurationBlocker = validation } : result;
	}

	// Only shares a result already read in this scoped staff request. Public/write guards still use ReadCoreAsync.
	internal static AdmissionInquiryAvailability ReadEvaluated(AdmissionOffering offering, AdmissionOfferingAvailability availability)
	{
		if (!availability.IsAvailable || offering.EntryMode == AdmissionEntryMode.DirectRegistration)
			return new(false, availability.ReasonMessage ?? "Admission is not open for this class.");
		// Inquiry capture and Registration handoff do not reserve seats. Historical Seat
		// stages enforce their own policy later; division placement remains owned by Onboarding.
		return new(true, null);
	}

	public async Task EnsureAvailableAsync(AdmissionOffering offering)
	{
		var availability = await ReadAsync(offering);
		EnsureAvailable(availability);
	}

	internal async Task EnsureAvailableForPublicAsync(AdmissionOffering offering)
	{
		var availability = await ReadForPublicAsync(offering);
		EnsureAvailable(availability);
	}

	internal static void EnsureAvailable(AdmissionInquiryAvailability availability)
	{
		if (availability.ConfigurationBlocker != null)
			AdmissionOfferingConfigurationValidator.EnsureCanSave(availability.ConfigurationBlocker, isActive: true);
		if (!availability.CanCreateInquiry)
			throw new BusinessException(AdmissionApplicationErrorCodes.OfferingNotAvailable, availability.Reason);
	}
}
