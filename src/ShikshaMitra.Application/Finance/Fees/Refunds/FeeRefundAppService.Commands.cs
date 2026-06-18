using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Finance.Events;
using ShikshaMitra.Finance.Fees.FeeRefund.Dtos;
using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Uow;
using Volo.Abp.Users;

namespace ShikshaMitra.Finance.Fees.FeeRefund;

public partial class FeeRefundAppService
{
	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.Add)]
	public async Task<FeeRefundRequestDto> CreateAsync(CreateFeeRefundRequestDto input)
	{
		await financeScopeAccessService.EnsureStudentAccessibleAsync(input.StudentId);

		using var uow = unitOfWorkManager.Begin(isTransactional: true);

		var request = await feeRefundManager.CreateAsync(
			studentId: input.StudentId, academicYearId: input.AcademicYearId,
			lines: input.Lines.Select(l => (l.DueFeeId, l.Amount)).ToList(),
			refundMethod: input.RefundMethod,
			refundPaymentDetails: FeeRefundManager.MapRefundPaymentDetails(input.RefundMethod, input.PaymentDetails),
			reason: input.Reason);

		await uow.CompleteAsync();

		var studentMap = await BuildStudentMapAsync([request.StudentId]);
		return MapToDto(request, studentMap);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.Add)]
	public async Task<FeeRefundRequestDto> UpdateAsync(Guid id, UpdateFeeRefundRequestDto input)
	{
		var request = await feeRefundRepository.FindWithLinesAsync(id)
			?? throw new BusinessException(FeeRefundErrorCodes.RefundNotFound).WithData("Id", id);

		if (request.Status != FeeRefundStatus.Pending)
			throw new BusinessException(FeeRefundErrorCodes.CannotModifyNonPendingRefund)
				.WithData("VoucherNumber", request.VoucherNumber).WithData("CurrentStatus", request.Status);

		await financeScopeAccessService.EnsureStudentAccessibleAsync(request.StudentId);

		using var uow = unitOfWorkManager.Begin(isTransactional: true);

		await feeRefundManager.UpdateAsync(
			request: request,
			lines: input.Lines.Select(l => (l.DueFeeId, l.Amount)).ToList(),
			refundMethod: input.RefundMethod,
			refundPaymentDetails: FeeRefundManager.MapRefundPaymentDetails(input.RefundMethod, input.PaymentDetails),
			reason: input.Reason);

		await feeRefundRepository.UpdateAsync(request, autoSave: false);
		await uow.CompleteAsync();

		var studentMap = await BuildStudentMapAsync([request.StudentId]);
		return MapToDto(request, studentMap);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.Delete)]
	public async Task DeleteAsync(Guid id)
	{
		var request = await feeRefundRepository.FindAsync(id)
			?? throw new BusinessException(FeeRefundErrorCodes.RefundNotFound).WithData("Id", id);

		if (request.Status != FeeRefundStatus.Pending)
			throw new BusinessException(FeeRefundErrorCodes.CannotModifyNonPendingRefund)
				.WithData("VoucherNumber", request.VoucherNumber).WithData("CurrentStatus", request.Status);

		await financeScopeAccessService.EnsureStudentAccessibleAsync(request.StudentId);
		await feeRefundRepository.DeleteAsync(request, autoSave: true);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.Approve)]
	public async Task<FeeRefundRequestDto> UpdateStatusAsync(Guid id, UpdateFeeRefundStatusDto input)
	{
		var request = await feeRefundRepository.FindWithLinesAsync(id)
			?? throw new BusinessException(FeeRefundErrorCodes.RefundNotFound).WithData("Id", id);

		await financeScopeAccessService.EnsureStudentAccessibleAsync(request.StudentId);

		if (CurrentUser.Id == request.CreatorId && !IsPrivilegedUser())
			throw new BusinessException("Creator cannot approve/reject their own request.");

		using var uow = unitOfWorkManager.Begin(isTransactional: true);

		var userId = currentUser.GetId();
		switch (input.Status)
		{
			case FeeRefundStatus.UnderReview:
				request.MarkUnderReview();
				break;
			case FeeRefundStatus.Approved:
				await feeRefundManager.ApproveAsync(request, userId, input.Notes);
				await eventBus.PublishAsync(new FeeRefundApprovedEto
				{
					RefundRequestId   = request.Id,
					StudentId         = request.StudentId,
					AcademicYearId    = request.AcademicYearId,
					TotalRefundAmount = request.Lines.Sum(l => l.Amount),
					TenantId          = CurrentTenant.Id,
					ApprovedAt        = Clock.Now,
				});
				break;
			case FeeRefundStatus.Rejected:
				request.Reject(userId, input.Notes);
				break;
			case FeeRefundStatus.Cancelled:
				request.Cancel();
				break;
			default:
				throw new BusinessException(FeeRefundErrorCodes.IllegalStatusTransition)
					.WithData("TargetStatus", input.Status);
		}

		await feeRefundRepository.UpdateAsync(request, autoSave: false);
		await uow.CompleteAsync();

		var studentMap = await BuildStudentMapAsync([request.StudentId]);
		return MapToDto(request, studentMap);
	}
}
