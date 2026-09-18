using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class FeeCollectionIdempotency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CommandFingerprint",
                table: "Finance_FeeRecordMetadatas",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CommandResultJson",
                table: "Finance_FeeRecordMetadatas",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                table: "Finance_FeeRecordMetadatas",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "UX_FeeRecordMetadatas_TenantId_IdempotencyKey",
                table: "Finance_FeeRecordMetadatas",
                columns: new[] { "TenantId", "IdempotencyKey" },
                unique: true,
                filter: "\"IdempotencyKey\" IS NOT NULL AND \"IsDeleted\" = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_FeeRecordMetadatas_TenantId_IdempotencyKey",
                table: "Finance_FeeRecordMetadatas");

            migrationBuilder.DropColumn(
                name: "CommandFingerprint",
                table: "Finance_FeeRecordMetadatas");

            migrationBuilder.DropColumn(
                name: "CommandResultJson",
                table: "Finance_FeeRecordMetadatas");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                table: "Finance_FeeRecordMetadatas");
        }
    }
}
