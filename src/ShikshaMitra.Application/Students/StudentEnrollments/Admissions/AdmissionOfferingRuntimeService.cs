using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public sealed record AdmissionOfferingRuntimeContext(AdmissionOffering Offering, AdmissionCycle Cycle);

public class AdmissionOfferingRuntimeService(
	IRepository<AdmissionOffering, Guid> offeringRepository,
	IRepository<AdmissionCycle, Guid> cycleRepository,
	AdmissionOfferingConfigurationValidator configurationValidator,
	AdmissionApplicationScopeAccessService applicationScope)
	: ApplicationService, ITransientDependency
{
	public async Task<AdmissionOfferingRuntimeContext> GetAvailableAsync(Guid offeringId)
	{
		var offering = await AsyncExecuter.FirstOrDefaultAsync(
			(await applicationScope.GetScopedOfferingsAsync()).Where(item => item.Id == offeringId)) ??
			throw new BusinessException(AdmissionApplicationErrorCodes.OfferingNotAvailable);
		var cycle = await cycleRepository.FindAsync(offering.AdmissionCycleId) ??
			throw new BusinessException(AdmissionApplicationErrorCodes.OfferingNotAvailable);
		var validation = await configurationValidator.ValidateStoredAsync(offering);
		var availability = AdmissionOfferingAvailabilityEvaluator.Evaluate(
			offering, cycle, validation.IsValid, new DateTimeOffset(Clock.Now.ToUniversalTime()));
		if (!availability.IsAvailable)
		{
			throw new BusinessException(
				availability.ReasonCode ?? AdmissionApplicationErrorCodes.OfferingNotAvailable,
				availability.ReasonMessage ?? "This Admission Offering is not available.");
		}
		return new AdmissionOfferingRuntimeContext(offering, cycle);
	}

	// Only called by the token-validated public inquiry capture path in the current tenant.
	internal async Task<AdmissionOfferingRuntimeContext> GetAvailableForPublicAsync(Guid offeringId, Guid cycleId)
	{
		var offering = await offeringRepository.FindAsync(item => item.Id == offeringId && item.AdmissionCycleId == cycleId) ??
			throw new BusinessException(AdmissionApplicationErrorCodes.OfferingNotAvailable);
		var cycle = await cycleRepository.GetAsync(cycleId);
		var validation = await configurationValidator.ValidateStoredForPublicAsync(offering);
		var availability = AdmissionOfferingAvailabilityEvaluator.Evaluate(offering, cycle,
			validation.IsValid, new DateTimeOffset(Clock.Now.ToUniversalTime()));
		if (!availability.IsAvailable)
			throw new BusinessException(availability.ReasonCode ?? AdmissionApplicationErrorCodes.OfferingNotAvailable,
				availability.ReasonMessage ?? "Admission is not open for this class.");
		return new(offering, cycle);
	}
}
