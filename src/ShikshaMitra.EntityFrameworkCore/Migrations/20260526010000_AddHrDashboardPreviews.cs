using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    public partial class AddHrDashboardPreviews : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HR_DashboardPreviews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    DashboardKey = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    PayloadJson = table.Column<string>(type: "jsonb", nullable: false),
                    PayloadVersion = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    LastRefreshedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    RefreshStartedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    RefreshRequestedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastError = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HR_DashboardPreviews", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HR_DashboardPreviews_Status_RefreshStartedAt",
                table: "HR_DashboardPreviews",
                columns: new[] { "Status", "RefreshStartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_HR_DashboardPreviews_Tenant_Key",
                table: "HR_DashboardPreviews",
                columns: new[] { "TenantId", "DashboardKey" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HR_DashboardPreviews");
        }
    }
}
