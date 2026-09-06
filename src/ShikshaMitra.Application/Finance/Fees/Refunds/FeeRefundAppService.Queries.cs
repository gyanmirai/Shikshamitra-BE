using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Finance.Common;
using ShikshaMitra.Finance.Fees.Concessions;
using ShikshaMitra.Finance.Fees.FeeRefund.Dtos;
using ShikshaMitra.Finance.Fees.Receipts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Finance.Fees.FeeRefund;

public partial class FeeRefundAppService
{
	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.View)]
	public async Task<PagedResultDto<FeeRefundRequestDto>> GetListAsync(GetFeeRefundsInput input)
	{
		FinanceClassDivisionFilterValidator.Validate(input);

		var scopedStudentIds = await financeScopeAccessService.GetScopedStudentIdsQueryAsync();
		var (items, totalCount) = await feeRefundRepository.GetListWithDetailsAsync(
			studentId: input.StudentId, academicMediumId: input.AcademicMediumId,
			standardId: input.StandardId, divisionId: input.DivisionId,
			status: input.Status, search: input.Search,
			skipCount: input.SkipCount, maxResultCount: input.MaxResultCount,
			sorting: input.Sorting, scopedStudentIdsQuery: scopedStudentIds);

		var studentMap = await BuildStudentMapAsync(items.Select(r => r.StudentId).Distinct());
		return new PagedResultDto<FeeRefundRequestDto>(totalCount, items.Select(r => MapToDto(r, studentMap)).ToList());
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.View)]
	public async Task<FeeRefundRequestDto> GetAsync(Guid id)
	{
		var request = await feeRefundRepository.FindWithLinesAsync(id)
			?? throw new Volo.Abp.BusinessException(FeeRefundErrorCodes.RefundNotFound).WithData("Id", id);

		await financeScopeAccessService.EnsureStudentAccessibleAsync(request.StudentId);

		var studentMap = await BuildStudentMapAsync([request.StudentId]);
		var dto = MapToDto(request, studentMap);
		await PopulateRefundDetailsAsync(dto, request);
		return dto;
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.View)]
	public async Task<RefundEligibilityDto> GetEligibilityAsync(Guid studentId, Guid academicYearId)
	{
		await financeScopeAccessService.EnsureStudentAccessibleAsync(studentId);

		var dueFees = await dueFeeRepository.GetListAsync(df => df.StudentId == studentId && df.PaidAmount > 0);
		if (dueFees.Count == 0)
			return new RefundEligibilityDto { StudentId = studentId, AcademicYearId = academicYearId };

		var feeHeadIds = dueFees.Select(df => df.FeeHeadId).Distinct().ToList();
		var feeHeads = await feeHeadRepository.GetListAsync(fh => feeHeadIds.Contains(fh.Id));
		var feeHeadMap = feeHeads.ToDictionary(fh => fh.Id, fh => fh.Name);

		// reconciliation amount which are not approved or rejected
		var reconciliations = await feePaymentReceiptRepository.GetListAsync(x => x.StudentId == studentId && x.VerificationStatus == FeeReceiptVerificationStatus.PendingVerification);

		

		var grouped = dueFees
			.GroupBy(df => df.FeeHeadId)
			.Select(g => new RefundEligibilityFeeHeadDto
			{
				FeeHeadId = g.Key,
				FeeHeadName = feeHeadMap.TryGetValue(g.Key, out var n) ? n : g.Key.ToString(),
				TotalPaid = (double)g.Sum(df => df.PaidAmount),
				TotalRefunded = (double)g.Sum(df => df.RefundedAmount),
				TotalRefundable = (double)g.Sum(df => df.RefundableBalance),
				Months = g.OrderBy(df => df.Year).ThenBy(df => df.Month)
					.Select(df => new RefundEligibilityMonthDto
					{
						DueFeeId = df.Id, Month = df.Month, Year = df.Year,
						Total = (double)df.OriginalAmount, Paid = (double)df.PaidAmount,
						Refunded = (double)df.RefundedAmount, Refundable = (double)df.RefundableBalance,
					}).ToList(),
			}).ToList();

		return new RefundEligibilityDto
		{
			StudentId = studentId, AcademicYearId = academicYearId,
			TotalPaid = grouped.Sum(fh => fh.TotalPaid),
			TotalRefunded = grouped.Sum(fh => fh.TotalRefunded),
			TotalRefundable = grouped.Sum(fh => fh.TotalRefundable),
			FeeHeads = grouped,
		};
	}
}
