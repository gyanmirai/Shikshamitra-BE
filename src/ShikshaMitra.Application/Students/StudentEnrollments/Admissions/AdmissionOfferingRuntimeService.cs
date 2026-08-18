using System;
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
	AdmissionOfferingConfigurationValidator configurationValidator)
	: ApplicationService, ITransientDependency
{
	public async Task<AdmissionOfferingRuntimeContext> GetAvailableAsync(Guid offeringId)
	{
		var offering = await offeringRepository.FindAsync(offeringId) ??
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
}
