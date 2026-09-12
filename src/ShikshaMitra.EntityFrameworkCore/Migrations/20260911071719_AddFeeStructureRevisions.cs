using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class AddFeeStructureRevisions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NormalizedName",
                table: "Finance_FeeStructures",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "PreviousRevisionId",
                table: "Finance_FeeStructures",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReceiptBookId",
                table: "Finance_FeeStructures",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RevisionNumber",
                table: "Finance_FeeStructures",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<Guid>(
                name: "RevisionRootId",
                table: "Finance_FeeStructures",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.Sql("""UPDATE "Finance_FeeStructures" SET "RevisionRootId" = "Id";""");
            migrationBuilder.Sql("""UPDATE "Finance_FeeStructures" SET "NormalizedName" = upper(trim("Name"));""");
            migrationBuilder.Sql("""
                WITH ordered_revisions AS (
                    SELECT
                        "Id",
                        first_value("Id") OVER revision_order AS root_id,
                        lag("Id") OVER revision_order AS previous_id,
                        row_number() OVER revision_order AS revision_number
                    FROM "Finance_FeeStructures"
                    WINDOW revision_order AS (
                        PARTITION BY "StandardId", "NormalizedName", "IsDeleted"
                        ORDER BY "CreationTime", "Id"
                    )
                )
                UPDATE "Finance_FeeStructures" AS fee_structure
                SET
                    "RevisionRootId" = ordered_revisions.root_id,
                    "PreviousRevisionId" = ordered_revisions.previous_id,
                    "RevisionNumber" = ordered_revisions.revision_number
                FROM ordered_revisions
                WHERE fee_structure."Id" = ordered_revisions."Id";
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Finance_FeeStructures_PreviousRevisionId",
                table: "Finance_FeeStructures",
                column: "PreviousRevisionId");

            migrationBuilder.CreateIndex(
                name: "IX_Finance_FeeStructures_ReceiptBookId",
                table: "Finance_FeeStructures",
                column: "ReceiptBookId");

            migrationBuilder.CreateIndex(
                name: "IX_Finance_FeeStructures_RevisionRootId_RevisionNumber",
                table: "Finance_FeeStructures",
                columns: new[] { "RevisionRootId", "RevisionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Finance_FeeStructures_StandardId_NormalizedName",
                table: "Finance_FeeStructures",
                columns: new[] { "StandardId", "NormalizedName" },
                unique: true,
                filter: "\"PreviousRevisionId\" IS NULL AND NOT \"IsDeleted\"");

            migrationBuilder.AddForeignKey(
                name: "FK_Finance_FeeStructures_PreviousRevisionId",
                table: "Finance_FeeStructures",
                column: "PreviousRevisionId",
                principalTable: "Finance_FeeStructures",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Finance_FeeStructures_ReceiptBookId",
                table: "Finance_FeeStructures",
                column: "ReceiptBookId",
                principalTable: "Finance_ReceiptBooks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Finance_FeeStructures_PreviousRevisionId",
                table: "Finance_FeeStructures");

            migrationBuilder.DropForeignKey(
                name: "FK_Finance_FeeStructures_ReceiptBookId",
                table: "Finance_FeeStructures");

            migrationBuilder.DropIndex(
                name: "IX_Finance_FeeStructures_PreviousRevisionId",
                table: "Finance_FeeStructures");

            migrationBuilder.DropIndex(
                name: "IX_Finance_FeeStructures_ReceiptBookId",
                table: "Finance_FeeStructures");

            migrationBuilder.DropIndex(
                name: "IX_Finance_FeeStructures_RevisionRootId_RevisionNumber",
                table: "Finance_FeeStructures");

            migrationBuilder.DropIndex(
                name: "IX_Finance_FeeStructures_StandardId_NormalizedName",
                table: "Finance_FeeStructures");

            migrationBuilder.DropColumn(
                name: "NormalizedName",
                table: "Finance_FeeStructures");

            migrationBuilder.DropColumn(
                name: "PreviousRevisionId",
                table: "Finance_FeeStructures");

            migrationBuilder.DropColumn(
                name: "ReceiptBookId",
                table: "Finance_FeeStructures");

            migrationBuilder.DropColumn(
                name: "RevisionNumber",
                table: "Finance_FeeStructures");

            migrationBuilder.DropColumn(
                name: "RevisionRootId",
                table: "Finance_FeeStructures");
        }
    }
}
