using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Finance.Accounts.Incomes;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.FeeRefund;
using ShikshaMitra.Finance.Fees.Receipts;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Finance.Audit;

public partial class FinanceAuditTrailAppService
{
    private async Task<IQueryable<FinanceAuditEvent>> ApplyPayerScopeAsync(IQueryable<FinanceAuditEvent> query)
    {
        var scope = await scopeAccess.GetCurrentUserScopeAsync();
        if (!scope.IsAdmin && !scope.HasWildcardAcademicYear)
            query = query.Where(e => e.AcademicYearId == null || scope.AcademicYearIds.Contains(e.AcademicYearId.Value));
        var students = await financeScopeAccessService.GetScopedStudentIdsQueryAsync();
        var accounts = await financeScopeAccessService.GetScopedFeeAccountIdsQueryAsync();
        var payments = (await LazyServiceProvider.LazyGetRequiredService<IRepository<FeeRecordMetadata, Guid>>().GetQueryableAsync()).Where(p => p.FinanceFeeAccountId.HasValue);
        var receipts = (await LazyServiceProvider.LazyGetRequiredService<IRepository<FeePaymentReceipt, Guid>>().GetQueryableAsync()).Where(r => r.FinanceFeeAccountId.HasValue);
        var dues = (await LazyServiceProvider.LazyGetRequiredService<IDueFeeRepository>().GetQueryableAsync()).Where(d => d.FinanceFeeAccountId.HasValue);
        var refunds = (await LazyServiceProvider.LazyGetRequiredService<IRepository<FeeRefundRequest, Guid>>().GetQueryableAsync()).Where(r => r.FinanceFeeAccountId.HasValue);
        var income = (await LazyServiceProvider.LazyGetRequiredService<IRepository<Transaction, Guid>>().GetQueryableAsync()).Where(t => t.FinanceFeeAccountId.HasValue);
        // Resolve account scope through immutable entity references. A null StudentId
        // on an applicant event must never turn it into an unscoped Finance event.
        return query.Where(e =>
            e.EntityType == FinanceAuditEntityTypes.FeePayment && payments.Any(p => p.Id == e.EntityId)
                ? payments.Any(p => p.Id == e.EntityId && accounts.Contains(p.FinanceFeeAccountId!.Value)) :
            (e.EntityType == FinanceAuditEntityTypes.FeeReceipt || e.EntityType == FinanceAuditEntityTypes.BankReconciliation) && receipts.Any(r => r.Id == e.EntityId)
                ? receipts.Any(r => r.Id == e.EntityId && accounts.Contains(r.FinanceFeeAccountId!.Value)) :
            e.EntityType == FinanceAuditEntityTypes.DueFee && dues.Any(d => d.Id == e.EntityId)
                ? dues.Any(d => d.Id == e.EntityId && accounts.Contains(d.FinanceFeeAccountId!.Value)) :
            e.EntityType == FinanceAuditEntityTypes.FeeRefund && refunds.Any(r => r.Id == e.EntityId)
                ? refunds.Any(r => r.Id == e.EntityId && accounts.Contains(r.FinanceFeeAccountId!.Value)) :
            e.EntityType == FinanceAuditEntityTypes.IncomeEntry && income.Any(t => t.Id == e.EntityId)
                ? income.Any(t => t.Id == e.EntityId && accounts.Contains(t.FinanceFeeAccountId!.Value)) :
            e.StudentId == null || students.Contains(e.StudentId.Value));
    }
}
