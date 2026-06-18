using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Finance.Fees.Collections.Dtos;
using System;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Finance.Fees.Collections;

[RemoteService(false)]
public class FeeCollectionAppService(
	FeeCollectionSummaryQueryService feeCollectionSummaryQueryService,
	StudentFeeDetailsQueryService studentFeeDetailsQueryService,
	CollectFeeWorkflow collectFeeWorkflow
) : ApplicationService, IFeeCollectionAppService
{
	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_FeeCollections.View)]
	public Task<PagedResultDto<FeeCollectionSummaryItemDto>> GetSummaryAsync(
		GetFeeCollectionSummaryInput input,
		CancellationToken cancellationToken = default)
		=> feeCollectionSummaryQueryService.GetSummaryAsync(input, cancellationToken);

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_FeeCollections.View)]
	public Task<byte[]> ExportAsync(
		FeeCollectionExportRequestDto input,
		CancellationToken cancellationToken = default)
		=> feeCollectionSummaryQueryService.ExportAsync(input, cancellationToken);

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_FeeCollections.Add)]
	public Task<CollectFeeOutput> CollectFeeAsync(CollectFeeInput input)
		=> collectFeeWorkflow.CollectFeeAsync(input);

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_FeeCollections.View)]
	public Task<FeeCollectFormDto> GetCollectFormAsync(
		Guid studentId, Guid academicYearId, CancellationToken cancellationToken = default)
		=> studentFeeDetailsQueryService.GetCollectFormAsync(studentId, academicYearId, cancellationToken);

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_FeeCollections.View)]
	public Task<StudentFeeDetailsDto> GetStudentFeeDetailsAsync(
		Guid studentId, Guid academicYearId, CancellationToken cancellationToken = default)
		=> studentFeeDetailsQueryService.GetStudentFeeDetailsAsync(studentId, academicYearId, cancellationToken);
}
