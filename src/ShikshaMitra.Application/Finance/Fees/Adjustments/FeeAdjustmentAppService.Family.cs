using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Finance.Audit;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Mobile;
using Volo.Abp;
using Volo.Abp.Uow;

namespace ShikshaMitra.Finance.Fees.FeeAdjustment;

[Volo.Abp.DependencyInjection.ExposeServices(typeof(FeeAdjustmentAppService), typeof(IFeeAdjustmentAppService), typeof(IFamilyAdjustmentService))]
public partial class FeeAdjustmentAppService : IFamilyAdjustmentService
{
	private Task<MobileFinanceAccessContext> RequireFamilyAdjustmentAsync(Guid child, Guid year) =>
		LazyServiceProvider.LazyGetRequiredService<MobileFinanceAccessService>().RequireAsync(child, year,
			guardianOnly: true, permission: MobileFinancePermissions.RequestAdjustment);

	private async Task<List<(DueFee Due, decimal Available)>> FamilyAdjustmentSourcesAsync(Guid child, Guid year)
	{
		var yearHeadIds = (await feeHeadRepository.GetQueryableAsync()).Where(x => x.AcademicYearId == year).Select(x => x.Id);
		var rows = await AsyncExecuter.ToListAsync((await dueFeeRepository.GetQueryableAsync())
			.Where(x => x.StudentId == child && yearHeadIds.Contains(x.FeeHeadId)));
		var pending = await feeAdjustmentRepository.GetPendingSourceAmountsByDueFeeAsync(child);
		return rows.Select(x => (Due: x, Available: Math.Max(0m, x.AdjustableBalance - Convert.ToDecimal(pending.GetValueOrDefault(x.Id)))))
			.Where(x => x.Available > 0m).ToList();
	}

	public virtual async Task<MobileAdjustmentEligibility> FamilyAdjustmentEligibilityAsync(Guid child, Guid year)
	{
		await RequireFamilyAdjustmentAsync(child, year);
		return new((await FamilyAdjustmentSourcesAsync(child, year)).Select(x => new MobileAdjustmentSource(x.Due.Id,x.Due.FeeHeadId,x.Due.GetDisplayInstallmentLabel(),MobileFinanceCommandRules.Money(x.Available))).ToList());
	}

	private async Task<(string Fingerprint, List<(Guid DueFeeId, double Amount)> Lines, decimal Amount)> PrepareFamilyAdjustmentAsync(Guid child, Guid year, MobileAdjustmentInput input)
	{
		if (input.SourceLines.Count is 0 or > 100 || input.SourceLines.Select(x => x.DueFeeId).Distinct().Count() != input.SourceLines.Count ||
			string.IsNullOrWhiteSpace(input.Reason) || input.Reason.Length is < 5 or > 1000)
			throw new BusinessException(FeeAdjustmentErrorCodes.SourceLineInvalid);
		var sources = (await FamilyAdjustmentSourcesAsync(child, year)).ToDictionary(x => x.Due.Id);
		var lines = input.SourceLines.Select(x =>
		{
			var amount = MobileFinanceCommandRules.ParseMoney(x.Amount);
			if (!sources.TryGetValue(x.DueFeeId, out var source) || amount > source.Available)
				throw new BusinessException(FeeAdjustmentErrorCodes.SourceExceedsAdjustableBalance);
			return new { x.DueFeeId, Amount = amount, source.Available, source.Due.ConcurrencyStamp };
		}).OrderBy(x => x.DueFeeId).ToList();
		var evidence = lines.Select(x => new { x.DueFeeId,
			Amount = MobileFinanceCommandRules.Money(x.Amount),
			Available = MobileFinanceCommandRules.Money(x.Available), x.ConcurrencyStamp }).ToList();
		return (MobileFinanceCommandRules.Hash(new { child, year, input.Reason, lines = evidence }),
			lines.Select(x => (x.DueFeeId, Convert.ToDouble(x.Amount))).ToList(), lines.Sum(x => x.Amount));
	}

	public virtual async Task<MobileAdjustmentPreview> FamilyAdjustmentPreviewAsync(Guid child, Guid year, MobileAdjustmentInput input)
	{
		await RequireFamilyAdjustmentAsync(child, year);
		var prepared = await PrepareFamilyAdjustmentAsync(child, year, input);
		return new(prepared.Fingerprint,MobileFinanceCommandRules.Money(prepared.Amount));
	}

	[UnitOfWork(IsDisabled = true)]
	public virtual async Task<MobileFinanceCommandResult> FamilyAdjustmentCreateAsync(Guid child, Guid year, MobileAdjustmentInput input)
	{
		await RequireFamilyAdjustmentAsync(child, year);
		using var uow = unitOfWorkManager.Begin(new AbpUnitOfWorkOptions { IsTransactional = true, IsolationLevel = IsolationLevel.Serializable }, requiresNew: true);
		var store = LazyServiceProvider.LazyGetRequiredService<MobileFinanceCommandStore>();
		var payload = new { child, year, input };
		var replay = await store.ReplayAsync("adjustment_create", input.IdempotencyKey, payload);
		if (replay.HasValue)
		{
			var saved = await feeAdjustmentRepository.GetAsync(replay.Value);
			MobileFinanceCommandRules.RequireOwner(child, year, CurrentUser.Id ?? Guid.Empty, saved.StudentId, saved.AcademicYearId, saved.CreatorId);
			await uow.CompleteAsync();
			return new(saved.Id, "adjustment", saved.Status.ToString(), MobileFinanceCommandRules.Money(Convert.ToDecimal(saved.RequestedAmount)), []);
		}
		var prepared = await PrepareFamilyAdjustmentAsync(child, year, input);
		MobileFinanceCommandRules.RequireFingerprint(prepared.Fingerprint, input.PreviewFingerprint);
		var request = await feeAdjustmentManager.CreateAsync(year, child, input.Reason, prepared.Lines);
		await feeAdjustmentRepository.InsertAsync(request, autoSave: false);
		await store.RecordAsync("adjustment_create", input.IdempotencyKey, payload, request.Id, child, year);
		var student = await studentRepository.GetAsync(child);
		await financeAuditWriter.WriteAsync(year, FinanceAuditEntityTypes.FeeAdjustment, request.Id, request.VoucherNumber,
			FinanceAuditActions.Adjustment, "Family noncash adjustment submitted for staff review.", BuildStudentContext(student, request.VoucherNumber));
		await uow.CompleteAsync();
		return new(request.Id, "adjustment", request.Status.ToString(), MobileFinanceCommandRules.Money(prepared.Amount), []);
	}
}
