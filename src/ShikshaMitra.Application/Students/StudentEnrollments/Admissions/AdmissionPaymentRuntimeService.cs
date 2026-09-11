using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

[RemoteService(false)]
public class AdmissionPaymentRuntimeService(AdmissionPaymentReadService reader) : ApplicationService, ITransientDependency
{
	public async Task<AdmissionPaymentStageDto> EnsureCompleteAsync(Guid applicationId, Guid stageId)
	{
		var state = await reader.GetAsync(applicationId, stageId);
		if (state.PaymentStageEnabled && !state.CanComplete)
			throw new BusinessException(AdmissionPaymentStageErrorCodes.VerificationRequired,
				state.Blockers.Count > 0 ? state.Blockers[0] : "Complete the managed Payment stage requirements.");
		return state;
	}

	public static IReadOnlyDictionary<string, string> AddFacts(
		IReadOnlyDictionary<string, string> source, AdmissionPaymentStageDto? payment)
	{
		if (payment == null) return source;
		var result = new Dictionary<string, string>(source, StringComparer.OrdinalIgnoreCase)
		{
			["payment.status"] = payment.Status,
			["payment.settledAmount"] = payment.SettledAmount.ToString(System.Globalization.CultureInfo.InvariantCulture),
			["payment.outstandingAmount"] = payment.OutstandingAmount.ToString(System.Globalization.CultureInfo.InvariantCulture)
		};
		return result;
	}
}
