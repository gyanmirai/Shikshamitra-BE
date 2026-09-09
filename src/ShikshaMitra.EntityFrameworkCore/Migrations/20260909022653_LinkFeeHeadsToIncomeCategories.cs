using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class LinkFeeHeadsToIncomeCategories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "IncomeCategoryId",
                table: "Finance_FeeHeads",
                type: "uuid",
                nullable: true);

            // Preserve historical classification by creating an explicit one-to-one legacy
            // income mapping for every existing Fee Head. Schools can consolidate unused
            // mappings later, but no existing revenue is silently reclassified during deploy.
            migrationBuilder.Sql("""
                INSERT INTO "Finance_AccountHeads"
                    ("Id", "TenantId", "AcademicYearId", "Code", "NormalizedCode", "Name", "NormalizedName",
                     "HeadType", "PostingAllowed", "IsControlAccount", "CurrencyCode", "Description",
                     "EffectiveFrom", "IsActive", "Version", "ExtraProperties", "ConcurrencyStamp",
                     "CreationTime", "IsDeleted")
                SELECT (md5('fee-head-account:' || h."Id"::text))::uuid,
                       h."TenantId", h."AcademicYearId",
                       'FEE-REV-' || upper(left(replace(h."Id"::text, '-', ''), 12)),
                       'FEE-REV-' || upper(left(replace(h."Id"::text, '-', ''), 12)),
                       left(h."Name", 100) || ' Revenue [' || left(h."Id"::text, 8) || ']',
                       upper(left(h."Name", 100)) || ' REVENUE [' || left(h."Id"::text, 8) || ']',
                       4, true, false, 'INR',
                       'Auto-generated legacy revenue account for Fee Head migration.',
                       h."CreationTime", true, 1, '{}', md5('fee-head-account-stamp:' || h."Id"::text),
                       h."CreationTime", false
                FROM "Finance_FeeHeads" h
                WHERE NOT EXISTS (
                    SELECT 1 FROM "Finance_AccountHeads" a
                    WHERE a."Id" = (md5('fee-head-account:' || h."Id"::text))::uuid
                );

                INSERT INTO "Finance_TransactionCategories"
                    ("Id", "TenantId", "AcademicYearId", "CategoryType", "Code", "NormalizedCode",
                     "Name", "NormalizedName", "Description", "DefaultAccountHeadId", "EffectiveFrom",
                     "IsActive", "Version", "ExtraProperties", "ConcurrencyStamp", "CreationTime", "IsDeleted")
                SELECT (md5('fee-head-income-category:' || h."Id"::text))::uuid,
                       h."TenantId", h."AcademicYearId", 0,
                       'FEE-INC-' || upper(left(replace(h."Id"::text, '-', ''), 12)),
                       'FEE-INC-' || upper(left(replace(h."Id"::text, '-', ''), 12)),
                       left(h."Name", 100) || ' Income [' || left(h."Id"::text, 8) || ']',
                       upper(left(h."Name", 100)) || ' INCOME [' || left(h."Id"::text, 8) || ']',
                       'Auto-generated legacy Income Category for Fee Head migration.',
                       (md5('fee-head-account:' || h."Id"::text))::uuid,
                       h."CreationTime", true, 1, '{}',
                       md5('fee-head-category-stamp:' || h."Id"::text), h."CreationTime", false
                FROM "Finance_FeeHeads" h
                WHERE NOT EXISTS (
                    SELECT 1 FROM "Finance_TransactionCategories" c
                    WHERE c."Id" = (md5('fee-head-income-category:' || h."Id"::text))::uuid
                );

                UPDATE "Finance_FeeHeads" h
                SET "IncomeCategoryId" = (md5('fee-head-income-category:' || h."Id"::text))::uuid
                WHERE h."IncomeCategoryId" IS NULL;
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "IncomeCategoryId",
                table: "Finance_FeeHeads",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Finance_FeeHeads_IncomeCategoryId",
                table: "Finance_FeeHeads",
                column: "IncomeCategoryId");

            migrationBuilder.AddForeignKey(
                name: "FK_Finance_FeeHeads_Finance_TransactionCategories_IncomeCatego~",
                table: "Finance_FeeHeads",
                column: "IncomeCategoryId",
                principalTable: "Finance_TransactionCategories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Finance_FeeHeads_Finance_TransactionCategories_IncomeCatego~",
                table: "Finance_FeeHeads");

            migrationBuilder.Sql("""
                DELETE FROM "Finance_TransactionCategories" c
                USING "Finance_FeeHeads" h
                WHERE c."Id" = (md5('fee-head-income-category:' || h."Id"::text))::uuid
                  AND h."IncomeCategoryId" = c."Id";

                DELETE FROM "Finance_AccountHeads" a
                USING "Finance_FeeHeads" h
                WHERE a."Id" = (md5('fee-head-account:' || h."Id"::text))::uuid;
                """);

            migrationBuilder.DropIndex(
                name: "IX_Finance_FeeHeads_IncomeCategoryId",
                table: "Finance_FeeHeads");

            migrationBuilder.DropColumn(
                name: "IncomeCategoryId",
                table: "Finance_FeeHeads");
        }
    }
}
