using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations;

public partial class AddAdmissionDecisionAndStudentEnrollment : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTime>(name: "DecisionAt", table: "AppAdmissionApplications", type: "timestamp with time zone", nullable: true);
        migrationBuilder.AddColumn<Guid>(name: "DecisionBy", table: "AppAdmissionApplications", type: "uuid", nullable: true);
        migrationBuilder.AddColumn<string>(name: "DecisionReason", table: "AppAdmissionApplications", type: "text", nullable: true);
        migrationBuilder.AddColumn<Guid>(name: "EnrolledStudentId", table: "AppAdmissionApplications", type: "uuid", nullable: true);
        migrationBuilder.AddColumn<DateTime>(name: "OfferAcceptedAt", table: "AppAdmissionApplications", type: "timestamp with time zone", nullable: true);
        migrationBuilder.AddColumn<DateTime>(name: "OfferExpiresAt", table: "AppAdmissionApplications", type: "timestamp with time zone", nullable: true);
        migrationBuilder.AddColumn<Guid>(name: "StudentEnrollmentId", table: "AppAdmissionApplications", type: "uuid", nullable: true);

        migrationBuilder.CreateTable(
            name: "AppStudentEnrollments",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                AcademicYearId = table.Column<Guid>(type: "uuid", nullable: false),
                StandardId = table.Column<Guid>(type: "uuid", nullable: true),
                DivisionId = table.Column<Guid>(type: "uuid", nullable: true),
                IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
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
                table.PrimaryKey("PK_AppStudentEnrollments", x => x.Id);
                table.ForeignKey("FK_AppStudentEnrollments_AppStudents_StudentId", x => x.StudentId, "AppStudents", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "AppAdmissionApplicationDecisionHistories",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                ApplicationId = table.Column<Guid>(type: "uuid", nullable: false),
                Status = table.Column<int>(type: "integer", nullable: false),
                Reason = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                ActorId = table.Column<Guid>(type: "uuid", nullable: true),
                OccurredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
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
                table.PrimaryKey("PK_AppAdmissionApplicationDecisionHistories", x => x.Id);
                table.ForeignKey("FK_AppAdmissionApplicationDecisionHistories_AppAdmissionApplications_ApplicationId", x => x.ApplicationId, "AppAdmissionApplications", "Id", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(name: "IX_AppStudentEnrollments_TenantId_StudentId_AcademicYearId", table: "AppStudentEnrollments", columns: new[] { "TenantId", "StudentId", "AcademicYearId" }, unique: true);
        migrationBuilder.CreateIndex(name: "IX_AppStudentEnrollments_TenantId_AcademicYearId_StandardId_DivisionId", table: "AppStudentEnrollments", columns: new[] { "TenantId", "AcademicYearId", "StandardId", "DivisionId" });
        migrationBuilder.CreateIndex(name: "IX_AppAdmissionApplicationDecisionHistories_ApplicationId", table: "AppAdmissionApplicationDecisionHistories", column: "ApplicationId");
        migrationBuilder.CreateIndex(name: "IX_AppAdmissionApplicationDecisionHistories_TenantId_ApplicationId_OccurredAt", table: "AppAdmissionApplicationDecisionHistories", columns: new[] { "TenantId", "ApplicationId", "OccurredAt" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "AppAdmissionApplicationDecisionHistories");
        migrationBuilder.DropTable(name: "AppStudentEnrollments");
        migrationBuilder.DropColumn(name: "DecisionAt", table: "AppAdmissionApplications");
        migrationBuilder.DropColumn(name: "DecisionBy", table: "AppAdmissionApplications");
        migrationBuilder.DropColumn(name: "DecisionReason", table: "AppAdmissionApplications");
        migrationBuilder.DropColumn(name: "EnrolledStudentId", table: "AppAdmissionApplications");
        migrationBuilder.DropColumn(name: "OfferAcceptedAt", table: "AppAdmissionApplications");
        migrationBuilder.DropColumn(name: "OfferExpiresAt", table: "AppAdmissionApplications");
        migrationBuilder.DropColumn(name: "StudentEnrollmentId", table: "AppAdmissionApplications");
    }
}
