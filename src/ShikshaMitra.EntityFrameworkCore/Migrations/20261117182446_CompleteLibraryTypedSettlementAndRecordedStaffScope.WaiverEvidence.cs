using Microsoft.EntityFrameworkCore.Migrations;

namespace ShikshaMitra.Migrations;

public partial class CompleteLibraryTypedSettlementAndRecordedStaffScope
{
	private static void InstallWaiverRequestEvidenceGuard(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
		CREATE FUNCTION "FinanceWaiverRequest_RetainEvidence"() RETURNS trigger
		LANGUAGE plpgsql AS $finance_waiver_evidence$
		BEGIN
		    IF TG_OP NOT IN ('INSERT','UPDATE') THEN
		        RAISE EXCEPTION 'Finance waiver request evidence cannot be deleted or truncated.';
		    END IF;
		    IF TG_OP='UPDATE' AND (OLD."State"<>0 OR
		       (to_jsonb(NEW)-ARRAY['State','DecidedByUserId','DecidedAt','DecisionReason','AppliedWaiverId','ConcurrencyStamp','LastModificationTime','LastModifierId'])
		       IS DISTINCT FROM
		       (to_jsonb(OLD)-ARRAY['State','DecidedByUserId','DecidedAt','DecisionReason','AppliedWaiverId','ConcurrencyStamp','LastModificationTime','LastModifierId'])) THEN
		        RAISE EXCEPTION 'Finance waiver request evidence must retain its original payload and final decision.';
		    END IF;
		    IF TG_OP='INSERT' AND NEW."State"=0 AND NEW."DecidedByUserId" IS NULL AND
		       NEW."DecidedAt" IS NULL AND NEW."DecisionReason" IS NULL AND NEW."AppliedWaiverId" IS NULL THEN
		        RETURN NEW;
		    END IF;
		    IF NEW."State" NOT IN (1,2) OR NEW."DecidedByUserId" IS NULL OR
		       NEW."DecidedByUserId"='00000000-0000-0000-0000-000000000000'::uuid OR
		       NEW."DecidedAt" IS NULL OR NOT isfinite(NEW."DecidedAt") OR
		       NEW."DecisionReason" IS NULL OR btrim(NEW."DecisionReason")='' OR
		       (NEW."RequiresApproval" AND NEW."DecidedByUserId"=NEW."RequestedByUserId") OR
		       (NEW."State"=1 AND NEW."AppliedWaiverId" IS NULL) OR
		       (NEW."State"=2 AND (NOT NEW."RequiresApproval" OR NEW."AppliedWaiverId" IS NOT NULL)) THEN
		        RAISE EXCEPTION 'Finance waiver request evidence requires one complete authorized decision.';
		    END IF;
		    IF NEW."State"=1 AND NOT EXISTS (
		        SELECT 1 FROM "Finance_ChargeWaiverApplications" application
		        JOIN "Finance_ExternalReceivableSourceLinks" source ON source."Id"=NEW."SourceLinkId"
		        WHERE application."Id"=NEW."AppliedWaiverId" AND application."TenantId"=NEW."TenantId" AND
		            source."TenantId"=NEW."TenantId" AND application."ChargeId"=source."FinanceChargeId" AND
		            application."AcademicYearId"=source."AcademicYearId" AND application."EffectiveAmount"=NEW."Amount" AND
		            application."AppliedByUserId"=NEW."DecidedByUserId") THEN
		        RAISE EXCEPTION 'Finance waiver request evidence must reference its exact authorized application and source.';
		    END IF;
		    RETURN NEW;
		END;
		$finance_waiver_evidence$;
		CREATE TRIGGER "TR_FinanceWaiverRequest_Evidence" BEFORE INSERT OR UPDATE OR DELETE ON "Finance_ExternalWaiverRequests"
		    FOR EACH ROW EXECUTE FUNCTION "FinanceWaiverRequest_RetainEvidence"();
		CREATE TRIGGER "TR_FinanceWaiverRequest_NoTruncate" BEFORE TRUNCATE ON "Finance_ExternalWaiverRequests"
		    FOR EACH STATEMENT EXECUTE FUNCTION "FinanceWaiverRequest_RetainEvidence"();
		""");
}
