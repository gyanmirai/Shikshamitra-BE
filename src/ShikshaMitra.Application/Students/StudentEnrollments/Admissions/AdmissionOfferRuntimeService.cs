using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionOfferRuntimeService(AdmissionOfferReadService reader) : ITransientDependency
{
	public async Task<AdmissionOfferStageDto> EnsureCompleteAsync(Guid applicationId, Guid stageId)
	{
		var state = await reader.GetAsync(applicationId, stageId);
		if (!state.CanComplete)
			throw new BusinessException(AdmissionOfferStageErrorCodes.ResponseRequired,
				state.Blockers.Count > 0 ? state.Blockers[0] : "Complete the admission offer response.");
		return state;
	}

	public static IReadOnlyDictionary<string, string> AddFacts(
		IReadOnlyDictionary<string, string> values, AdmissionOfferStageDto? offer)
	{
		if (offer == null) return values;
		var result = new Dictionary<string, string>(values, StringComparer.Ordinal)
		{
			["offer.status"] = offer.Status,
			["offer.versionNumber"] = offer.VersionNumber.ToString(System.Globalization.CultureInfo.InvariantCulture),
			["offer.extensionCount"] = offer.ExtensionCount.ToString(System.Globalization.CultureInfo.InvariantCulture),
			["offer.isExpired"] = (offer.Status == "expired").ToString().ToLowerInvariant()
		};
		return result;
	}
}
