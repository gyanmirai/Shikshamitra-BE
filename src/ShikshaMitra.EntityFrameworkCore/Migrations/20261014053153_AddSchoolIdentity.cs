using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class AddSchoolIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppSchoolIdentityAssets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    EducationBoardId = table.Column<Guid>(type: "uuid", nullable: true),
                    EducationMediumId = table.Column<Guid>(type: "uuid", nullable: true),
                    BlobName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    FileName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Size = table.Column<long>(type: "bigint", nullable: false),
                    Width = table.Column<int>(type: "integer", nullable: false),
                    Height = table.Column<int>(type: "integer", nullable: false),
                    IsRetained = table.Column<bool>(type: "boolean", nullable: false),
                    IsCleanupPending = table.Column<bool>(type: "boolean", nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppSchoolIdentityAssets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppSchoolIdentityProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    AcademicYearId = table.Column<Guid>(type: "uuid", nullable: true),
                    EducationBoardId = table.Column<Guid>(type: "uuid", nullable: true),
                    EducationMediumId = table.Column<Guid>(type: "uuid", nullable: true),
                    ScopeKey = table.Column<string>(type: "character varying(96)", maxLength: 96, nullable: false),
                    ChoicesJson = table.Column<string>(type: "character varying(8192)", maxLength: 8192, nullable: false),
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
                    table.PrimaryKey("PK_AppSchoolIdentityProfiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppSchoolIdentityRevisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChoicesJson = table.Column<string>(type: "character varying(8192)", maxLength: 8192, nullable: false),
                    Reason = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppSchoolIdentityRevisions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppSchoolIdentityAssets_TenantId_IsRetained_CreationTime",
                table: "AppSchoolIdentityAssets",
                columns: new[] { "TenantId", "IsRetained", "CreationTime" });

            migrationBuilder.CreateIndex(
                name: "IX_AppSchoolIdentityProfiles_TenantId_AcademicYearId",
                table: "AppSchoolIdentityProfiles",
                columns: new[] { "TenantId", "AcademicYearId" });

            migrationBuilder.CreateIndex(
                name: "IX_AppSchoolIdentityProfiles_TenantId_ScopeKey",
                table: "AppSchoolIdentityProfiles",
                columns: new[] { "TenantId", "ScopeKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppSchoolIdentityRevisions_TenantId_ProfileId_CreationTime",
                table: "AppSchoolIdentityRevisions",
                columns: new[] { "TenantId", "ProfileId", "CreationTime" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "AppSchoolIdentityAssets");
            migrationBuilder.DropTable(name: "AppSchoolIdentityProfiles");
            migrationBuilder.DropTable(name: "AppSchoolIdentityRevisions");
        }
    }
}
