using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class AddChargeRequestBatch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ChargeName",
                table: "Charges",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "ChargeRequestId",
                table: "Charges",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "ChargeRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    AcademicYearId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChargeName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ChargeCategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    PurposeDescription = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    DueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    BoardId = table.Column<Guid>(type: "uuid", nullable: false),
                    EducationMediumId = table.Column<Guid>(type: "uuid", nullable: false),
                    AdditionalNotes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    RequestorId = table.Column<Guid>(type: "uuid", nullable: false),
                    AppliedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AudienceSnapshot = table.Column<string>(type: "text", nullable: false),
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
                    table.PrimaryKey("PK_ChargeRequests", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Charges_ChargeRequestId",
                table: "Charges",
                column: "ChargeRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_ChargeRequests_AcademicYear_Board_Medium",
                table: "ChargeRequests",
                columns: new[] { "AcademicYearId", "BoardId", "EducationMediumId" });

            // Backfill: one synthetic ChargeRequest per existing Charge (1:1)
            migrationBuilder.Sql("""
                INSERT INTO "ChargeRequests" (
                    "Id", "TenantId", "AcademicYearId", "ChargeName", "ChargeCategoryId",
                    "PurposeDescription", "Amount", "DueDate", "Priority", "BoardId",
                    "EducationMediumId", "AdditionalNotes", "RequestorId", "AppliedOn",
                    "AudienceSnapshot", "ExtraProperties", "ConcurrencyStamp",
                    "CreationTime", "CreatorId", "IsDeleted")
                SELECT
                    c."Id",
                    c."TenantId",
                    c."AcademicYearId",
                    LEFT(c."PurposeDescription", 100),
                    c."ChargeCategoryId",
                    c."PurposeDescription",
                    c."Amount",
                    c."DueDate",
                    c."Priority",
                    c."BoardId",
                    c."EducationMediumId",
                    c."AdditionalNotes",
                    c."RequestorId",
                    c."AppliedOn",
                    '{}',
                    '{}',
                    gen_random_uuid()::text,
                    c."CreationTime",
                    c."CreatorId",
                    c."IsDeleted"
                FROM "Charges" c
                WHERE c."ChargeRequestId" = '00000000-0000-0000-0000-000000000000';

                UPDATE "Charges"
                SET "ChargeRequestId" = "Id",
                    "ChargeName" = LEFT("PurposeDescription", 100)
                WHERE "ChargeRequestId" = '00000000-0000-0000-0000-000000000000';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChargeRequests");

            migrationBuilder.DropIndex(
                name: "IX_Charges_ChargeRequestId",
                table: "Charges");

            migrationBuilder.DropColumn(
                name: "ChargeName",
                table: "Charges");

            migrationBuilder.DropColumn(
                name: "ChargeRequestId",
                table: "Charges");
        }
    }
}
