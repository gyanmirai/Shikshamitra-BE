using System;
using System.Data;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Domain.Finance.Policies;
using ShikshaMitra.Finance.Audit;
using ShikshaMitra.Finance.Fees.Charges;
using ShikshaMitra.Library;
using ShikshaMitra.Library.Books;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;

namespace ShikshaMitra.Finance.ExternalReceivables;

[RemoteService(false)]
public class FinanceExternalReceivableAppService(
	IFinanceExternalReceivableSourceReader sourceReader,
	IFinanceExternalReceivableSourceBinding sourceBinding,
	IPermissionChecker permissions,
	FinanceScopeAccessService scope,
	ILibraryInventoryLock inventoryLock,
	IRepository<FinanceExternalReceivableSourceLink, Guid> links,
	IRepository<FinanceExternalReceivableCommand, Guid> commands,
	IRepository<Charge, Guid> charges,
	IRepository<FinancePolicyVersion, Guid> policyVersions,
	ChargeRequestManager chargeManager,
	FinanceExternalReceivableProjection projection,
	FinanceAuditWriter audit,
	IUnitOfWorkManager unitOfWorkManager)
	: ApplicationService, IFinanceExternalReceivableAppService
{
	[UnitOfWork(IsDisabled = true)]
	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Charges.Add)]
	public async Task<FinanceExternalReceivableDto> PostAsync(PostFinanceExternalReceivableInput input)
	{
		using var uow = unitOfWorkManager.Begin(new AbpUnitOfWorkOptions
			{ IsTransactional = true, IsolationLevel = IsolationLevel.Serializable }, requiresNew: true);
		await RequireActionAsync(ShikshaMitraSubmodulePermissions.Finance.Fees_Charges.Add);
		FinanceExternalReceivableSourceGuard.ValidateReference(input.Source);
		var key = input.IdempotencyKey?.Trim();
		if (string.IsNullOrWhiteSpace(key) || key.Length > 100)
			throw new BusinessException(LibraryFinanceErrorCodes.IdempotencyConflict);
		if (input.FinanceChargeCategoryId == Guid.Empty)
			throw new BusinessException(LibraryFinanceErrorCodes.InvalidFinanceMapping);
		await inventoryLock.AcquireAsync(CurrentTenant.Id);
		var facts = await ReadSourceAsync(input.Source);
		var sourceFingerprint = FinanceExternalReceivableFingerprint.Source(facts);
		var commandFingerprint = FinanceExternalReceivableFingerprint.PostCommand(input, sourceFingerprint);
		var replay = await commands.FindAsync(row => row.TenantId == CurrentTenant.Id &&
			row.Operation == "Post" && row.IdempotencyKey == key);
		if (replay != null)
		{
			if (replay.CommandFingerprint != commandFingerprint)
				throw new BusinessException(LibraryFinanceErrorCodes.IdempotencyConflict);
			var original = await links.GetAsync(replay.SourceLinkId);
			await EnsureSameSourceAsync(original, input, sourceFingerprint);
			var result = await projection.ReadAsync(original, facts);
			await uow.CompleteAsync();
			return result;
		}
		var link = await links.FindAsync(row => row.TenantId == CurrentTenant.Id &&
			row.SourceKind == input.Source.Kind && row.SourceId == input.Source.SourceId);
		if (link == null)
		{
			await ValidatePolicyAsync(input.CollectionPolicyVersionId, "collection", facts.AcademicYearId, facts.Payer.Kind);
			await ValidatePolicyAsync(input.RefundPolicyVersionId, "refund", facts.AcademicYearId, facts.Payer.Kind);
			var snapshot = JsonSerializer.Serialize(facts);
			var payer = new FinanceExternalPayerIdentity(facts.Payer.Kind,
				facts.Payer.StudentId ?? facts.Payer.EmployeeId!.Value);
			var created = await chargeManager.CreateExternalAssessmentAsync(CurrentTenant.Id!.Value,
				facts.AcademicYearId, input.FinanceChargeCategoryId, payer, facts.Amount,
				facts.AssessmentDate, facts.DueDate, facts.Reason, snapshot, CurrentUser.Id!.Value);
			link = new FinanceExternalReceivableSourceLink(GuidGenerator.Create(), CurrentTenant.Id.Value,
				input.Source.Kind, input.Source.SourceId, facts.Source.SourceVersion, sourceFingerprint, snapshot,
				created.Request.Id, created.Charge.Id, facts.AcademicYearId, facts.Currency,
				input.CollectionPolicyVersionId, input.RefundPolicyVersionId);
			await links.InsertAsync(link, autoSave: false);
			await uow.SaveChangesAsync();
			await sourceBinding.BindAsync(input.Source, link.Id);
			await audit.WriteAsync(facts.AcademicYearId, FinanceAuditEntityTypes.FeeCharge,
				created.Request.Id, created.Request.ChargeName, FinanceAuditActions.AddCharge,
				"Created a Finance charge from a reviewed Library assessment.", reason: facts.Reason,
				context: new { input.Source, SourceLinkId = link.Id, facts.Payer.Kind,
					facts.Payer.StudentId, facts.Payer.EmployeeId }, correlationId: $"external-{link.Id:N}");
		}
		else
			await EnsureSameSourceAsync(link, input, sourceFingerprint);
		await commands.InsertAsync(new FinanceExternalReceivableCommand(GuidGenerator.Create(),
			CurrentTenant.Id!.Value, "Post", key, commandFingerprint, link.Id, CurrentUser.Id!.Value), autoSave: false);
		await uow.SaveChangesAsync();
		var output = await projection.ReadAsync(link, facts);
		await uow.CompleteAsync();
		return output;
	}

	[UnitOfWork(IsDisabled = true)]
	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Charges.View)]
	public async Task<FinanceExternalReceivableDto> GetAsync(FinanceExternalSourceReference source)
	{
		using var uow = unitOfWorkManager.Begin(isTransactional: false, requiresNew: true);
		await RequireActionAsync(ShikshaMitraSubmodulePermissions.Finance.Fees_Charges.View);
		var facts = await ReadSourceAsync(source);
		var link = await links.FindAsync(row => row.TenantId == CurrentTenant.Id &&
			row.SourceKind == source.Kind && row.SourceId == source.SourceId)
			?? throw new BusinessException(LibraryFinanceErrorCodes.InvalidSource, "The assessment has no Finance obligation.");
		if (link.SourceFingerprint != FinanceExternalReceivableFingerprint.Source(facts))
			throw new BusinessException(LibraryFinanceErrorCodes.SourceChanged);
		var result = await projection.ReadAsync(link, facts);
		await uow.CompleteAsync();
		return result;
	}

	private async Task RequireActionAsync(string action)
	{
		if (!CurrentTenant.Id.HasValue || !CurrentUser.Id.HasValue || !await permissions.IsGrantedAsync(action))
			throw new AbpAuthorizationException("External receivable action access denied.");
	}

	private async Task<FinanceExternalReceivableSourceFacts> ReadSourceAsync(FinanceExternalSourceReference source)
	{
		FinanceExternalReceivableSourceGuard.ValidateReference(source);
		var facts = await sourceReader.ReadAuthorizedAsync(source);
		FinanceExternalReceivableSourceGuard.ValidateFacts(source, facts, CurrentTenant.Id);
		await scope.EnsureCollectionYearAccessibleAsync(facts.AcademicYearId);
		return facts;
	}

	private async Task EnsureSameSourceAsync(FinanceExternalReceivableSourceLink link,
		PostFinanceExternalReceivableInput input, string sourceFingerprint)
	{
		if (link.TenantId != CurrentTenant.Id || link.SourceKind != input.Source.Kind || link.SourceId != input.Source.SourceId ||
			link.SourceFingerprint != sourceFingerprint || link.CollectionPolicyVersionId != input.CollectionPolicyVersionId ||
			link.RefundPolicyVersionId != input.RefundPolicyVersionId ||
			(await charges.GetAsync(link.FinanceChargeId)).ChargeCategoryId != input.FinanceChargeCategoryId)
			throw new BusinessException(LibraryFinanceErrorCodes.SourceAlreadyLinked);
	}

	private async Task ValidatePolicyAsync(Guid? id, string kind, Guid yearId, FinanceExternalPayerKind payerKind)
	{
		if (!id.HasValue) return;
		var version = await policyVersions.FindAsync(id.Value);
		if (version == null || version.TenantId != CurrentTenant.Id || version.AcademicYearId != yearId ||
			version.PolicyType != kind || !string.Equals(version.Status, "Active", StringComparison.OrdinalIgnoreCase))
			throw new BusinessException(LibraryFinanceErrorCodes.InvalidFinancePolicy);
		if (kind == "refund") FinanceExternalSettlementPolicyRules.EnsureRefundCreationCompatible(version, payerKind);
	}
}
