using System;
using System.Linq;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using ShikshaMitra.Finance.Fees.FeeRefund.Dtos;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Application.Finance.Fees.Receipts;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Students;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Finance.Fees.FeeRefund;

public partial class FeeRefundAppService
{
	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.View)]
	public async Task<FeeRefundReceiptFileDto> GetReceiptPdfAsync(Guid id)
	{
		// The existing read applies payer scope and permission-based reference masking.
		var refund = await GetAsync(id);
		var sourceIds = refund.Lines.Where(line => line.SourceAllocationId.HasValue)
			.Select(line => line.SourceAllocationId!.Value).Distinct().ToArray();
		if (sourceIds.Length > 0)
		{
			var sources = await allocationRepository.GetListAsync(line => Enumerable.Contains(sourceIds, line.Id));
			var periods = sources.ToDictionary(line => line.Id, line => ClassicReceiptPdfRenderer.Period(
				line.CoverageStartDate, line.CoverageEndDate, line.InstallmentLabel));
			foreach (var line in refund.Lines)
				if (line.SourceAllocationId.HasValue && periods.TryGetValue(line.SourceAllocationId.Value, out var period))
					line.InstallmentLabel = period;
		}
		var studentFacts = refund.StudentId.HasValue ? await AsyncExecuter.FirstOrDefaultAsync((await LazyServiceProvider
			.LazyGetRequiredService<IStudentRepository>().GetQueryableWithoutAutoIncludesAsync())
			.Where(student => student.Id == refund.StudentId).Select(student => new { student.GrNumber })) : null;
		var sourceReceipt = refund.OriginalReceiptId.HasValue
			? await feePaymentReceiptRepository.FindAsync(refund.OriginalReceiptId.Value) : null;
		var sourceCollection = sourceReceipt == null ? null
			: await collectionRepository.FindAsync(sourceReceipt.FeeCollectionTransactionId);
		var account = refund.FinanceFeeAccountId.HasValue
			? await accountRepository.GetAsync(refund.FinanceFeeAccountId.Value) : null;
		var organizationId = ReceiptCollectionSnapshot.ReadOrganization(sourceCollection) ?? account?.MediumStageId;
		Guid? boardId = sourceReceipt?.BoardId ?? account?.BoardId;
		Guid? mediumId = sourceReceipt?.EducationMediumId ?? account?.EducationMediumId;
		if (boardId == Guid.Empty) boardId = null;
		if (mediumId == Guid.Empty) mediumId = null;
		var school = await LazyServiceProvider.LazyGetRequiredService<IReceiptSchoolIdentityReader>()
			.ReadAsync(boardId, mediumId, sourceReceipt?.AcademicYearId ?? account?.AcademicYearId ?? refund.AcademicYearId, organizationId);
		var model = RefundReceiptDocumentFactory.Create(refund, school, studentFacts?.GrNumber);
		var number = Regex.Replace(model.Number, "[^A-Za-z0-9._-]", "-");
		var prefix = refund.Status == FeeRefundStatus.Processing ? "Provisional-Refund-Receipt" : "Refund-Receipt";
		return new(ClassicReceiptPdfRenderer.Render(model), $"{prefix}_{number}_{model.Date:yyyy-MM-dd}.pdf");
	}
}
