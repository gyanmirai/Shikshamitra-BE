using System;
using System.Threading.Tasks;
using ShikshaMitra.Finance.Fees.Collections.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Finance.Fees.Collections;

public interface IAdmissionFeeCollectionAppService : IApplicationService
{
	Task<AdmissionFeeCollectionDto> GetSummaryAsync(Guid applicationId);
	Task<AdmissionFeeCollectionDto> PrepareAsync(Guid applicationId, PrepareAdmissionFeeCollectionDto input);
	Task<FeeCollectFormDto> GetCollectFormAsync(Guid applicationId);
	Task<CollectFeeOutput> CollectAsync(Guid applicationId, CollectAdmissionFeeInput input);
}
