using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Finance.Fees.Charges;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.Receipts;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.TenantSettings.AcademicYears;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Timing;
using Permissions = ShikshaMitra.Finance.Mobile.MobileFinancePermissions;

namespace ShikshaMitra.Finance.Mobile;

[Authorize]
[RemoteService(false)]
public partial class MobileFinanceReadService
	: ApplicationService, IMobileFinanceReadService, ITransientDependency
{
	private readonly MobileFinanceAccessService access;
	private readonly IDueFeeRepository dues;
	private readonly IRepository<FeeHead, Guid> heads;
	private readonly IRepository<FeeRecordMetadata, Guid> transactions;
	private readonly IRepository<PaymentMethodRecord, Guid> methods;
	private readonly IFeePaymentReceiptRepository receipts;
	private readonly LateFeeBalanceService lateFees;
	private readonly IFeeReceiptPdfGenerator receiptPdf;
	private readonly IClock clock;

	public MobileFinanceReadService(
		MobileFinanceAccessService access,
		IDueFeeRepository dues,
		IRepository<FeeHead, Guid> heads,
		IRepository<FeeRecordMetadata, Guid> transactions,
		IRepository<PaymentMethodRecord, Guid> methods,
		IFeePaymentReceiptRepository receipts,
		LateFeeBalanceService lateFees,
		IFeeReceiptPdfGenerator receiptPdf,
		IRepository<AcademicYear, Guid> years,
		IClock clock
	)
	{
		this.access = access;
		this.dues = dues;
		this.heads = heads;
		this.transactions = transactions;
		this.methods = methods;
		this.receipts = receipts;
		this.lateFees = lateFees;
		this.receiptPdf = receiptPdf;
		_ = years;
		this.clock = clock;
	}

	[Authorize(Permissions.View)]
	public async Task<MobileFinanceContext> ContextAsync(Guid studentId, Guid? academicYearId = null, CancellationToken cancellationToken = default)
	{
		var context = await access.RequireAsync(studentId, academicYearId, cancellationToken: cancellationToken);
		Task<bool> Can(string permission) => access.IsGrantedAsync(permission);
		var receipt = await Can(Permissions.DownloadReceipts); var statement = await Can(Permissions.DownloadStatement);
		var viewCharges = await Can(Permissions.ViewCharges);
		var viewConcessions = await Can(Permissions.ViewConcessions);
		var viewRefunds = await Can(Permissions.ViewRefunds);
		var viewAdjustments = await Can(Permissions.ViewAdjustments);
		var concession = context.IsGuardian && viewConcessions && await Can(Permissions.RequestConcession);
		var refund = context.IsGuardian && viewRefunds && await Can(Permissions.RequestRefund) && await Can(Permissions.SubmitRefund);
		var adjustment = context.IsGuardian && viewAdjustments && await Can(Permissions.RequestAdjustment);
		var disabled = new Dictionary<string, string> { ["onlinePayment"] = "Online payment is not enabled." };
		foreach (var (key, enabled) in new[] { ("downloadReceipts",receipt),("downloadStatement",statement),("requestConcession",concession),("requestRefund",refund),("requestAdjustment",adjustment) })
			if (!enabled) disabled[key] = key.StartsWith("request", StringComparison.Ordinal) && !context.IsGuardian ? "Only a linked guardian can send requests." : "This action has not been granted.";
		var selected = academicYearId ?? context.AcademicYears.FirstOrDefault(x => x.StartDate.Date <= clock.Now.Date && x.EndDate.Date >= clock.Now.Date)?.Id ?? context.AcademicYears.FirstOrDefault()?.Id;
		return new(studentId, context.StudentName, context.AcademicYears.Select(x => new MobileFinanceYear(x.Id,x.Name)).ToList(), selected,
			new(true,receipt,statement,concession,refund,adjustment, CanViewCharges: viewCharges,
				CanViewReceipts: receipt, CanViewConcessions: viewConcessions, CanViewRefunds: viewRefunds, CanViewAdjustments: viewAdjustments),disabled);
	}
	[Authorize(Permissions.View)]
	public async Task<MobileFinanceOverview> OverviewAsync(Guid studentId, Guid academicYearId, CancellationToken cancellationToken = default)
	{
		await access.RequireAsync(studentId,academicYearId,cancellationToken:cancellationToken);
		return (await ReadSnapshotAsync(studentId,academicYearId,cancellationToken)).Overview;
	}
	private async Task<(MobileFinanceOverview Overview,List<DueFee> Dues,List<MobileChargeBalance> Charges)> ReadSnapshotAsync(Guid studentId, Guid yearId, CancellationToken ct)
	{
		var dueRows = await DueRowsAsync(studentId,yearId,ct);
		var chargeRows = await ChargeRowsAsync(studentId,yearId,ct);
		var late = await lateFees.GetBalancesAsync(dueRows,yearId,clock.Now.Date);
		var metadata = await transactions.GetListAsync(x => x.StudentId == studentId && x.AcademicYearId == yearId, cancellationToken:ct);
		var ids = metadata.Select(x => x.Id).ToArray();
		var splits = await methods.GetListAsync(x => x.StudentId == studentId && Enumerable.Contains(ids,x.FeeRecordMetadataId), cancellationToken:ct);
		var payments = metadata.Select(x => new MobilePaymentBalance(x.Id,MobileFinanceProjection.PaymentAmount(x.TotalPaidFee,splits.Where(s => s.FeeRecordMetadataId == x.Id).Select(s=>s.Amount).ToList()),x.Status)).ToList();
		var pendingIds = metadata.Where(x => x.Status == FeeCollectionStatus.InReconciliation).Select(x => x.Id).ToArray();
		var headIds = dueRows.Select(x => x.FeeHeadId).Distinct().ToArray();
		var principalRecords = LazyServiceProvider.LazyGetRequiredService<IRepository<FeeCollectionRecord, Guid>>();
		var reconciliation = await principalRecords.GetListAsync(x => x.StudentId == studentId
			&& Enumerable.Contains(headIds, x.FeeHeadId) && Enumerable.Contains(pendingIds, x.FeeRecordMetadataId), cancellationToken: ct);
		return (MobileFinanceProjection.Overview(studentId,yearId,clock.Now,dueRows,chargeRows,late.Values.ToList(),payments,
			reconciliation.Sum(x => MobileFinanceProjection.LegacyMoney(x.Amount))),dueRows,chargeRows);
	}
	[Authorize(Permissions.View)]
	public async Task<List<MobileFinanceInstallment>> InstallmentsAsync(Guid studentId, Guid academicYearId, CancellationToken cancellationToken = default)
	{
		await access.RequireAsync(studentId,academicYearId,cancellationToken:cancellationToken);
		var rows = await DueRowsAsync(studentId,academicYearId,cancellationToken);
		return await BuildInstallmentsAsync(studentId,academicYearId,rows,cancellationToken);
	}
	private async Task<List<MobileFinanceInstallment>> BuildInstallmentsAsync(Guid studentId,Guid academicYearId,List<DueFee> rows,CancellationToken cancellationToken)
	{
		var headIds=rows.Select(x=>x.FeeHeadId).Distinct().ToArray();
		var names=(await heads.GetListAsync(x=>Enumerable.Contains(headIds,x.Id),cancellationToken:cancellationToken)).ToDictionary(x=>x.Id,x=>x.Name);
		var pending = await PendingPrincipalAsync(studentId,academicYearId,cancellationToken);
		return rows.OrderBy(x=>x.ScheduleDueDate??x.DueDate).ThenBy(x=>x.Id).Select(x=>new MobileFinanceInstallment(x.Id,x.PaymentTermInstallmentId,x.GetDisplayInstallmentLabel(),x.FeeHeadId,names.GetValueOrDefault(x.FeeHeadId,string.Empty),
			DateOnly.FromDateTime(x.ScheduleDueDate??x.DueDate),x.DemandDate.HasValue?DateOnly.FromDateTime(x.DemandDate.Value):null,
			MobileFinanceProjection.Money(x.OriginalAmount),MobileFinanceProjection.Money(x.AppliedPaymentAmount),MobileFinanceProjection.Money(x.OutstandingAmount),
   MobileFinanceProjection.Money(pending.GetValueOrDefault(x.Id)),MobileFinanceProjection.Money(x.ConcessionAmount),MobileFinanceProjection.Money(x.RefundedAmount),
			DueFeeCollectionStatusResolver.Resolve(x,clock.Now,Convert.ToDouble(pending.GetValueOrDefault(x.Id))).ToString())
			{
				ScheduleStatus = MobileFinanceProjection.InstallmentScheduleStatus(x, clock.Now, pending.GetValueOrDefault(x.Id))
			}).ToList();
	}
	[Authorize(Permissions.ViewCharges)]
	public async Task<List<MobileFamilyCharge>> ChargesAsync(Guid studentId, Guid academicYearId, CancellationToken cancellationToken = default)
	{
		await access.RequireAsync(studentId,academicYearId,permission:Permissions.ViewCharges,cancellationToken:cancellationToken);
		var now=new DateTimeOffset(clock.Now).ToUniversalTime();
  return (await FamilyChargeRowsAsync(studentId,academicYearId,cancellationToken)).Select(x=>x.ToDto(now)).ToList();
	}
	private async Task<List<DueFee>> DueRowsAsync(Guid studentId, Guid yearId, CancellationToken ct)
	{
		var headQuery = (await heads.GetQueryableAsync()).Where(x=>x.AcademicYearId==yearId).Select(x=>x.Id);
		return await AsyncExecuter.ToListAsync((await dues.GetQueryableAsync()).Where(x=>x.StudentId==studentId && headQuery.Contains(x.FeeHeadId)),ct);
	}
	private async Task<Dictionary<Guid,decimal>> PendingPrincipalAsync(Guid studentId, Guid yearId, CancellationToken ct)
	{
		var txIds=(await transactions.GetQueryableAsync()).Where(x=>x.StudentId==studentId&&x.AcademicYearId==yearId&&x.Status==FeeCollectionStatus.InReconciliation).Select(x=>x.Id);
		var allocations=LazyServiceProvider.LazyGetRequiredService<IRepository<FeeCollectionDueFeeAllocation,Guid>>();
		return (await AsyncExecuter.ToListAsync((await allocations.GetQueryableAsync()).Where(x=>txIds.Contains(x.FeeRecordMetadataId)),ct)).GroupBy(x=>x.DueFeeId).ToDictionary(g=>g.Key,g=>g.Sum(x=>MobileFinanceProjection.LegacyMoney(x.Amount)));
	}
	private async Task<List<MobileChargeBalance>> ChargeRowsAsync(Guid studentId,Guid yearId,CancellationToken ct)
	{
		return (await FamilyChargeRowsAsync(studentId,yearId,ct)).Where(x=>x.Charge.Status==ChargeStatus.Approved).Select(x=>x.Legacy).ToList();
	}
}
