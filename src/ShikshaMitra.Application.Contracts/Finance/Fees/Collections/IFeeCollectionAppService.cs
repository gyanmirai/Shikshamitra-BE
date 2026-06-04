using System;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Finance.Fees.Collections.Dtos;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Finance.Fees.Collections;

public interface IFeeCollectionAppService : IApplicationService
{
	Task<PagedResultDto<FeeCollectionSummaryItemDto>> GetSummaryAsync(
		GetFeeCollectionSummaryInput input,
		CancellationToken cancellationToken = default);

	Task<byte[]> ExportAsync(
		FeeCollectionExportRequestDto input,
		CancellationToken cancellationToken = default);

	Task<CollectFeeOutput> CollectFeeAsync(CollectFeeInput input);

	Task<FeeCollectFormDto> GetCollectFormAsync(Guid studentId, Guid academicYearId, CancellationToken cancellationToken = default);

	Task<StudentFeeDetailsDto> GetStudentFeeDetailsAsync(Guid studentId, Guid academicYearId, CancellationToken cancellationToken = default);
}
