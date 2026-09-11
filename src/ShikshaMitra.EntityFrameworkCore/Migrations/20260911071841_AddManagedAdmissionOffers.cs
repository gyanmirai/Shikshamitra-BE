using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class AddManagedAdmissionOffers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Action",
                table: "AppAdmissionApplicationDecisionHistories",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                table: "AppAdmissionApplicationDecisionHistories",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MetadataJson",
                table: "AppAdmissionApplicationDecisionHistories",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "StageInstanceId",
                table: "AppAdmissionApplicationDecisionHistories",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "UX_AdmissionDecisionHistory_Stage_Idempotency",
                table: "AppAdmissionApplicationDecisionHistories",
                columns: new[] { "TenantId", "ApplicationId", "StageInstanceId", "IdempotencyKey" },
                unique: true,
                filter: "\"IsDeleted\" = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_AdmissionDecisionHistory_Stage_Idempotency",
                table: "AppAdmissionApplicationDecisionHistories");

            migrationBuilder.DropColumn(
                name: "Action",
                table: "AppAdmissionApplicationDecisionHistories");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                table: "AppAdmissionApplicationDecisionHistories");

            migrationBuilder.DropColumn(
                name: "MetadataJson",
                table: "AppAdmissionApplicationDecisionHistories");

            migrationBuilder.DropColumn(
                name: "StageInstanceId",
                table: "AppAdmissionApplicationDecisionHistories");
        }
    }
}
