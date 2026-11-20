using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ShikshaMitra.EntityFrameworkCore;

#nullable disable

namespace ShikshaMitra.Migrations;

[DbContext(typeof(ShikshaMitraDbContext))]
[Migration("20261117143000_IndexAssignmentHeadEvidence")]
public partial class IndexAssignmentHeadEvidence : Migration
{
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.AddColumn<Guid[]>(name: "IncludedFeeHeadIds", table: "Finance_StudentFeeStructures", type: "uuid[]", nullable: true);
		migrationBuilder.AddColumn<Guid>(name: "AcademicStandardId", table: "Finance_StudentFeeStructures", type: "uuid", nullable: true);
		migrationBuilder.AddColumn<Guid>(name: "AcademicDivisionId", table: "Finance_StudentFeeStructures", type: "uuid", nullable: true);
		migrationBuilder.AddColumn<int>(name: "AcademicRollNumber", table: "Finance_StudentFeeStructures", type: "integer", nullable: true);
		// A migration-local parser isolates corrupt legacy text without rejecting the batch.
		// NULL deliberately retains conservative structure-based dependency evidence.
		migrationBuilder.Sql("""
			CREATE FUNCTION pg_temp.fee_assignment_selected_heads(snapshot text) RETURNS uuid[]
			LANGUAGE plpgsql AS $$
			DECLARE evidence jsonb; head jsonb; head_id uuid; selected uuid[] := ARRAY[]::uuid[];
			BEGIN
				IF snapshot IS NULL THEN RETURN NULL; END IF;
				BEGIN evidence := snapshot::jsonb;
				EXCEPTION WHEN invalid_text_representation OR numeric_value_out_of_range OR untranslatable_character THEN RETURN NULL; END;
				IF jsonb_typeof(evidence->'Heads') IS DISTINCT FROM 'array' THEN RETURN NULL; END IF;
				FOR head IN SELECT value FROM jsonb_array_elements(evidence->'Heads') LOOP
					IF jsonb_typeof(head) IS DISTINCT FROM 'object' THEN RETURN NULL; END IF;
					IF head ? 'Included' AND jsonb_typeof(head->'Included') IS DISTINCT FROM 'boolean' THEN RETURN NULL; END IF;
					head_id := '00000000-0000-0000-0000-000000000000'::uuid;
					IF head ? 'FeeHeadId' THEN
						IF jsonb_typeof(head->'FeeHeadId') IS DISTINCT FROM 'string' THEN RETURN NULL; END IF;
						IF head->>'FeeHeadId' !~ '^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$' THEN RETURN NULL; END IF;
						BEGIN head_id := (head->>'FeeHeadId')::uuid;
						EXCEPTION WHEN invalid_text_representation THEN RETURN NULL; END;
					END IF;
					IF head->>'Included' = 'true' THEN selected := array_append(selected, head_id); END IF;
				END LOOP;
				RETURN ARRAY(SELECT DISTINCT id FROM unnest(selected) id ORDER BY id);
			END $$;
			UPDATE "Finance_StudentFeeStructures"
			SET "IncludedFeeHeadIds" = pg_temp.fee_assignment_selected_heads("AssignmentSnapshotJson");
			DROP FUNCTION pg_temp.fee_assignment_selected_heads(text);
			""");
		migrationBuilder.CreateIndex(name: "IX_StudentFeeStructures_IncludedFeeHeadIds", table: "Finance_StudentFeeStructures", column: "IncludedFeeHeadIds")
			.Annotation("Npgsql:IndexMethod", "gin");
	}
	protected override void Down(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.Sql("""
			LOCK TABLE "Finance_StudentFeeStructures" IN ACCESS EXCLUSIVE MODE;
			DO $$ BEGIN
				IF EXISTS (SELECT 1 FROM "Finance_StudentFeeStructures"
					WHERE "AcademicStandardId" IS NOT NULL OR "AcademicDivisionId" IS NOT NULL OR "AcademicRollNumber" IS NOT NULL) THEN
					RAISE EXCEPTION 'Academic assignment evidence exists; use a forward fix instead of dropping history.';
				END IF;
			END $$;
			""");
		migrationBuilder.DropIndex(name: "IX_StudentFeeStructures_IncludedFeeHeadIds", table: "Finance_StudentFeeStructures");
		migrationBuilder.DropColumn(name: "IncludedFeeHeadIds", table: "Finance_StudentFeeStructures");
		migrationBuilder.DropColumn(name: "AcademicStandardId", table: "Finance_StudentFeeStructures");
		migrationBuilder.DropColumn(name: "AcademicDivisionId", table: "Finance_StudentFeeStructures");
		migrationBuilder.DropColumn(name: "AcademicRollNumber", table: "Finance_StudentFeeStructures");
	}
}
