using System;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Finance.Audit;
using ShikshaMitra.Finance.Fees.FeeRefund;
using ShikshaMitra.Finance.Fees.FeeRefund.Dtos;
using ShikshaMitra.Finance.Fees.FeeRefund.Repositories;
using ShikshaMitra.Finance.Fees.Receipts;
using ShikshaMitra.Library.Books;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;

namespace ShikshaMitra.Finance.ExternalReceivables;

[RemoteService(false)]
public partial class FinanceExternalRefundAppService(FinanceExternalSettlementAuthority authority,
	FinanceExternalRefundSourceReader sources, FinanceExternalReceivableProjection projection,
	IRepository<FinanceExternalSettlementPolicy, Guid> bindings, IFeeRefundRepository requests,
	FeeRefundManager manager, FinanceAuditWriter audit, ILibraryInventoryLock inventoryLock,
	IUnitOfWorkManager units) : ApplicationService, IFinanceExternalRefundAppService
{
	private const string FingerprintProperty = "ExternalRefundCommandFingerprintV1";

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.View)]
	[UnitOfWork(IsDisabled = true)]
	public async Task<FinanceExternalRefundFormDto> GetFormAsync(FinanceExternalSourceReference source)
	{
		using var unit = units.Begin(requiresNew: true, isTransactional: false);
		var context = await authority.RequireAsync(source, ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.View);
		var prepared = await ReadFormAsync(context);
		await unit.CompleteAsync();
		return prepared.Form;
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.Add)]
	[UnitOfWork(IsDisabled = true)]
	public async Task<FeeRefundPreviewDto> PreviewAsync(FinanceExternalRefundSelection input)
	{
		using var unit = units.Begin(requiresNew: true, isTransactional: false);
		var context = await authority.RequireAsync(input.Source, ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.Add);
		var prepared = await PrepareAsync(context, input);
		await unit.CompleteAsync();
		return Preview(prepared);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.Add)]
	[UnitOfWork(IsDisabled = true)]
	public async Task<FeeRefundRequestDto> CreateAsync(FinanceExternalRefundRequestInput input)
	{
		using var unit = units.Begin(new AbpUnitOfWorkOptions
			{ IsTransactional = true, IsolationLevel = IsolationLevel.Serializable }, requiresNew: true);
		await inventoryLock.AcquireAsync(CurrentTenant.Id);
		var context = await authority.RequireAsync(input.Source, ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.Add);
		if (string.IsNullOrWhiteSpace(input.IdempotencyKey) || input.IdempotencyKey.Length > 100 ||
			string.IsNullOrWhiteSpace(input.Reason) || input.Reason.Trim().Length > 1000)
			throw new BusinessException(FeeRefundErrorCodes.IdempotencyConflict);
		var fingerprint = RequestFingerprint(context, input);
		var prior = await requests.FindAsync(row => row.TenantId == CurrentTenant.Id && row.RequestIdempotencyKey == input.IdempotencyKey.Trim(),
			includeDetails: false);
		if (prior != null)
		{
			if (prior.FinanceExternalSourceLinkId != context.Link.Id || prior.GetProperty<string>(FingerprintProperty) != fingerprint)
				throw new BusinessException(FeeRefundErrorCodes.IdempotencyConflict);
			var replay = await requests.FindWithLinesAsync(prior.Id) ?? throw new BusinessException(FeeRefundErrorCodes.RefundNotFound);
			var output = Map(replay, context);
			await unit.CompleteAsync();
			return output;
		}
		var prepared = await PrepareAsync(context, input);
		var payoutMethod = CanonicalPayoutMethod(input.Method);
		var receipt = await LazyServiceProvider.LazyGetRequiredService<IRepository<FeePaymentReceipt, Guid>>()
			.GetAsync(input.OriginalReceiptId);
		var payer = new FinanceExternalPayerIdentity(context.Source.Payer.Kind,
			context.Source.Payer.StudentId ?? context.Source.Payer.EmployeeId!.Value);
		var request = await manager.CreateExternalPendingAsync(context.Link, context.Charge, payer, prepared.Version,
			receipt, prepared.Lines, payoutMethod, input.PaymentDetails == null ? null : FeeRefundManager.MapRefundPaymentDetails(payoutMethod, input.PaymentDetails),
			input.Reason.Trim(), prepared.CalculationJson, prepared.CalculationHash, context.ActorId, input.IdempotencyKey.Trim());
		request.SetProperty(FingerprintProperty, fingerprint);
		await units.Current!.SaveChangesAsync();
		await audit.WriteAsync(context.Link.AcademicYearId, FinanceAuditEntityTypes.FeeRefund, request.Id, request.VoucherNumber,
			FinanceAuditActions.Create, "Submitted a source-bound refund request for Finance review.", reason: input.Reason.Trim(),
			context: new { SourceLinkId = context.Link.Id, request.OriginalReceiptId, request.RefundPolicyVersionId,
				request.StudentId, request.EmployeeId, request.TotalAmount, request.CalculatedAmount });
		await unit.CompleteAsync();
		return Map(request, context);
	}
}
