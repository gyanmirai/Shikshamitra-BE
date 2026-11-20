using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.Finance.Accounts.Incomes;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EntityFrameworkCore;

namespace ShikshaMitra.Finance.Accounts.Activity;

public class AccountsRegisterEvidenceQuery(IDbContextProvider<ShikshaMitraDbContext> contexts)
    : IAccountsRegisterEvidenceQuery, ITransientDependency
{
    public async Task<IQueryable<AccountsTransactionParty>> PartiesAsync(Guid? tenantId, Guid academicYearId)
    {
        var db = await contexts.GetDbContextAsync();
        if (db.Database.IsNpgsql())
            return db.Database.SqlQuery<AccountsTransactionParty>($"""
                SELECT "Id", COALESCE("ExtraProperties"::jsonb ->> 'Counterparty', '') AS "Counterparty"
                FROM "Finance_Transactions"
                WHERE "TenantId" IS NOT DISTINCT FROM {tenantId} AND "AcademicYearId" = {academicYearId} AND NOT "IsDeleted"
                """);
        return db.Database.SqlQuery<AccountsTransactionParty>($"""
            SELECT "Id", COALESCE(json_extract("ExtraProperties", '$.Counterparty'), '') AS "Counterparty"
            FROM "Finance_Transactions"
            WHERE "TenantId" IS {tenantId} AND "AcademicYearId" = {academicYearId} AND NOT "IsDeleted"
            """);
    }

    public async Task<IQueryable<AccountsRefundEntryTime>?> RefundTimesAsync(Guid? tenantId, Guid academicYearId)
    {
        var db = await contexts.GetDbContextAsync();
        // Read the immutable refund instant using the transaction date SQL type, including
        // the runtime legacy timestamp mapping. SQLite retains its exact scalar fallback
        // rather than rounding stored instants.
        if (!db.Database.IsNpgsql()) return null;
        var dateType = db.Model.FindEntityType(typeof(Transaction))!.FindProperty(nameof(Transaction.PaymentDate))!.GetColumnType();
        var instant = dateType == "timestamp without time zone" ? "e.\"PostedAt\" AT TIME ZONE 'UTC'" : "e.\"PostedAt\"";
        var sql = FormattableStringFactory.Create("""
            SELECT e."Id", 
            """ + instant + """
             AS "PostedAtUtc"
            FROM "Finance_FeeRefundAccountingEntries" e
            JOIN "Finance_FeeRefundRequests" r ON r."Id" = e."RefundRequestId"
            WHERE e."TenantId" IS NOT DISTINCT FROM {0} AND r."TenantId" IS NOT DISTINCT FROM {0}
              AND r."AcademicYearId" = {1} AND NOT r."IsDeleted"
            """, tenantId, academicYearId);
        return db.Database.SqlQuery<AccountsRefundEntryTime>(sql);
    }
}
