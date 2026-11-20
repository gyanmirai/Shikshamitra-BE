using Microsoft.EntityFrameworkCore.Migrations;

namespace ShikshaMitra.Migrations;

public partial class CompleteLibraryCirculationAndFinanceSources
{
	private static readonly string[] CirculationEvidenceTables =
	[
		"AppLibraryLoanCommands", "AppLibraryLoanEvents", "AppLibraryReservationEvents",
		"Finance_ExternalReceivableSourceLinks", "Finance_ExternalReceivableCommands"
	];

	private static void InstallCirculationEvidenceGuards(MigrationBuilder migrationBuilder)
	{
		foreach (var table in CirculationEvidenceTables)
			migrationBuilder.Sql($$"""
				CREATE FUNCTION "{{table}}_BlockMutation"() RETURNS trigger
				LANGUAGE plpgsql AS $library_evidence$
				BEGIN
				    RAISE EXCEPTION USING ERRCODE = 'P0001',
				        MESSAGE = 'Library circulation and Finance evidence is append-only; record a new authorized event instead.';
				END;
				$library_evidence$;
				CREATE TRIGGER "TR_{{table}}_ImmutableRows" BEFORE UPDATE OR DELETE ON "{{table}}"
				    FOR EACH ROW EXECUTE FUNCTION "{{table}}_BlockMutation"();
				CREATE TRIGGER "TR_{{table}}_NoTruncate" BEFORE TRUNCATE ON "{{table}}"
				    FOR EACH STATEMENT EXECUTE FUNCTION "{{table}}_BlockMutation"();
				""");
	}

	private static void DropCirculationEvidenceGuards(MigrationBuilder migrationBuilder)
	{
		foreach (var table in CirculationEvidenceTables)
			migrationBuilder.Sql($"DROP FUNCTION \"{table}_BlockMutation\"();");
	}
}
