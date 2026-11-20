using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class AddLibraryPoliciesAndRecipientInbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RepeatIntervalDays",
                table: "AppLibraryNotificationSettings",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReadAt",
                table: "AppLibraryNotificationHistories",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RecipientUserId",
                table: "AppLibraryNotificationHistories",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AppLibraryBorrowingPolicyVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    OriginAcademicYearId = table.Column<Guid>(type: "uuid", nullable: false),
                    PolicyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Role = table.Column<int>(type: "integer", nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveTo = table.Column<DateOnly>(type: "date", nullable: true),
                    BookCategoryId = table.Column<Guid>(type: "uuid", nullable: true),
                    SectionId = table.Column<Guid>(type: "uuid", nullable: true),
                    EducationMediumStageId = table.Column<Guid>(type: "uuid", nullable: true),
                    StandardId = table.Column<Guid>(type: "uuid", nullable: true),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    MaxBooksAtTime = table.Column<int>(type: "integer", nullable: false),
                    ReservationLimit = table.Column<int>(type: "integer", nullable: false),
                    LoanDays = table.Column<int>(type: "integer", nullable: false),
                    DayCountingMode = table.Column<int>(type: "integer", nullable: false),
                    MaxRenewals = table.Column<int>(type: "integer", nullable: false),
                    RenewalDays = table.Column<int>(type: "integer", nullable: false),
                    PickupWindowDays = table.Column<int>(type: "integer", nullable: false),
                    MoveDueDateToNextOpenDay = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ArchivedFrom = table.Column<DateOnly>(type: "date", nullable: true),
                    ArchiveReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_AppLibraryBorrowingPolicyVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppLibraryBorrowingPolicyVersions_AppBookCategories_BookCat~",
                        column: x => x.BookCategoryId,
                        principalTable: "AppBookCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppLibraryBorrowingPolicyVersions_AppLibrarySections_Sectio~",
                        column: x => x.SectionId,
                        principalTable: "AppLibrarySections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppLibraryBorrowingPolicyVersions_academic_EducationMediumS~",
                        column: x => x.EducationMediumStageId,
                        principalTable: "academic_EducationMediumStages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppLibraryBorrowingPolicyVersions_academic_Standards_Standa~",
                        column: x => x.StandardId,
                        principalTable: "academic_Standards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppLibraryBorrowingPolicyVersions_academic_Years_OriginAcad~",
                        column: x => x.OriginAcademicYearId,
                        principalTable: "academic_Years",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AppLibraryCalendarClosures",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    FromDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ToDate = table.Column<DateOnly>(type: "date", nullable: false),
                    SectionId = table.Column<Guid>(type: "uuid", nullable: true),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
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
                    table.PrimaryKey("PK_AppLibraryCalendarClosures", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppLibraryCalendarClosures_AppLibrarySections_SectionId",
                        column: x => x.SectionId,
                        principalTable: "AppLibrarySections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppLibraryNotificationHistories_TenantId_RecipientUserId_Ch~",
                table: "AppLibraryNotificationHistories",
                columns: new[] { "TenantId", "RecipientUserId", "Channel", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_AppLibraryBorrowingPolicyVersions_BookCategoryId",
                table: "AppLibraryBorrowingPolicyVersions",
                column: "BookCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_AppLibraryBorrowingPolicyVersions_EducationMediumStageId",
                table: "AppLibraryBorrowingPolicyVersions",
                column: "EducationMediumStageId");

            migrationBuilder.CreateIndex(
                name: "IX_AppLibraryBorrowingPolicyVersions_OriginAcademicYearId",
                table: "AppLibraryBorrowingPolicyVersions",
                column: "OriginAcademicYearId");

            migrationBuilder.CreateIndex(
                name: "IX_AppLibraryBorrowingPolicyVersions_SectionId",
                table: "AppLibraryBorrowingPolicyVersions",
                column: "SectionId");

            migrationBuilder.CreateIndex(
                name: "IX_AppLibraryBorrowingPolicyVersions_StandardId",
                table: "AppLibraryBorrowingPolicyVersions",
                column: "StandardId");

            migrationBuilder.CreateIndex(
                name: "IX_AppLibraryBorrowingPolicyVersions_TenantId_PolicyId_Version",
                table: "AppLibraryBorrowingPolicyVersions",
                columns: new[] { "TenantId", "PolicyId", "Version" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_AppLibraryBorrowingPolicyVersions_TenantId_Role_Status_Effe~",
                table: "AppLibraryBorrowingPolicyVersions",
                columns: new[] { "TenantId", "Role", "Status", "EffectiveFrom", "EffectiveTo" });

            migrationBuilder.CreateIndex(
                name: "IX_AppLibraryCalendarClosures_SectionId",
                table: "AppLibraryCalendarClosures",
                column: "SectionId");

            migrationBuilder.CreateIndex(
                name: "IX_AppLibraryCalendarClosures_TenantId_FromDate_ToDate_Section~",
                table: "AppLibraryCalendarClosures",
                columns: new[] { "TenantId", "FromDate", "ToDate", "SectionId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM "AppLibraryBorrowingPolicyVersions")
                        OR EXISTS (SELECT 1 FROM "AppLibraryCalendarClosures")
                        OR EXISTS (SELECT 1 FROM "AppLibraryNotificationHistories"
                            WHERE "RecipientUserId" IS NOT NULL OR "ReadAt" IS NOT NULL)
                        OR EXISTS (SELECT 1 FROM "AppLibraryNotificationSettings" WHERE "RepeatIntervalDays" <> 1)
                    THEN
                        RAISE EXCEPTION 'Library policy/inbox rollback cannot preserve existing policy, calendar, recipient or recurrence evidence. Use a forward migration.';
                    END IF;
                END $$;
                """);

            migrationBuilder.DropTable(
                name: "AppLibraryBorrowingPolicyVersions");

            migrationBuilder.DropTable(
                name: "AppLibraryCalendarClosures");

            migrationBuilder.DropIndex(
                name: "IX_AppLibraryNotificationHistories_TenantId_RecipientUserId_Ch~",
                table: "AppLibraryNotificationHistories");

            migrationBuilder.DropColumn(
                name: "RepeatIntervalDays",
                table: "AppLibraryNotificationSettings");

            migrationBuilder.DropColumn(
                name: "ReadAt",
                table: "AppLibraryNotificationHistories");

            migrationBuilder.DropColumn(
                name: "RecipientUserId",
                table: "AppLibraryNotificationHistories");
        }
    }
}
