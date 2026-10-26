using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Finance.Fees.ConcessionRequests;
using ShikshaMitra.Finance.Fees.Concessions;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.FeeAdjustment.Aggregates;
using ShikshaMitra.Finance.Fees.FeeRefund;
using ShikshaMitra.Finance.Fees.Receipts;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Permissions = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Finance.SelfService;

namespace ShikshaMitra.Finance.Mobile;

public partial class MobileFinanceReadService
{
	[Authorize(Permissions.View)]
	public async Task<MobileFinancePage<MobileFinancePayment>> PaymentsAsync(Guid studentId, Guid academicYearId, int page = 1, int pageSize = 20, CancellationToken cancellationToken = default, string? status = null)
	{
		await access.RequireAsync(studentId,academicYearId,cancellationToken:cancellationToken); ValidatePage(page,pageSize);
		var query=(await transactions.GetQueryableAsync()).Where(x=>x.StudentId==studentId&&x.AcademicYearId==academicYearId);
		if(!string.IsNullOrWhiteSpace(status)&&!string.Equals(status,"all",StringComparison.OrdinalIgnoreCase))
		{
			if(string.Equals(status,"Rejected",StringComparison.OrdinalIgnoreCase))status="Reversed";
			if(!Enum.TryParse<FeeCollectionStatus>(status,true,out var filter)||!Enum.IsDefined(filter))throw new BusinessException("Finance:SelfService:InvalidRequest");
			query=query.Where(x=>x.Status==filter);
		}
		var count=await AsyncExecuter.CountAsync(query,cancellationToken);
		var rows=await AsyncExecuter.ToListAsync(query.OrderByDescending(x=>x.CreationTime).ThenBy(x=>x.Id).Skip(Skip(page,pageSize)).Take(pageSize),cancellationToken);
		return new(await PaymentDtosAsync(rows,studentId,academicYearId,cancellationToken),count,page,pageSize);
	}
	private async Task<List<MobileFinancePayment>> PaymentDtosAsync(List<FeeRecordMetadata> rows,Guid studentId,Guid academicYearId,CancellationToken cancellationToken)
	{
		var ids=rows.Select(x=>x.Id).ToArray();
		var splits=await methods.GetListAsync(x=>x.StudentId==studentId&&Enumerable.Contains(ids,x.FeeRecordMetadataId),cancellationToken:cancellationToken);
		var documents=await receipts.GetListAsync(x=>x.StudentId==studentId&&x.AcademicYearId==academicYearId&&Enumerable.Contains(ids,x.FeeCollectionTransactionId),cancellationToken:cancellationToken);
		var documentMap=await ReceiptDtosAsync(documents,studentId,academicYearId,cancellationToken);
		return rows.Select(x=>new MobileFinancePayment(x.Id,DateOnly.FromDateTime(x.CreationTime),
			MobileFinanceProjection.Money(MobileFinanceProjection.PaymentAmount(x.TotalPaidFee,splits.Where(s=>s.FeeRecordMetadataId==x.Id).Select(s=>s.Amount).ToList())),x.Status.ToString(),
			splits.Where(s=>s.FeeRecordMetadataId==x.Id).Select(s=>s.PaymentMethod.ToString()).Distinct().ToList(),
			documents.Where(r=>r.FeeCollectionTransactionId==x.Id).Select(r=>documentMap[r.Id]).ToList())).ToList();
	}
	[Authorize(Permissions.View)]
	public async Task<MobileFinancePage<MobileFinanceReceipt>> ReceiptsAsync(Guid studentId, Guid academicYearId, int page = 1, int pageSize = 20, CancellationToken cancellationToken = default)
	{
		await access.RequireAsync(studentId,academicYearId,cancellationToken:cancellationToken); ValidatePage(page,pageSize);
		var query=(await receipts.GetQueryableAsync()).Where(x=>x.StudentId==studentId&&x.AcademicYearId==academicYearId);
		var count=await AsyncExecuter.CountAsync(query,cancellationToken);
		var rows=await AsyncExecuter.ToListAsync(query.OrderByDescending(x=>x.ReceiptDate).ThenBy(x=>x.Id).Skip(Skip(page,pageSize)).Take(pageSize),cancellationToken);
		var mapped=await ReceiptDtosAsync(rows,studentId,academicYearId,cancellationToken);
		return new(rows.Select(x=>mapped[x.Id]).ToList(),count,page,pageSize);
	}
	[Authorize(Permissions.DownloadReceipts)]
	public async Task<byte[]> ReceiptPdfAsync(Guid studentId, Guid academicYearId, Guid receiptId, CancellationToken cancellationToken = default)
	{
		var context=await access.RequireAsync(studentId,academicYearId,permission:Permissions.DownloadReceipts,cancellationToken:cancellationToken);
		var receipt=await receipts.FindAsync(receiptId,cancellationToken:cancellationToken) ?? throw new Volo.Abp.Authorization.AbpAuthorizationException();
		MobileFinanceProjection.RequireReceiptOwnership(receipt,studentId,academicYearId,CurrentTenant.Id);
		if(!await transactions.AnyAsync(x=>x.Id==receipt.FeeCollectionTransactionId&&x.StudentId==studentId&&x.AcademicYearId==academicYearId,cancellationToken:cancellationToken))
			throw new Volo.Abp.Authorization.AbpAuthorizationException();
		// Reuse the official generator without invoking a staff-authorized service or writing a read-time cache.
		return await receiptPdf.GenerateAsync(receipt,context.Student,context.AcademicYears.Single(x=>x.Id==academicYearId),cancellationToken);
	}
	[Authorize(Permissions.View)]
	public async Task<MobileFinancePage<MobileFinanceRequest>> RequestsAsync(Guid studentId, Guid academicYearId, string? kind = null, int page = 1, int pageSize = 20, CancellationToken cancellationToken = default)
	{
		var context=await access.RequireAsync(studentId,academicYearId,cancellationToken:cancellationToken); ValidatePage(page,pageSize);
		if (kind is not (null or "concession" or "refund" or "adjustment")) throw new BusinessException("Finance:SelfService:InvalidRequest");
		var result=new List<MobileFinanceRequest>();
		if (kind is null or "concession")
		{
			var repository=LazyServiceProvider.LazyGetRequiredService<IConcessionRequestRepository>();
			var rows=await AsyncExecuter.ToListAsync((await repository.GetQueryableAsync())
				.Where(x=>x.StudentId==studentId&&x.AcademicYearId==academicYearId&&(x.Status!=ConcessionRequestStatus.Draft||x.CreatorId==context.UserId))
				.Select(x=>new{x.Id,x.Status,x.CreatorId,x.CreationTime,x.Remarks,Amount=x.ConcessionFeeHeadMappings.Sum(m=>m.FeeHeadConcessionAmount)}),cancellationToken);
			var canRequest=context.IsGuardian&&await AuthorizationService.IsGrantedAsync(Permissions.RequestConcession);
			result.AddRange(rows.Select(x=>new MobileFinanceRequest(x.Id,"concession","Concession request",x.Status.ToString(),MobileFinanceProjection.Money(x.Amount),Utc(x.CreationTime),
				context.IsGuardian&&x.CreatorId==context.UserId?x.Remarks:null,canRequest&&x.CreatorId==context.UserId
					?x.Status==ConcessionRequestStatus.Draft?["edit","submit","cancel"]:x.Status==ConcessionRequestStatus.Pending?["cancel"]:[]:[])));
		}
		if (kind is null or "refund")
		{
			var repository=LazyServiceProvider.LazyGetRequiredService<IRepository<FeeRefundRequest,Guid>>();
			var rows=await repository.GetListAsync(x=>x.StudentId==studentId&&x.AcademicYearId==academicYearId&&(x.Status!=FeeRefundStatus.Draft||x.CreatorId==context.UserId),cancellationToken:cancellationToken);
			result.AddRange(rows.Select(x=>new MobileFinanceRequest(x.Id,"refund","Refund request",x.Status.ToString(),
				MobileFinanceProjection.Money(MobileFinanceProjection.RefundRequestAmount(x.TotalAmount,x.CalculatedAmount,
					!x.RefundPolicyVersionId.HasValue&&!x.ConcessionRequestId.HasValue)),Utc(x.CreationTime),context.IsGuardian&&x.CreatorId==context.UserId?x.Reason:null,[])));
		}
		if (kind is null or "adjustment")
		{
			var repository=LazyServiceProvider.LazyGetRequiredService<IRepository<FeeAdjustmentRequest,Guid>>();
			var rows=await repository.GetListAsync(x=>x.StudentId==studentId&&x.AcademicYearId==academicYearId,cancellationToken:cancellationToken);
			result.AddRange(rows.Select(x=>new MobileFinanceRequest(x.Id,"adjustment","Adjustment request",x.Status.ToString(),MobileFinanceProjection.Money(MobileFinanceProjection.LegacyMoney(x.RequestedAmount)),Utc(x.CreationTime),context.IsGuardian&&x.CreatorId==context.UserId?x.Reason:null,[])));
		}
		return new(result.OrderByDescending(x=>x.Date).ThenBy(x=>x.Id).Skip(Skip(page,pageSize)).Take(pageSize).ToList(),result.Count,page,pageSize);
	}
	private async Task<Dictionary<Guid,MobileFinanceReceipt>> ReceiptDtosAsync(List<FeePaymentReceipt> rows,Guid studentId,Guid yearId,CancellationToken ct)
	{
		var linkIds=rows.SelectMany(x=>new[]{x.LinkedFinalReceiptId,x.LinkedProvisionalReceiptId}).Where(x=>x.HasValue).Select(x=>x!.Value).Distinct().ToArray();
		var linked=(await receipts.GetListAsync(x=>x.StudentId==studentId&&x.AcademicYearId==yearId&&Enumerable.Contains(linkIds,x.Id),cancellationToken:ct)).Select(x=>x.Id).ToHashSet();
		var txIds=rows.Select(x=>x.FeeCollectionTransactionId).Distinct().ToArray();
		var ownedTransactions=(await transactions.GetListAsync(x=>x.StudentId==studentId&&x.AcademicYearId==yearId&&Enumerable.Contains(txIds,x.Id),cancellationToken:ct)).Select(x=>x.Id).ToHashSet();
		return rows.ToDictionary(x=>x.Id,x=>
		{
			var dto=MobileFinanceProjection.Receipt(x);
			return dto with{PaymentId=ownedTransactions.Contains(x.FeeCollectionTransactionId)?x.FeeCollectionTransactionId:null,
				LinkedReceiptId=dto.LinkedReceiptId.HasValue&&linked.Contains(dto.LinkedReceiptId.Value)?dto.LinkedReceiptId:null};
		});
	}
	private static DateTimeOffset Utc(DateTime value)=>new(DateTime.SpecifyKind(value,DateTimeKind.Utc));
	private static int Skip(int page,int size)=>(int)Math.Min((long)(page-1)*size,int.MaxValue);
	private static void ValidatePage(int page,int pageSize)
	{
		if(page<1||pageSize<1||pageSize>100) throw new BusinessException("Finance:SelfService:InvalidRequest");
	}
}
