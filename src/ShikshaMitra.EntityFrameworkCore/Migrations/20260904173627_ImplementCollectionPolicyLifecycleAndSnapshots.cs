using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class ImplementCollectionPolicyLifecycleAndSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Finance_CollectionPolicies_Tenant_AcademicYear_Default",
                table: "Finance_CollectionPolicies");

            // Some deployed databases already contain this column from the student-fee
            // assignment rollout even though it is absent from the EF migration snapshot.
            // Keep this migration compatible with both those databases and fresh schemas.
            migrationBuilder.Sql("""
                ALTER TABLE "Finance_StudentFeeStructures"
                ADD COLUMN IF NOT EXISTS "AcademicYearId" uuid NULL;
                """);

            migrationBuilder.AddColumn<Guid>(
                name: "CollectionPolicyId",
                table: "Finance_StudentFeeStructures",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CollectionPolicyVersionId",
                table: "Finance_StudentFeeStructures",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CollectionPolicyVersionId",
                table: "Finance_FeeStructures",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_StudentFeeStructures_CollectionPolicyId",
                table: "Finance_StudentFeeStructures",
                column: "CollectionPolicyId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentFeeStructures_CollectionPolicyVersionId",
                table: "Finance_StudentFeeStructures",
                column: "CollectionPolicyVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_Finance_FeeStructures_CollectionPolicyId",
                table: "Finance_FeeStructures",
                column: "CollectionPolicyId");

            migrationBuilder.CreateIndex(
                name: "IX_Finance_FeeStructures_CollectionPolicyVersionId",
                table: "Finance_FeeStructures",
                column: "CollectionPolicyVersionId");

            // Older releases only had a filtered, non-unique index and could therefore
            // contain multiple defaults for one tenant/year. Keep the newest default and
            // make the rest non-default before the invariant becomes database-enforced.
            migrationBuilder.Sql("""
                WITH ranked_defaults AS (
                    SELECT "Id",
                           ROW_NUMBER() OVER (
                               PARTITION BY "TenantId", "AcademicYearId"
                               ORDER BY "CreationTime" DESC, "Id" DESC) AS row_number
                    FROM "Finance_CollectionPolicies"
                    WHERE "IsDefault" = true
                )
                UPDATE "Finance_CollectionPolicies" AS policy
                SET "IsDefault" = false
                FROM ranked_defaults
                WHERE policy."Id" = ranked_defaults."Id"
                  AND ranked_defaults.row_number > 1;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Finance_CollectionPolicies_Tenant_AcademicYear_Default",
                table: "Finance_CollectionPolicies",
                columns: new[] { "TenantId", "AcademicYearId", "IsDefault" },
                unique: true,
                filter: "\"IsDefault\" = true");

            migrationBuilder.AddForeignKey(
                name: "FK_Finance_FeeStructures_Finance_CollectionPolicies_Collection~",
                table: "Finance_FeeStructures",
                column: "CollectionPolicyId",
                principalTable: "Finance_CollectionPolicies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Finance_FeeStructures_Finance_PolicyVersions_CollectionPoli~",
                table: "Finance_FeeStructures",
                column: "CollectionPolicyVersionId",
                principalTable: "Finance_PolicyVersions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Finance_StudentFeeStructures_Finance_CollectionPolicies_Col~",
                table: "Finance_StudentFeeStructures",
                column: "CollectionPolicyId",
                principalTable: "Finance_CollectionPolicies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Finance_StudentFeeStructures_Finance_PolicyVersions_Collect~",
                table: "Finance_StudentFeeStructures",
                column: "CollectionPolicyVersionId",
                principalTable: "Finance_PolicyVersions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Finance_FeeStructures_Finance_CollectionPolicies_Collection~",
                table: "Finance_FeeStructures");

            migrationBuilder.DropForeignKey(
                name: "FK_Finance_FeeStructures_Finance_PolicyVersions_CollectionPoli~",
                table: "Finance_FeeStructures");

            migrationBuilder.DropForeignKey(
                name: "FK_Finance_StudentFeeStructures_Finance_CollectionPolicies_Col~",
                table: "Finance_StudentFeeStructures");

            migrationBuilder.DropForeignKey(
                name: "FK_Finance_StudentFeeStructures_Finance_PolicyVersions_Collect~",
                table: "Finance_StudentFeeStructures");

            migrationBuilder.DropIndex(
                name: "IX_StudentFeeStructures_CollectionPolicyId",
                table: "Finance_StudentFeeStructures");

            migrationBuilder.DropIndex(
                name: "IX_StudentFeeStructures_CollectionPolicyVersionId",
                table: "Finance_StudentFeeStructures");

            migrationBuilder.DropIndex(
                name: "IX_Finance_FeeStructures_CollectionPolicyId",
                table: "Finance_FeeStructures");

            migrationBuilder.DropIndex(
                name: "IX_Finance_FeeStructures_CollectionPolicyVersionId",
                table: "Finance_FeeStructures");

            migrationBuilder.DropIndex(
                name: "IX_Finance_CollectionPolicies_Tenant_AcademicYear_Default",
                table: "Finance_CollectionPolicies");

            // Do not drop AcademicYearId here: it may have existed before this migration
            // in databases where the column was introduced outside EF migration history.

            migrationBuilder.DropColumn(
                name: "CollectionPolicyId",
                table: "Finance_StudentFeeStructures");

            migrationBuilder.DropColumn(
                name: "CollectionPolicyVersionId",
                table: "Finance_StudentFeeStructures");

            migrationBuilder.DropColumn(
                name: "CollectionPolicyVersionId",
                table: "Finance_FeeStructures");

            migrationBuilder.CreateIndex(
                name: "IX_Finance_CollectionPolicies_Tenant_AcademicYear_Default",
                table: "Finance_CollectionPolicies",
                columns: new[] { "TenantId", "AcademicYearId", "IsDefault" },
                filter: "\"IsDefault\" = true");
        }
    }
}
