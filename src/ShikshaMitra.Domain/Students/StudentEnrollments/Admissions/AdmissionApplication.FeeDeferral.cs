using System;
using System.Text.Json;
using Volo.Abp;
using Volo.Abp.Data;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public sealed record AdmissionFeeCollectionDeferral(Guid StageId, string Reason, Guid DeferredBy, DateTimeOffset DeferredAt);

public partial class AdmissionApplication
{
	private const string FeeCollectionDeferralProperty = "AdmissionFeeCollectionDeferral";
	public AdmissionFeeCollectionDeferral? GetFeeCollectionDeferral() =>
		this.GetProperty<string>(FeeCollectionDeferralProperty) is string json
			? JsonSerializer.Deserialize<AdmissionFeeCollectionDeferral>(json) : null;

	public void DeferFeeCollection(Guid stageId, string reason, Guid actorId, DateTimeOffset now)
	{
		if (stageId == Guid.Empty || CurrentStageInstanceId != stageId || actorId == Guid.Empty || !CanEndJourney)
			throw new BusinessException(AdmissionFeeSelectionErrorCodes.InvalidState,
				"Collection can only be deferred during an open admission Payment stage.");
		if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 512)
			throw new BusinessException(AdmissionFeeSelectionErrorCodes.DeferralReasonRequired,
				"Enter a reason of up to 512 characters for collecting fees later in Onboarding.");
		this.SetProperty(FeeCollectionDeferralProperty, JsonSerializer.Serialize(
			new AdmissionFeeCollectionDeferral(stageId, reason.Trim(), actorId, now.ToUniversalTime())));
	}
}
