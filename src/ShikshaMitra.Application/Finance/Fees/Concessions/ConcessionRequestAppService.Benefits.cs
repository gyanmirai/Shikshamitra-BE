using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Common;
using ShikshaMitra.Finance.Fees.ConcessionRequests;
using ShikshaMitra.Finance.Fees.Concessions.Dtos;
using ShikshaMitra.Finance.Mobile;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Users;

namespace ShikshaMitra.Finance.Fees.Concessions;

public partial class ConcessionRequestAppService
{
	private async Task ValidateContextRequestAsync(Guid? requestId, Guid studentId, Guid yearId, Guid? typeId = null)
	{
		if (!requestId.HasValue) return;
		var query = await concessionRequestRepository.GetQueryableAsync();
		if (!await AsyncExecuter.AnyAsync(query.Where(x => x.Id == requestId && x.StudentId == studentId &&
			x.AcademicYearId == yearId && (!typeId.HasValue || x.ConcessionTypeId == typeId))))
			throw new BusinessException(FinanceConcessionErrorCodes.ScopeMismatch);
	}

	private async Task AcquireBenefitForRequestAsync(Guid id)
	{
		// Scalar projection avoids retaining an entity/stamp read before waiting for the lock.
		var key = await AsyncExecuter.FirstOrDefaultAsync((await concessionRequestRepository.GetQueryableAsync())
			.Where(x => x.Id == id).Select(x => new { x.StudentId, x.ConcessionTypeId, x.AcademicYearId }))
			?? throw new EntityNotFoundException(typeof(ConcessionRequest), id);
		await financeScopeAccessService.EnsureStudentAccessibleAsync(key.StudentId);
		await concessionRequestRepository.AcquireBenefitLockAsync(key.StudentId, key.ConcessionTypeId, key.AcademicYearId);
	}

	private Task<Guid?> FindBenefitCommandReplayAsync(string action, string? key, object payload) =>
		concessionRequestRepository.ReadCommittedAsync(() => FindCommandReplayAsync(action, key, payload));

	private async Task AcquireFamilyBenefitForRequestAsync(Guid child, Guid year, Guid id)
	{
		var key = await AsyncExecuter.FirstOrDefaultAsync((await concessionRequestRepository.GetQueryableAsync())
			.Where(x => x.Id == id).Select(x => new { x.StudentId, x.ConcessionTypeId, x.AcademicYearId, x.CreatorId }))
			?? throw new EntityNotFoundException(typeof(ConcessionRequest), id);
		MobileFinanceCommandRules.RequireOwner(child, year, currentUser.GetId(), key.StudentId, key.AcademicYearId, key.CreatorId);
		await concessionRequestRepository.AcquireBenefitLockAsync(key.StudentId, key.ConcessionTypeId, key.AcademicYearId);
	}

	private Task<ApiDataResponseDto<ConcessionRequestDetailDto>> ReadBenefitReplayDetailAsync(Guid id) =>
		concessionRequestRepository.ReadCommittedAsync(async () =>
		{
			var request = await concessionRequestRepository.GetWithDetailsAsync(id);
			await financeScopeAccessService.EnsureStudentAccessibleAsync(request.StudentId);
			return ApiDataResponseDto<ConcessionRequestDetailDto>.SuccessResult(await MapDetailAsync(request));
		});

	private ConcessionBenefitBlockerDto? MapBenefitBlocker(ConcessionBenefitConflict? conflict)
	{
		if (conflict == null) return null;
		var code = conflict.Status == ConcessionRequestStatus.Pending
			? FinanceConcessionErrorCodes.DuplicatePendingRequest : FinanceConcessionErrorCodes.BenefitAlreadyGranted;
		return new ConcessionBenefitBlockerDto
		{
			Code = code, Message = L[code].Value,
			BlockingRequestId = conflict.RequestId, BlockingStatus = conflict.Status.ToString(),
		};
	}
}
