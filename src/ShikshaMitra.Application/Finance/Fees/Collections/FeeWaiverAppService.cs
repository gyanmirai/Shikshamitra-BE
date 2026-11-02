using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Domain.Finance.Policies;
using ShikshaMitra.Finance.Audit;
using ShikshaMitra.Finance.Common;
using ShikshaMitra.Finance.Fees.Charges;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment;
using System;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;

namespace ShikshaMitra.Finance.Fees.FeeCollection;

/// <summary>
/// Applies an immediate, full-balance waiver from Fee Collection. The client identifies
/// the source and supplies a reason; the server owns eligibility and amount calculation.
/// </summary>
[RemoteService(false)]
[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_FeeCollections.ApplyWaiver)]
public class FeeWaiverAppService(
	IDueFeeRepository dueFeeRepository,
	IRepository<Charge, Guid> chargeRepository,
	IRepository<FeeHead, Guid> feeHeadRepository,
	IRepository<StudentFeeStructure, Guid> studentFeeStructureRepository,
	IRepository<LateFeeWaiverApplication, Guid> lateFeeWaiverRepository,
	IRepository<ChargeWaiverApplication, Guid> chargeWaiverRepository,
	IRepository<ChargeCollectionRecord, Guid> chargeCollectionRepository,
	IRepository<FeeRecordMetadata, Guid> feeRecordMetadataRepository,
	CollectionPolicyResolver policyResolver,
	LateFeeBalanceService lateFeeBalanceService,
	FinanceScopeAccessService financeScopeAccessService,
	FinanceAuditWriter financeAuditWriter,
	IUnitOfWorkManager unitOfWorkManager
) : ApplicationService, IFeeWaiverAppService
{
	public async Task<FeeWaiverResultDto> ApplyAsync(ApplyFeeWaiverInput input)
	{
		var sourceType = NormalizeSourceType(input.SourceType);
		var reason = input.Reason?.Trim();
		var idempotencyKey = input.IdempotencyKey?.Trim();
		if (string.IsNullOrWhiteSpace(reason)) throw new BusinessException(FeeWaiverErrorCodes.ReasonRequired);
		if (string.IsNullOrWhiteSpace(idempotencyKey)) throw new BusinessException(FeeWaiverErrorCodes.IdempotencyKeyRequired);
		if (input.SourceId == Guid.Empty) throw new BusinessException(FeeWaiverErrorCodes.SourceNotFound);

		using var uow = unitOfWorkManager.Begin(new AbpUnitOfWorkOptions
			{ IsTransactional = true, IsolationLevel = IsolationLevel.Serializable }, requiresNew: true);

		var actorId = CurrentUser.Id ?? Guid.Empty;
		var replay = await FindReplayAsync(actorId, idempotencyKey, sourceType, input.SourceId);
		if (replay is not null)
		{
			await uow.CompleteAsync();
			return replay;
		}

		var result = sourceType == "LateFee"
			? await ApplyLateFeeAsync(input.SourceId, reason, idempotencyKey, actorId)
			: await ApplyChargeAsync(input.SourceId, reason, idempotencyKey, actorId);

		await uow.CompleteAsync();
		return result;
	}

	private async Task<FeeWaiverResultDto> ApplyLateFeeAsync(
		Guid dueFeeId, string reason, string idempotencyKey, Guid actorId)
	{
		var dueFee = await dueFeeRepository.FindAsync(dueFeeId)
			?? throw new BusinessException(FeeWaiverErrorCodes.SourceNotFound).WithData("sourceId", dueFeeId);
		await financeScopeAccessService.EnsureCollectionPayerAccessibleAsync(dueFee.StudentId, dueFee.FinanceFeeAccountId);
		if (!dueFee.StudentId.HasValue)
			throw new BusinessException(FeeWaiverErrorCodes.SourceNotEligible).WithData("reason", "Waivers require a Student fee assignment.");

		var feeHead = await feeHeadRepository.FindAsync(dueFee.FeeHeadId)
			?? throw new BusinessException(FeeWaiverErrorCodes.SourceNotFound).WithData("feeHeadId", dueFee.FeeHeadId);
		var assignment = await studentFeeStructureRepository.FirstOrDefaultAsync(x =>
			x.StudentId == dueFee.StudentId && x.AcademicYearId == feeHead.AcademicYearId);
		if (assignment is null)
			throw new BusinessException(FeeWaiverErrorCodes.SourceNotEligible).WithData("reason", "No fee structure is assigned.");

		var effective = await policyResolver.ResolveEffectiveRulesForStudentAsync(
			dueFee.StudentId.Value, assignment.FeeStructureStandardId);
		if (effective.Policy is null)
			throw new BusinessException(FeeWaiverErrorCodes.SourceNotEligible).WithData("reason", "No late fee applies.");

		var policy = effective.Policy;
		var balances = await lateFeeBalanceService.GetBalancesAsync([dueFee], feeHead.AcademicYearId,
			Clock.Now, preserveAssessment: true);
		var remaining = balances[dueFee.Id].PendingAmount;
		if (remaining <= 0) throw new BusinessException(FeeWaiverErrorCodes.NoRemainingBalance);

		var entity = new LateFeeWaiverApplication(
			GuidGenerator.Create(), CurrentTenant.Id, dueFee.StudentId.Value, dueFee.Id, policy.Id,
			policy.AcademicYearId, remaining, remaining, remaining, reason,
			requiresApproval: false, actorId, idempotencyKey: idempotencyKey);
		await lateFeeWaiverRepository.InsertAsync(entity, autoSave: false);
		await financeAuditWriter.WriteAsync(policy.AcademicYearId, FinanceAuditEntityTypes.LateFeeWaiver,
			entity.Id, $"Late fee waiver on due line {dueFee.Id:N}", FinanceAuditActions.Waive,
			$"Waived the full remaining late-fee balance of {remaining:0.00}.", reason: reason,
			context: new { SourceType = "LateFee", SourceId = dueFee.Id, WaivedAmount = remaining },
			correlationId: $"waiver-{idempotencyKey}");

		return ToResult(entity);
	}

	private async Task<FeeWaiverResultDto> ApplyChargeAsync(
		Guid chargeId, string reason, string idempotencyKey, Guid actorId)
	{
		var charge = await chargeRepository.FindAsync(chargeId)
			?? throw new BusinessException(FeeWaiverErrorCodes.SourceNotFound).WithData("sourceId", chargeId);
		await financeScopeAccessService.EnsureStudentAccessibleAsync(charge.StudentId);
		if (charge.Status != ChargeStatus.Approved)
			throw new BusinessException(FeeWaiverErrorCodes.SourceNotEligible).WithData("status", charge.Status);

		var waivers = await chargeWaiverRepository.GetListAsync(x => x.ChargeId == chargeId);
		var allocations = await chargeCollectionRepository.GetListAsync(x => x.ChargeId == chargeId);
		var transactionIds = allocations.Select(x => x.FeeRecordMetadataId).Distinct().ToList();
		var reversedIds = (await feeRecordMetadataRepository.GetListAsync(x =>
			transactionIds.Contains(x.Id) && x.Status == FeeCollectionStatus.Reversed))
			.Select(x => x.Id).ToHashSet();
		var paid = allocations.Where(x => !reversedIds.Contains(x.FeeRecordMetadataId)).Sum(x => (decimal)x.Amount);
		var remaining = Math.Max(0m, charge.Amount - paid - waivers.Sum(x => x.EffectiveAmount));
		if (remaining <= 0) throw new BusinessException(FeeWaiverErrorCodes.NoRemainingBalance);

		var entity = new ChargeWaiverApplication(GuidGenerator.Create(), CurrentTenant.Id,
			charge.StudentId, charge.Id, charge.AcademicYearId, charge.Amount, remaining,
			reason, actorId, idempotencyKey);
		await chargeWaiverRepository.InsertAsync(entity, autoSave: false);
		await financeAuditWriter.WriteAsync(charge.AcademicYearId, FinanceAuditEntityTypes.FeeCharge,
			entity.Id, $"Charge waiver on charge line {charge.Id:N}", FinanceAuditActions.Waive,
			$"Waived the full remaining charge balance of {remaining:0.00}.", reason: reason,
			context: new { SourceType = "Charge", SourceId = charge.Id, WaivedAmount = remaining },
			correlationId: $"waiver-{idempotencyKey}");

		return ToResult(entity);
	}

	private async Task<FeeWaiverResultDto?> FindReplayAsync(
		Guid actorId, string key, string sourceType, Guid sourceId)
	{
		var late = await lateFeeWaiverRepository.FirstOrDefaultAsync(x =>
			x.AppliedByUserId == actorId && x.IdempotencyKey == key);
		if (late is not null)
		{
			if (sourceType != "LateFee" || late.DueFeeId != sourceId)
				throw new BusinessException(FeeWaiverErrorCodes.IdempotencyConflict);
			return ToResult(late);
		}

		var charge = await chargeWaiverRepository.FirstOrDefaultAsync(x =>
			x.AppliedByUserId == actorId && x.IdempotencyKey == key);
		if (charge is null) return null;
		if (sourceType != "Charge" || charge.ChargeId != sourceId)
			throw new BusinessException(FeeWaiverErrorCodes.IdempotencyConflict);
		return ToResult(charge);
	}

	private static string NormalizeSourceType(string value)
	{
		if (string.Equals(value?.Trim(), "LateFee", StringComparison.OrdinalIgnoreCase)) return "LateFee";
		if (string.Equals(value?.Trim(), "Charge", StringComparison.OrdinalIgnoreCase)) return "Charge";
		throw new BusinessException(FeeWaiverErrorCodes.UnsupportedSource).WithData("sourceType", value!);
	}

	private static FeeWaiverResultDto ToResult(LateFeeWaiverApplication entity) => new()
	{
		Id = entity.Id, SourceType = "LateFee", SourceId = entity.DueFeeId,
		StudentId = entity.StudentId, AcademicYearId = entity.AcademicYearId,
		WaivedAmount = entity.EffectiveAmount, RemainingAmount = 0,
		Reason = entity.Reason, CreationTime = entity.CreationTime
	};

	private static FeeWaiverResultDto ToResult(ChargeWaiverApplication entity) => new()
	{
		Id = entity.Id, SourceType = "Charge", SourceId = entity.ChargeId,
		StudentId = entity.StudentId, AcademicYearId = entity.AcademicYearId,
		WaivedAmount = entity.EffectiveAmount, RemainingAmount = 0,
		Reason = entity.Reason, CreationTime = entity.CreationTime
	};
}
