using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <summary>Add reviewed Admission return provenance without rewriting legacy handoffs.</summary>
    public partial class AddStudentRecordAdmissionReturn : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(name: "SourceEnrollmentId", table: "AppStudentRecordTransitions",
                type: "uuid", nullable: true, oldClrType: typeof(Guid), oldType: "uuid");
            migrationBuilder.AlterColumn<Guid>(name: "AdmissionApplicationId", table: "AppStudentRecordReturnHandoffs",
                type: "uuid", nullable: true, oldClrType: typeof(Guid), oldType: "uuid");
            migrationBuilder.AddColumn<Guid>(name: "AdmissionInquiryId", table: "AppStudentRecordReturnHandoffs", type: "uuid", nullable: true);
            migrationBuilder.AddColumn<Guid>(name: "AdmissionOfferingId", table: "AppStudentRecordReturnHandoffs", type: "uuid", nullable: true);
            migrationBuilder.AddColumn<Guid>(name: "TransitionId", table: "AppStudentRecordReturnHandoffs", type: "uuid", nullable: true);
            migrationBuilder.AddColumn<int>(name: "EntryPath", table: "AppStudentRecordReturnHandoffs", type: "integer", nullable: true);
            migrationBuilder.AddColumn<string>(name: "SourceSnapshotJson", table: "AppStudentRecordReturnHandoffs", type: "text", nullable: false, defaultValue: "{}");
            migrationBuilder.CreateIndex(name: "IX_AppStudentRecordReturnHandoffs_TenantId_TransitionId", table: "AppStudentRecordReturnHandoffs",
                columns: new[] { "TenantId", "TransitionId" }, unique: true, filter: "\"TransitionId\" IS NOT NULL");
            migrationBuilder.CreateIndex(name: "IX_AppStudentRecordReturnHandoffs_TenantId_AdmissionApplicatio~", table: "AppStudentRecordReturnHandoffs",
                columns: new[] { "TenantId", "AdmissionApplicationId" }, unique: true, filter: "\"TransitionId\" IS NOT NULL AND \"AdmissionApplicationId\" IS NOT NULL");
            migrationBuilder.CreateIndex(name: "IX_AppStudentRecordReturnHandoffs_TenantId_AdmissionInquiryId", table: "AppStudentRecordReturnHandoffs",
                columns: new[] { "TenantId", "AdmissionInquiryId" }, unique: true, filter: "\"TransitionId\" IS NOT NULL AND \"AdmissionInquiryId\" IS NOT NULL");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $records_return_rollback$
                BEGIN
                    IF EXISTS (SELECT 1 FROM "AppStudentRecordReturnHandoffs" WHERE "TransitionId" IS NOT NULL
                        OR "AdmissionInquiryId" IS NOT NULL OR "AdmissionApplicationId" IS NULL)
                        OR EXISTS (SELECT 1 FROM "AppStudentRecordTransitions" WHERE "SourceEnrollmentId" IS NULL) THEN
                        RAISE EXCEPTION 'Student Records rollback cannot retain reviewed Admission return history; use a forward fix.';
                    END IF;
                END $records_return_rollback$;
                """);
            migrationBuilder.DropIndex(name: "IX_AppStudentRecordReturnHandoffs_TenantId_TransitionId", table: "AppStudentRecordReturnHandoffs");
            migrationBuilder.DropIndex(name: "IX_AppStudentRecordReturnHandoffs_TenantId_AdmissionApplicatio~", table: "AppStudentRecordReturnHandoffs");
            migrationBuilder.DropIndex(name: "IX_AppStudentRecordReturnHandoffs_TenantId_AdmissionInquiryId", table: "AppStudentRecordReturnHandoffs");
            migrationBuilder.DropColumn(name: "AdmissionInquiryId", table: "AppStudentRecordReturnHandoffs");
            migrationBuilder.DropColumn(name: "AdmissionOfferingId", table: "AppStudentRecordReturnHandoffs");
            migrationBuilder.DropColumn(name: "TransitionId", table: "AppStudentRecordReturnHandoffs");
            migrationBuilder.DropColumn(name: "EntryPath", table: "AppStudentRecordReturnHandoffs");
            migrationBuilder.DropColumn(name: "SourceSnapshotJson", table: "AppStudentRecordReturnHandoffs");
            migrationBuilder.AlterColumn<Guid>(name: "AdmissionApplicationId", table: "AppStudentRecordReturnHandoffs",
                type: "uuid", nullable: false, oldClrType: typeof(Guid), oldType: "uuid", oldNullable: true);
            migrationBuilder.AlterColumn<Guid>(name: "SourceEnrollmentId", table: "AppStudentRecordTransitions",
                type: "uuid", nullable: false, oldClrType: typeof(Guid), oldType: "uuid", oldNullable: true);
        }
    }
}
