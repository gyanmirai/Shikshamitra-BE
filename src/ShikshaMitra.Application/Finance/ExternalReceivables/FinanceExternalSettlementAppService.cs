using System;
using System.Data;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Domain.Finance.Accounts.Settings;
using ShikshaMitra.Finance.Accounts;
using ShikshaMitra.Finance.Accounts.CashManagement;
using ShikshaMitra.Finance.Audit;
using ShikshaMitra.Finance.Fees.Charges;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.Receipts;
using ShikshaMitra.Library;
using ShikshaMitra.Library.Books;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;

namespace ShikshaMitra.Finance.ExternalReceivables;

[RemoteService(false)]
public partial class FinanceExternalSettlementAppService(
	FinanceExternalSettlementAuthority authority,
	FinanceExternalSettlementPolicyRules policyRules,
	FinanceExternalChargeBalanceReader balances,
	FinanceExternalReceivableProjection projection,
	IRepository<FinanceExternalSettlementPolicy, Guid> policies,
	IRepository<FinanceExternalReceivableCommand, Guid> commands,
	IRepository<FinanceExternalWaiverRequest, Guid> waiverRequests,
	IRepository<FinanceChargeWaiverReversal, Guid> reversals,
	IRepository<ChargeWaiverApplication, Guid> waivers,
	IRepository<Charge, Guid> charges,
	IRepository<FeePaymentReceipt, Guid> receipts,
	IRepository<CashCollection, Guid> cashCollections,
	FeeCollectionManager collectionManager,
	FinanceRoutingManager routing,
	AutomaticTransactionManager transactions,
	FinanceAuditWriter audit,
	ILibraryInventoryLock inventoryLock,
	IUnitOfWorkManager units) : ApplicationService, IFinanceExternalSettlementAppService
{
	[UnitOfWork(IsDisabled = true)]
	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_FeeCollections.View)]
	public async Task<FinanceExternalCollectionFormDto> GetCollectFormAsync(FinanceExternalSourceReference source)
	{
		using var uow = units.Begin(isTransactional: false, requiresNew: true);
		var context = await authority.RequireAsync(source, ShikshaMitraSubmodulePermissions.Finance.Fees_FeeCollections.View);
		var form = await FormAsync(context);
		await uow.CompleteAsync();
		return form;
	}

	[UnitOfWork(IsDisabled = true)]
	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Charges.Update)]
	public Task<FinanceExternalCollectionFormDto> BindPolicyAsync(BindFinanceExternalSettlementPolicyInput input) =>
		CommandAsync(input, "BindPolicy", ShikshaMitraSubmodulePermissions.Finance.Fees_Charges.Update,
			async (context, replay) =>
			{
				if (replay == null)
				{
					await EnsureRevisionAsync(context, input.Revision);
					await policyRules.ReadCollectionAsync(context, input.CollectionPolicyVersionId);
					await policyRules.ValidateRefundCreationAsync(context, input.RefundPolicyVersionId);
					if (await BindingAsync(context) != null) throw new BusinessException(LibraryFinanceErrorCodes.SourceAlreadyLinked);
					var binding = new FinanceExternalSettlementPolicy(GuidGenerator.Create(), CurrentTenant.Id!.Value,
						context.Link.Id, input.CollectionPolicyVersionId, input.RefundPolicyVersionId, context.ActorId, input.Reason);
					await policies.InsertAsync(binding, autoSave: false);
					await RecordAsync(context, input, "BindPolicy", binding.Id, "SettlementPolicy");
				}
				return await FormAsync(context);
			});

	[UnitOfWork(IsDisabled = true)]
	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Charges.Approve)]
	public Task<FinanceExternalReceivableDto> ApproveAsync(FinanceExternalApprovalInput input) =>
		CommandAsync(input, "Approve", ShikshaMitraSubmodulePermissions.Finance.Fees_Charges.Approve,
			async (context, replay) =>
			{
				if (replay == null)
				{
					await EnsureRevisionAsync(context, input.Revision);
					FinanceExternalSettlementAuthority.EnsureIndependent(context, context.Charge.RequestorId);
					context.Charge.Approve(context.ActorId, Reason(input.Reason));
					await charges.UpdateAsync(context.Charge, autoSave: false);
					await RecordAsync(context, input, "Approve", context.Charge.Id, "Charge");
				}
				return await projection.ReadAsync(context.Link, context.Source);
			});

	private async Task<T> CommandAsync<T>(FinanceExternalSettlementCommand input, string operation, string action,
		Func<FinanceExternalSettlementContext, FinanceExternalReceivableCommand?, Task<T>> execute)
	{
		using var uow = units.Begin(new AbpUnitOfWorkOptions
			{ IsTransactional = true, IsolationLevel = IsolationLevel.Serializable }, requiresNew: true);
		await inventoryLock.AcquireAsync(CurrentTenant.Id);
		var context = await authority.RequireAsync(input.Source, action);
		var key = input.IdempotencyKey?.Trim();
		if (string.IsNullOrWhiteSpace(key) || key.Length > 100) throw new BusinessException(LibraryFinanceErrorCodes.IdempotencyConflict);
		var replay = await commands.FindAsync(row => row.TenantId == CurrentTenant.Id && row.Operation == operation && row.IdempotencyKey == key);
		if (replay != null && (replay.SourceLinkId != context.Link.Id || replay.CommandFingerprint != Fingerprint(context, input)))
			throw new BusinessException(LibraryFinanceErrorCodes.IdempotencyConflict);
		var result = await execute(context, replay);
		await uow.CompleteAsync();
		return result;
	}

	private async Task RecordAsync(FinanceExternalSettlementContext context, FinanceExternalSettlementCommand input,
		string operation, Guid resultId, string resultType)
	{
		await commands.InsertAsync(new FinanceExternalReceivableCommand(GuidGenerator.Create(), CurrentTenant.Id!.Value,
			operation, input.IdempotencyKey.Trim(), Fingerprint(context, input), context.Link.Id, context.ActorId,
			resultId, resultType), autoSave: false);
		await audit.WriteAsync(context.Link.AcademicYearId, FinanceAuditEntityTypes.FeeCharge,
			context.Charge.Id, context.Charge.ChargeName, operation, $"Recorded Finance source action {operation}.",
			context: new { context.Link.Id, ResultId = resultId, ResultType = resultType, context.Source.Payer },
			correlationId: $"external-{context.Link.Id:N}");
		await units.Current!.SaveChangesAsync();
	}

	private Task<FinanceExternalSettlementPolicy?> BindingAsync(FinanceExternalSettlementContext context) =>
		policies.FindAsync(row => row.TenantId == CurrentTenant.Id && row.SourceLinkId == context.Link.Id);

	private Task<string> RevisionAsync(FinanceExternalSettlementContext context, Guid? bindingId) =>
		LazyServiceProvider.LazyGetRequiredService<FinanceExternalRevisionReader>().ReadAsync(context, bindingId);

	private async Task EnsureRevisionAsync(FinanceExternalSettlementContext context, string revision) =>
		FinanceExternalBalanceRevision.EnsureCurrent(revision, await RevisionAsync(context, (await BindingAsync(context))?.Id));
	private static string Fingerprint(FinanceExternalSettlementContext context, FinanceExternalSettlementCommand input) =>
		Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(context.Link.SourceFingerprint + JsonSerializer.Serialize(input, input.GetType())))).ToLowerInvariant();
	private static string Reason(string reason) => !string.IsNullOrWhiteSpace(reason) && reason.Trim().Length <= 1000
		? reason.Trim() : throw new BusinessException(LibraryFinanceErrorCodes.InvalidSource, "A recorded reason is required.");
}
