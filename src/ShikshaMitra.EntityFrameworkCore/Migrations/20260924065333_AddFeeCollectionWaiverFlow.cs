using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    public partial class AddFeeCollectionWaiverFlow : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                table: "Finance_LateFeeWaiverApplications",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Finance_ChargeWaiverApplications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChargeId = table.Column<Guid>(type: "uuid", nullable: false),
                    AcademicYearId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChargeAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    EffectiveAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    AppliedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Finance_ChargeWaiverApplications", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "UX_LateFeeWaiverApplications_Actor_IdempotencyKey",
                table: "Finance_LateFeeWaiverApplications",
                columns: new[] { "AppliedByUserId", "IdempotencyKey" },
                unique: true,
                filter: "\"IdempotencyKey\" IS NOT NULL");
            migrationBuilder.CreateIndex(
                name: "IX_ChargeWaiverApplications_ChargeId",
                table: "Finance_ChargeWaiverApplications",
                column: "ChargeId");
            migrationBuilder.CreateIndex(
                name: "UX_ChargeWaiverApplications_Actor_IdempotencyKey",
                table: "Finance_ChargeWaiverApplications",
                columns: new[] { "AppliedByUserId", "IdempotencyKey" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "Finance_ChargeWaiverApplications");
            migrationBuilder.DropIndex(
                name: "UX_LateFeeWaiverApplications_Actor_IdempotencyKey",
                table: "Finance_LateFeeWaiverApplications");
            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                table: "Finance_LateFeeWaiverApplications");
        }
    }
}
