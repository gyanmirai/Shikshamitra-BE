using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class AddAdmissionCrmPlatformSetup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppAdmissionCrmPlatformPresets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ConfigurationJson = table.Column<string>(type: "jsonb", nullable: false),
                    AccessPolicyJson = table.Column<string>(type: "jsonb", nullable: false),
                    ReleaseNotes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    PublishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_AppAdmissionCrmPlatformPresets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppAdmissionCrmPlatformSupportSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OperatorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetTenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_AppAdmissionCrmPlatformSupportSessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppAdmissionCrmPresetAdoptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    AcademicYearId = table.Column<Guid>(type: "uuid", nullable: false),
                    PresetId = table.Column<Guid>(type: "uuid", nullable: false),
                    PresetVersion = table.Column<int>(type: "integer", nullable: false),
                    SnapshotHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PreviousConfigurationJson = table.Column<string>(type: "jsonb", nullable: false),
                    AppliedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RolledBackAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_AppAdmissionCrmPresetAdoptions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppAdmissionCrmSettingsAccessPolicies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    SettingsKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SectionKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    AccessMode = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_AppAdmissionCrmSettingsAccessPolicies", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppAdmissionCrmPlatformPresets_Code_Version",
                table: "AppAdmissionCrmPlatformPresets",
                columns: new[] { "Code", "Version" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_AppAdmissionCrmPlatformSupportSessions_OperatorUserId_Targe~",
                table: "AppAdmissionCrmPlatformSupportSessions",
                columns: new[] { "OperatorUserId", "TargetTenantId", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AppAdmissionCrmPresetAdoptions_TenantId_AcademicYearId_Appl~",
                table: "AppAdmissionCrmPresetAdoptions",
                columns: new[] { "TenantId", "AcademicYearId", "AppliedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AppAdmissionCrmSettingsAccessPolicies_TenantId_SettingsKey_~",
                table: "AppAdmissionCrmSettingsAccessPolicies",
                columns: new[] { "TenantId", "SettingsKey", "SectionKey" },
                unique: true,
                filter: "\"IsDeleted\" = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppAdmissionCrmPlatformPresets");

            migrationBuilder.DropTable(
                name: "AppAdmissionCrmPlatformSupportSessions");

            migrationBuilder.DropTable(
                name: "AppAdmissionCrmPresetAdoptions");

            migrationBuilder.DropTable(
                name: "AppAdmissionCrmSettingsAccessPolicies");
        }
    }
}
