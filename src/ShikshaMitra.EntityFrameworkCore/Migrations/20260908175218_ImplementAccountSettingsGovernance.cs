using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class ImplementAccountSettingsGovernance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Finance_TransactionCategories",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "Finance_TransactionCategories",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Code",
                table: "Finance_TransactionCategories",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeactivationReason",
                table: "Finance_TransactionCategories",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DefaultAccountHeadId",
                table: "Finance_TransactionCategories",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "EffectiveFrom",
                table: "Finance_TransactionCategories",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "EffectiveTo",
                table: "Finance_TransactionCategories",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NormalizedCode",
                table: "Finance_TransactionCategories",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NormalizedName",
                table: "Finance_TransactionCategories",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "Finance_TransactionCategories",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AlterColumn<string>(
                name: "UpiId",
                table: "Finance_BankAccounts",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "IfscCode",
                table: "Finance_BankAccounts",
                type: "character varying(11)",
                maxLength: 11,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "BranchName",
                table: "Finance_BankAccounts",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "BankName",
                table: "Finance_BankAccounts",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "AccountNumber",
                table: "Finance_BankAccounts",
                type: "character varying(34)",
                maxLength: 34,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "AccountHolderName",
                table: "Finance_BankAccounts",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<Guid>(
                name: "AccountHeadId",
                table: "Finance_BankAccounts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Code",
                table: "Finance_BankAccounts",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeactivationReason",
                table: "Finance_BankAccounts",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "EffectiveFrom",
                table: "Finance_BankAccounts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "EffectiveTo",
                table: "Finance_BankAccounts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NormalizedCode",
                table: "Finance_BankAccounts",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "Finance_BankAccounts",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateTable(
                name: "Finance_AccountHeads",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    AcademicYearId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    NormalizedCode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    NormalizedName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    HeadType = table.Column<int>(type: "integer", nullable: false),
                    ParentAccountHeadId = table.Column<Guid>(type: "uuid", nullable: true),
                    PostingAllowed = table.Column<bool>(type: "boolean", nullable: false),
                    IsControlAccount = table.Column<bool>(type: "boolean", nullable: false),
                    CurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    EffectiveFrom = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EffectiveTo = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    DeactivationReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Finance_AccountHeads", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Finance_AccountHeads_Finance_AccountHeads_ParentAccountHead~",
                        column: x => x.ParentAccountHeadId,
                        principalTable: "Finance_AccountHeads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            // Backfill the newly governed masters without orphaning historical records.
            // Deterministic UUIDs make the migration repeatable and avoid extension dependencies.
            migrationBuilder.Sql("""
                UPDATE "Finance_TransactionCategories" c
                SET "CategoryType" = COALESCE(
                    (SELECT min(t."TransactionType") FROM "Finance_Transactions" t WHERE t."CategoryId" = c."Id"),
                    0)
                WHERE c."CategoryType" IS NULL;

                INSERT INTO "Finance_AccountHeads"
                    ("Id", "TenantId", "AcademicYearId", "Code", "NormalizedCode", "Name", "NormalizedName",
                     "HeadType", "PostingAllowed", "IsControlAccount", "CurrencyCode", "EffectiveFrom",
                     "IsActive", "Version", "ExtraProperties", "ConcurrencyStamp", "CreationTime", "IsDeleted")
                SELECT (md5('account-category:' || c."Id"::text))::uuid, c."TenantId", c."AcademicYearId",
                       'CAT-' || upper(left(replace(c."Id"::text, '-', ''), 12)),
                       'CAT-' || upper(left(replace(c."Id"::text, '-', ''), 12)),
                       left(c."Name", 115) || ' [' || left(c."Id"::text, 8) || ']',
                       upper(left(c."Name", 115)) || ' [' || left(c."Id"::text, 8) || ']',
                       CASE WHEN c."CategoryType" = 0 THEN 4 ELSE 5 END,
                       true, false, 'INR', c."CreationTime", c."IsActive", 1, '{}', md5(c."Id"::text),
                       c."CreationTime", false
                FROM "Finance_TransactionCategories" c;

                UPDATE "Finance_TransactionCategories" c
                SET "Code" = (CASE WHEN c."CategoryType" = 0 THEN 'INC-' ELSE 'EXP-' END) || upper(left(replace(c."Id"::text, '-', ''), 12)),
                    "NormalizedCode" = (CASE WHEN c."CategoryType" = 0 THEN 'INC-' ELSE 'EXP-' END) || upper(left(replace(c."Id"::text, '-', ''), 12)),
                    "NormalizedName" = upper(left(trim(c."Name"), 115)) || ' [' || left(c."Id"::text, 8) || ']',
                    "DefaultAccountHeadId" = (md5('account-category:' || c."Id"::text))::uuid,
                    "EffectiveFrom" = c."CreationTime",
                    "Version" = 1;

                INSERT INTO "Finance_AccountHeads"
                    ("Id", "TenantId", "AcademicYearId", "Code", "NormalizedCode", "Name", "NormalizedName",
                     "HeadType", "PostingAllowed", "IsControlAccount", "CurrencyCode", "EffectiveFrom",
                     "IsActive", "Version", "ExtraProperties", "ConcurrencyStamp", "CreationTime", "IsDeleted")
                SELECT (md5('account-bank:' || b."Id"::text))::uuid, b."TenantId", b."AcademicYearId",
                       'BANK-' || upper(left(replace(b."Id"::text, '-', ''), 12)),
                       'BANK-' || upper(left(replace(b."Id"::text, '-', ''), 12)),
                       left(b."BankName", 105) || ' - ' || right(b."AccountNumber", 4) || ' [' || left(b."Id"::text, 8) || ']',
                       upper(left(b."BankName", 105)) || ' - ' || right(b."AccountNumber", 4) || ' [' || left(b."Id"::text, 8) || ']',
                       1, true, false, 'INR', b."CreationTime", b."IsActive", 1, '{}', md5(b."Id"::text),
                       b."CreationTime", false
                FROM "Finance_BankAccounts" b;

                UPDATE "Finance_BankAccounts" b
                SET "Code" = 'BANK-' || upper(left(replace(b."Id"::text, '-', ''), 12)),
                    "NormalizedCode" = 'BANK-' || upper(left(replace(b."Id"::text, '-', ''), 12)),
                    "AccountHeadId" = (md5('account-bank:' || b."Id"::text))::uuid,
                    "EffectiveFrom" = b."CreationTime",
                    "Version" = 1;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Finance_TransactionCategories_DefaultAccountHeadId",
                table: "Finance_TransactionCategories",
                column: "DefaultAccountHeadId");

            migrationBuilder.CreateIndex(
                name: "IX_Finance_TransactionCategories_TenantId_AcademicYearId_Cate~1",
                table: "Finance_TransactionCategories",
                columns: new[] { "TenantId", "AcademicYearId", "CategoryType", "NormalizedName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Finance_TransactionCategories_TenantId_AcademicYearId_Categ~",
                table: "Finance_TransactionCategories",
                columns: new[] { "TenantId", "AcademicYearId", "CategoryType", "NormalizedCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Finance_BankAccounts_AccountHeadId",
                table: "Finance_BankAccounts",
                column: "AccountHeadId");

            migrationBuilder.CreateIndex(
                name: "IX_Finance_BankAccounts_TenantId_AcademicYearId_AccountHeadId",
                table: "Finance_BankAccounts",
                columns: new[] { "TenantId", "AcademicYearId", "AccountHeadId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Finance_BankAccounts_TenantId_AcademicYearId_NormalizedCode",
                table: "Finance_BankAccounts",
                columns: new[] { "TenantId", "AcademicYearId", "NormalizedCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Finance_AccountHeads_ParentAccountHeadId",
                table: "Finance_AccountHeads",
                column: "ParentAccountHeadId");

            migrationBuilder.CreateIndex(
                name: "IX_Finance_AccountHeads_TenantId_AcademicYearId_NormalizedCode",
                table: "Finance_AccountHeads",
                columns: new[] { "TenantId", "AcademicYearId", "NormalizedCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Finance_AccountHeads_TenantId_AcademicYearId_NormalizedName",
                table: "Finance_AccountHeads",
                columns: new[] { "TenantId", "AcademicYearId", "NormalizedName" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Finance_BankAccounts_Finance_AccountHeads_AccountHeadId",
                table: "Finance_BankAccounts",
                column: "AccountHeadId",
                principalTable: "Finance_AccountHeads",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Finance_TransactionCategories_Finance_AccountHeads_DefaultA~",
                table: "Finance_TransactionCategories",
                column: "DefaultAccountHeadId",
                principalTable: "Finance_AccountHeads",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Finance_BankAccounts_Finance_AccountHeads_AccountHeadId",
                table: "Finance_BankAccounts");

            migrationBuilder.DropForeignKey(
                name: "FK_Finance_TransactionCategories_Finance_AccountHeads_DefaultA~",
                table: "Finance_TransactionCategories");

            migrationBuilder.DropTable(
                name: "Finance_AccountHeads");

            migrationBuilder.DropIndex(
                name: "IX_Finance_TransactionCategories_DefaultAccountHeadId",
                table: "Finance_TransactionCategories");

            migrationBuilder.DropIndex(
                name: "IX_Finance_TransactionCategories_TenantId_AcademicYearId_Cate~1",
                table: "Finance_TransactionCategories");

            migrationBuilder.DropIndex(
                name: "IX_Finance_TransactionCategories_TenantId_AcademicYearId_Categ~",
                table: "Finance_TransactionCategories");

            migrationBuilder.DropIndex(
                name: "IX_Finance_BankAccounts_AccountHeadId",
                table: "Finance_BankAccounts");

            migrationBuilder.DropIndex(
                name: "IX_Finance_BankAccounts_TenantId_AcademicYearId_AccountHeadId",
                table: "Finance_BankAccounts");

            migrationBuilder.DropIndex(
                name: "IX_Finance_BankAccounts_TenantId_AcademicYearId_NormalizedCode",
                table: "Finance_BankAccounts");

            migrationBuilder.DropColumn(
                name: "Code",
                table: "Finance_TransactionCategories");

            migrationBuilder.DropColumn(
                name: "DeactivationReason",
                table: "Finance_TransactionCategories");

            migrationBuilder.DropColumn(
                name: "DefaultAccountHeadId",
                table: "Finance_TransactionCategories");

            migrationBuilder.DropColumn(
                name: "EffectiveFrom",
                table: "Finance_TransactionCategories");

            migrationBuilder.DropColumn(
                name: "EffectiveTo",
                table: "Finance_TransactionCategories");

            migrationBuilder.DropColumn(
                name: "NormalizedCode",
                table: "Finance_TransactionCategories");

            migrationBuilder.DropColumn(
                name: "NormalizedName",
                table: "Finance_TransactionCategories");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "Finance_TransactionCategories");

            migrationBuilder.DropColumn(
                name: "AccountHeadId",
                table: "Finance_BankAccounts");

            migrationBuilder.DropColumn(
                name: "Code",
                table: "Finance_BankAccounts");

            migrationBuilder.DropColumn(
                name: "DeactivationReason",
                table: "Finance_BankAccounts");

            migrationBuilder.DropColumn(
                name: "EffectiveFrom",
                table: "Finance_BankAccounts");

            migrationBuilder.DropColumn(
                name: "EffectiveTo",
                table: "Finance_BankAccounts");

            migrationBuilder.DropColumn(
                name: "NormalizedCode",
                table: "Finance_BankAccounts");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "Finance_BankAccounts");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Finance_TransactionCategories",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(128)",
                oldMaxLength: 128);

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "Finance_TransactionCategories",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(1000)",
                oldMaxLength: 1000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "UpiId",
                table: "Finance_BankAccounts",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(128)",
                oldMaxLength: 128,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "IfscCode",
                table: "Finance_BankAccounts",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(11)",
                oldMaxLength: 11);

            migrationBuilder.AlterColumn<string>(
                name: "BranchName",
                table: "Finance_BankAccounts",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(128)",
                oldMaxLength: 128);

            migrationBuilder.AlterColumn<string>(
                name: "BankName",
                table: "Finance_BankAccounts",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(128)",
                oldMaxLength: 128);

            migrationBuilder.AlterColumn<string>(
                name: "AccountNumber",
                table: "Finance_BankAccounts",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(34)",
                oldMaxLength: 34);

            migrationBuilder.AlterColumn<string>(
                name: "AccountHolderName",
                table: "Finance_BankAccounts",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(128)",
                oldMaxLength: 128);
        }
    }
}
