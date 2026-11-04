using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public sealed record AdmissionInquiryAvailability(bool CanCreateInquiry, string? Reason,
	AdmissionClassCapacityDto ClassCapacity, bool WaitlistAllowed)
{
	public AdmissionOfferingValidationResult? ConfigurationBlocker { get; init; }
}

[RemoteService(false)]
public class AdmissionInquiryAvailabilityService(
	IRepository<AdmissionCycle, Guid> cycles,
	IRepository<AdmissionWorkflowStageDefinition, Guid> stages,
	IRepository<AdmissionSeatPool, Guid> pools,
	AdmissionOfferingConfigurationValidator configuration,
	AdmissionClassCapacityReadService capacityReader) : ApplicationService, ITransientDependency
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
		var capacity = await capacityReader.ReadAsync(offering, cycle);
		var result = await ReadEvaluatedAsync(offering, availability, capacity);
		return enforceUserScope && availability.Status == AdmissionOfferingAvailabilityStatus.ConfigurationIncomplete
			? result with { ConfigurationBlocker = validation } : result;
	}

	// Only shares a result already read in this scoped staff request. Public/write guards still use ReadCoreAsync.
	internal async Task<AdmissionInquiryAvailability> ReadEvaluatedAsync(AdmissionOffering offering, AdmissionOfferingAvailability availability,
		AdmissionClassCapacityDto capacity)
	{
		if (!availability.IsAvailable || offering.EntryMode == AdmissionEntryMode.DirectRegistration)
			return new(false, availability.ReasonMessage ?? "Admission is not open for this class.", capacity, false);
		var seatStages = offering.WorkflowDefinitionId.HasValue
			? await stages.GetListAsync(stage => stage.WorkflowDefinitionId == offering.WorkflowDefinitionId.Value &&
				stage.StageType == AdmissionWorkflowStageType.Seat) : [];
		var policies = seatStages.Select(AdmissionSeatAccessService.ReadPolicy).Where(policy => policy != null).ToList();
		var waitlistAllowed = policies.Count > 0 && policies.All(policy => policy!.AllowWaitlist);
		var seatPools = await pools.GetListAsync(pool => pool.AdmissionOfferingId == offering.Id);
		var isFull = capacity.IsFull || seatPools.Any(pool => pool.OccupiedCount >= pool.Capacity);
		return isFull && !waitlistAllowed
			? new(false, "Admission is closed for this class because all seats are filled.", capacity, false)
			: new(true, null, capacity, waitlistAllowed);
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
