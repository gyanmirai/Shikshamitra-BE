using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.Finance.ExternalReceivables;
using ShikshaMitra.Security.UserScope;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Library.Charges;

public class EfCoreLibraryChargePositionSourceQuery(IDbContextProvider<ShikshaMitraDbContext> contexts, ICurrentTenant tenant)
	: ILibraryChargePositionSourceQuery, ITransientDependency
{
	public async Task<IQueryable<FinanceExternalPositionSource>> GetQueryAsync(CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		var school = tenant.Id ?? throw new BusinessException(UserScopeErrorCodes.AccessDenied);
		var context = await contexts.GetDbContextAsync();
		var postgres = context.Database.ProviderName switch
		{
			"Npgsql.EntityFrameworkCore.PostgreSQL" => true,
			"Microsoft.EntityFrameworkCore.Sqlite" => false,
			_ => throw new NotSupportedException("Library charge source projection requires the configured PostgreSQL or test SQLite provider.")
		};
		var sql = SourceSql.Replace("REASON_TRIM", postgres ? "btrim" : "trim", StringComparison.Ordinal)
			.Replace("JSON_SCALAR", postgres ? "public.\"AppFinanceTryJsonScalar\"" : "\"AppFinanceTryJsonScalar\"", StringComparison.Ordinal);
		cancellationToken.ThrowIfCancellationRequested();
		return context.Database.SqlQueryRaw<FinanceExternalPositionSource>(sql, school, FinanceExternalEvidenceJson.WhiteSpaceCharacters);
	}

	private const string SourceSql = """
		SELECT c."TenantId", 1 AS "Kind", c."Id" AS "SourceId",
		    CASE WHEN c."FinanceSourceLinkId" IS NULL THEN c."ConcurrencyStamp"
		        ELSE JSON_SCALAR(c."ExtraProperties", 'FinanceBoundAssessmentVersion', 'string') END AS "SourceVersion",
		    c."FinanceSourceLinkId" AS "ExpectedSourceLinkId", c."AcademicYearId",
		    (c."AcademicYearId" IS NOT NULL AND c."AcademicYearProvenance" IN (1,2,3)) AS "AcademicYearConfirmed",
		    c."AcademicYearEvidenceJson",
		    CASE WHEN c."MemberRole"=0 THEN 1 ELSE 2 END AS "PayerKind",
		    CASE WHEN c."MemberRole"=0 THEN c."MemberId" ELSE NULL END AS "StudentId",
		    CASE WHEN c."MemberRole" IN (1,2) THEN c."MemberId" ELSE NULL END AS "EmployeeId",
		    c."Amount" AS "AssessmentOriginal", c."Currency" AS "RecordedCurrency",
		    c."SchoolChargeDate" AS "SchoolAssessmentDate", c."SchoolDueDate",
		    (c."SchoolChargeDate" IS NOT NULL AND c."SchoolDueDate" IS NOT NULL AND
		        c."ChargeDateProvenance" IN (1,2) AND c."DueDateProvenance" IN (1,2)) AS "SchoolDatesConfirmed",
		    c."SourceDateEvidenceJson", c."ChargeCategoryId" AS "RawSourceCategoryId", c."Remarks" AS "RawSourceRemarks",
		    CASE WHEN c."Remarks" IS NULL OR REASON_TRIM(c."Remarks", {1})=''
		        THEN REASON_TRIM(category."ChargeType", {1}) ELSE REASON_TRIM(c."Remarks", {1}) END AS "CurrentLegacyResolvedReason",
		    c."PaymentStatus" AS "RecordedSettlementDeclaration"
		FROM "AppLibraryCharges" c
		LEFT JOIN "AppLibraryChargeCategories" category ON category."Id"=c."ChargeCategoryId" AND category."TenantId"=c."TenantId"
		WHERE c."TenantId"={0}
		""";
}
