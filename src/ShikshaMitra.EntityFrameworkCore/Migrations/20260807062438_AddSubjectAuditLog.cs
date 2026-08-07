using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class AddSubjectAuditLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppSubjectAuditLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    SubjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    OldValueJson = table.Column<string>(type: "text", nullable: false),
                    NewValueJson = table.Column<string>(type: "text", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ChangedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChangedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Module = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SubModule = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    EntityType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    EntityName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ChangedFieldsJson = table.Column<string>(type: "text", nullable: true),
                    HasDiff = table.Column<bool>(type: "boolean", nullable: false),
                    ActorNameSnapshot = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ActorRoleSnapshot = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ActorEmailSnapshot = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Source = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    IpAddress = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Device = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Browser = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    OperatingSystem = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    RequestPath = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    HttpMethod = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    CorrelationId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
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
                    table.PrimaryKey("PK_AppSubjectAuditLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppSubjectAuditLogs_ChangedAt",
                table: "AppSubjectAuditLogs",
                column: "ChangedAt");

            migrationBuilder.CreateIndex(
                name: "IX_AppSubjectAuditLogs_SubjectId",
                table: "AppSubjectAuditLogs",
                column: "SubjectId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppSubjectAuditLogs");
        }
    }
}
