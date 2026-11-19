using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.EntityFrameworkCore;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EntityFrameworkCore;
namespace ShikshaMitra.Finance.Accounts.BankActivity;
/// <summary>PostgreSQL reads authoritative evidence directly; no backfill, mutable header fallback, or second ledger.</summary>
public class EfCoreBankActivityQuerySource(IDbContextProvider<ShikshaMitraDbContext> contexts) : IBankActivityQuerySource, ITransientDependency
{
	public async Task<IQueryable<BankActivityFact>> QueryAsync(Guid? tenantId, Guid academicYearId, Guid bankId)
	{
		var db = await contexts.GetDbContextAsync();
		if (!db.Database.IsNpgsql()) throw new BusinessException(BankActivityErrorCodes.UnsupportedProvider);
		return db.Database.SqlQuery<BankActivityFact>($$"""
SELECT * FROM (
WITH tx AS (
 SELECT t.*,
  CASE WHEN t."SourceKey" LIKE 'collection:%' THEN 'collection:' || split_part(t."SourceKey", ':', 2) || ':' || split_part(t."SourceKey", ':', 3) || CASE WHEN t."Amount" < 0 THEN ':return' ELSE '' END
       WHEN t."SourceKey" LIKE 'refund:%' THEN t."SourceKey" ELSE 'transaction:' || t."Id"::text END AS movement_key,
  CASE WHEN t."SourceKey" LIKE 'collection:%' THEN 'collection' WHEN t."SourceKey" LIKE 'refund:%' THEN 'refund'
       WHEN t."TransactionType" = 0 THEN 'income' ELSE 'expense' END AS source_kind
 FROM "Finance_Transactions" t
 WHERE t."TenantId" IS NOT DISTINCT FROM {{tenantId}} AND t."AcademicYearId" = {{academicYearId}}
  AND t."BankAccountId" = {{bankId}} AND NOT t."IsDeleted"
  AND (t."SourceKey" IS NULL OR t."SourceKey" LIKE 'collection:%' OR t."SourceKey" LIKE 'refund:%')
  AND (t."PaymentMethod" <> 0 OR t."SourceKey" LIKE 'refund:%')
  AND NOT (t."SourceKey" LIKE 'collection:%' AND NOT t."IsConfirmed" AND EXISTS (
   SELECT 1 FROM "Finance_FeeRecordMetadatas" m WHERE m."Id"=t."SourceId" AND m."Status"=3))
), canonical_groups AS (
 SELECT movement_key, source_kind, COALESCE("SourceId", "Id") AS source_id, "StudentId", "FinanceFeeAccountId",
  min("Name") AS reference, COALESCE(min("ExtraProperties"::jsonb ->> 'Counterparty'), '') AS counterparty,
  CASE WHEN count(DISTINCT "CategoryId") > 1 THEN 'Multiple categories' ELSE COALESCE(min("CategoryNameSnapshot"), 'Unmapped category') END AS category,
  string_agg(DISTINCT "CategoryId"::text, ',') AS category_ids,
  jsonb_agg(jsonb_build_object('id', "CategoryId", 'category', COALESCE("CategoryNameSnapshot", 'Unmapped category'), 'amount', "Amount"))::text AS allocations,
  "PaymentMethod" AS method, min("PaymentDate") AS occurred, bool_and("IsConfirmed") AS confirmed,
  "TransactionType" AS type, sum("Amount") AS amount,
  min("ExtraProperties"::jsonb ->> 'PaymentReference') AS payment_reference,
  min("Notes") AS notes
 FROM tx
 GROUP BY movement_key, source_kind, COALESCE("SourceId", "Id"), "StudentId", "FinanceFeeAccountId", "PaymentMethod", "TransactionType"
), grouped AS (
 SELECT * FROM canonical_groups
 UNION ALL
 SELECT 'instrument:' || p."Id"::text, 'collection', m."Id", p."StudentId", p."FinanceFeeAccountId",
  m."ReceiptNumber", '', 'Unmapped historical category', '', '[]', p."PaymentMethod", p."PaymentDate",
  m."Status" IN (1,4), 0, p."Amount", NULL, p."Notes"
 FROM "Finance_PaymentMethodRecords" p JOIN "Finance_FeeRecordMetadatas" m ON m."Id"=p."FeeRecordMetadataId"
 WHERE p."TenantId" IS NOT DISTINCT FROM {{tenantId}} AND m."TenantId" IS NOT DISTINCT FROM {{tenantId}}
  AND m."AcademicYearId"={{academicYearId}} AND p."BankAccountId"={{bankId}} AND p."PaymentMethod"<>0
  AND NOT p."IsDeleted" AND NOT m."IsDeleted" AND m."Status" IN (1,4,5)
  AND NOT EXISTS (SELECT 1 FROM "Finance_Transactions" canonical WHERE canonical."TenantId" IS NOT DISTINCT FROM {{tenantId}} AND canonical."SourceId"=m."Id" AND canonical."SourceKey" LIKE 'collection:%')
 UNION ALL
 SELECT 'refund-entry:' || CASE WHEN e."IsCorrection" THEN e."CorrectsEntryId"::text || ':return' ELSE e."Id"::text END, 'refund', r."Id", r."StudentId", r."FinanceFeeAccountId", r."VoucherNumber", '', 'Unmapped historical category', '', '[]',
  CASE e."Method" WHEN 'Cash' THEN 0 WHEN 'Cheque' THEN 1 WHEN 'DemandDraft' THEN 2 WHEN 'Demand Draft (DD)' THEN 2 WHEN 'BankTransfer' THEN 3 WHEN 'Bank Transfer (NEFT/RTGS/IMPS)' THEN 3 WHEN 'Upi' THEN 6 WHEN 'UPI' THEN 6 END,
  e."PostedAt", true, 1, CASE WHEN e."IsCorrection" THEN -e."Amount" ELSE e."Amount" END, e."ExternalReference", NULL
 FROM "Finance_FeeRefundAccountingEntries" e JOIN "Finance_FeeRefundRequests" r ON r."Id"=e."RefundRequestId"
 WHERE e."TenantId" IS NOT DISTINCT FROM {{tenantId}} AND r."TenantId" IS NOT DISTINCT FROM {{tenantId}}
  AND r."AcademicYearId"={{academicYearId}} AND e."BankAccountId"={{bankId}} AND NOT r."IsDeleted"
  AND e."Method" IN ('Cash','Cheque','DemandDraft','Demand Draft (DD)','BankTransfer','Bank Transfer (NEFT/RTGS/IMPS)','Upi','UPI')
  AND NOT EXISTS (SELECT 1 FROM "Finance_Transactions" canonical WHERE canonical."TenantId" IS NOT DISTINCT FROM {{tenantId}} AND canonical."SourceId"=r."Id" AND canonical."SourceKey" LIKE 'refund:%')
), cash_evidence AS (
 SELECT c."Id" AS cash_id, c."SourceId", c."StudentId", c."FinanceFeeAccountId", c."CollectedAt", c."PayerName",
  d."Id" AS deposit_id, d."Amount", d."CreditedAmount", d."DepositDate", d."CreditDate", d."Reference", d."Notes",
  COALESCE(pin.bank_json, bulk.bank_json) AS bank_json
 FROM "Finance_CashCollections" c JOIN "Finance_CashDeposits" d ON d."CashCollectionId"=c."Id"
 LEFT JOIN LATERAL (
  SELECT CASE WHEN jsonb_typeof(c."ExtraProperties"::jsonb -> ('DepositBank:' || replace(d."Id"::text, '-', '')))='string'
    THEN (c."ExtraProperties"::jsonb ->> ('DepositBank:' || replace(d."Id"::text, '-', '')))::jsonb
    ELSE c."ExtraProperties"::jsonb -> ('DepositBank:' || replace(d."Id"::text, '-', '')) END AS bank_json
 ) pin ON true
 LEFT JOIN LATERAL (
  SELECT evidence AS bank_json FROM (
   SELECT CASE WHEN jsonb_typeof(e.value)='string' THEN (e.value #>> '{}')::jsonb ELSE e.value END AS evidence
   FROM jsonb_each(c."ExtraProperties"::jsonb) e WHERE e.key LIKE 'BulkDeposit:%'
  ) saved WHERE evidence ->> 'depositId'=d."Id"::text LIMIT 1
 ) bulk ON pin.bank_json IS NULL
 WHERE c."TenantId" IS NOT DISTINCT FROM {{tenantId}} AND c."AcademicYearId"={{academicYearId}} AND NOT c."IsDeleted"
), bank_cash AS (
 SELECT * FROM cash_evidence WHERE COALESCE(bank_json ->> 'Id',bank_json ->> 'id',bank_json ->> 'bankAccountId')={{bankId.ToString()}}
), facts AS (
 SELECT movement_key AS "Key", source_kind AS "Source", source_id AS "SourceId", "StudentId", "FinanceFeeAccountId",
  NULL::uuid AS "CashSourceId", reference AS "Reference", counterparty AS "Counterparty", category AS "CategoryName",
  category_ids AS "CategoryIds", allocations AS "AllocationsJson", method AS "PaymentMethod",
  occurred AS "OccurredAt", occurred AS "OriginalPaymentAt", NULL::timestamptz AS "BankCreditAt",
  CASE WHEN confirmed AND NOT (source_kind='refund' AND method=0) THEN greatest(CASE WHEN type=0 THEN amount ELSE -amount END,0) ELSE 0 END AS "Incoming",
  CASE WHEN confirmed AND NOT (source_kind='refund' AND method=0) THEN greatest(CASE WHEN type=0 THEN -amount ELSE amount END,0) ELSE 0 END AS "Outgoing",
  CASE WHEN NOT confirmed AND type=0 AND amount>0 AND method<>0 THEN amount ELSE 0 END AS "PendingAmount",
  CASE WHEN source_kind='refund' AND method=0 THEN 3 WHEN amount<0 THEN 2 WHEN confirmed THEN 0 ELSE 1 END AS "RecognitionStatus",
  0 AS "BankConfirmationStatus", CASE WHEN payment_reference IS NULL THEN NULL ELSE repeat('*',greatest(length(payment_reference)-4,4)) || CASE WHEN length(payment_reference)>4 THEN right(payment_reference,4) ELSE '' END END AS "PaymentReference",
  CASE WHEN amount<0 AND source_kind IN ('collection','refund') THEN replace(movement_key,':return','') ELSE NULL END AS "CorrectsKey",
  NULL::uuid AS "DepositId", 0::numeric AS "DepositAmount", 0::numeric AS "CreditedAmount", 0::numeric AS "InTransitAmount",
  NULL::timestamptz AS "DepositDate", notes AS "Notes", 'activity'::text AS "Panel"
 FROM grouped
 UNION ALL
 SELECT 'credit:' || r."Id"::text, 'cash-deposit', b."SourceId", b."StudentId", b."FinanceFeeAccountId", b.cash_id,
  safe.bank_reference, b."PayerName", 'Cash deposit', '', '[]', 0,
  r."CreditDate", b."CollectedAt", r."CreditDate", r."Amount", 0, 0, 0, 1, safe.bank_reference, NULL,
  b.deposit_id, b."Amount", r."Amount", 0, b."DepositDate", r."Notes", 'activity'
 FROM bank_cash b JOIN "Finance_CashDepositCredits" r ON r."CashDepositId"=b.deposit_id
 CROSS JOIN LATERAL (SELECT repeat('*',greatest(length(r."Reference")-4,4)) || CASE WHEN length(r."Reference")>4 THEN right(r."Reference",4) ELSE '' END AS bank_reference) safe
 UNION ALL
 SELECT 'legacy-credit:' || b.deposit_id::text, 'cash-deposit', b."SourceId", b."StudentId", b."FinanceFeeAccountId", b.cash_id,
  b."Reference", b."PayerName", 'Cash deposit', '', '[]', 0,
  b."CreditDate", b."CollectedAt", b."CreditDate", b."CreditedAmount", 0, 0, 0, 1, NULL, NULL,
  b.deposit_id, b."Amount", b."CreditedAmount", 0, b."DepositDate", b."Notes", 'activity'
 FROM bank_cash b WHERE b."CreditedAmount">0 AND b."CreditDate" IS NOT NULL
  AND NOT EXISTS (SELECT 1 FROM "Finance_CashDepositCredits" r WHERE r."CashDepositId"=b.deposit_id)
 UNION ALL
 SELECT 'transit:' || b.deposit_id::text, 'cash-deposit', b."SourceId", b."StudentId", b."FinanceFeeAccountId", b.cash_id,
  b."Reference", b."PayerName", 'Cash deposit', '', '[]', 0,
  b."DepositDate", b."CollectedAt", NULL, 0, 0, 0, 1, 2, NULL, NULL,
  b.deposit_id, b."Amount", 0, b."Amount"-b."CreditedAmount", b."DepositDate", b."Notes", 'activity'
 FROM bank_cash b WHERE b."Amount">b."CreditedAmount"
 UNION ALL
 SELECT 'deposit:' || b.deposit_id::text, 'cash-deposit', b."SourceId", b."StudentId", b."FinanceFeeAccountId", b.cash_id,
  b."Reference", b."PayerName", 'Cash deposit', '', '[]', 0,
  b."DepositDate", b."CollectedAt", COALESCE((SELECT max(r."CreditDate") FROM "Finance_CashDepositCredits" r WHERE r."CashDepositId"=b.deposit_id), CASE WHEN b."CreditedAmount">0 THEN b."CreditDate" ELSE NULL END),
  b."CreditedAmount", 0, 0, CASE WHEN b."Amount"=b."CreditedAmount" THEN 0 ELSE 1 END,
  CASE WHEN b."Amount"=b."CreditedAmount" THEN 1 ELSE 2 END, NULL, NULL,
  b.deposit_id, b."Amount", b."CreditedAmount", b."Amount"-b."CreditedAmount", b."DepositDate", b."Notes", 'deposits'
 FROM bank_cash b
)
SELECT facts.*, NULL::text AS "RecordedBy" FROM facts
) bank_activity
""");
	}
}
