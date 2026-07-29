using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class AddAdmissionCrmInquiryTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "InquiryTypeId",
                table: "AppAdmissionInquiryLeads",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InquiryTypeSnapshot",
                table: "AppAdmissionInquiryLeads",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AppAdmissionCrmInquiryTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    AcademicYearId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    DeactivationReason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
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
                    table.PrimaryKey("PK_AppAdmissionCrmInquiryTypes", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppAdmissionInquiryLeads_InquiryTypeId",
                table: "AppAdmissionInquiryLeads",
                column: "InquiryTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_AppAdmissionInquiryLeads_TenantId_AcademicYearId_InquiryTyp~",
                table: "AppAdmissionInquiryLeads",
                columns: new[] { "TenantId", "AcademicYearId", "InquiryTypeId" });

            migrationBuilder.CreateIndex(
                name: "IX_AppAdmissionCrmInquiryTypes_TenantId_AcademicYearId_Code",
                table: "AppAdmissionCrmInquiryTypes",
                columns: new[] { "TenantId", "AcademicYearId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppAdmissionCrmInquiryTypes_TenantId_AcademicYearId_IsActiv~",
                table: "AppAdmissionCrmInquiryTypes",
                columns: new[] { "TenantId", "AcademicYearId", "IsActive", "DisplayOrder" });

            migrationBuilder.AddForeignKey(
                name: "FK_AppAdmissionInquiryLeads_AppAdmissionCrmInquiryTypes_Inquir~",
                table: "AppAdmissionInquiryLeads",
                column: "InquiryTypeId",
                principalTable: "AppAdmissionCrmInquiryTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AppAdmissionInquiryLeads_AppAdmissionCrmInquiryTypes_Inquir~",
                table: "AppAdmissionInquiryLeads");

            migrationBuilder.DropTable(
                name: "AppAdmissionCrmInquiryTypes");

            migrationBuilder.DropIndex(
                name: "IX_AppAdmissionInquiryLeads_InquiryTypeId",
                table: "AppAdmissionInquiryLeads");

            migrationBuilder.DropIndex(
                name: "IX_AppAdmissionInquiryLeads_TenantId_AcademicYearId_InquiryTyp~",
                table: "AppAdmissionInquiryLeads");

            migrationBuilder.DropColumn(
                name: "InquiryTypeId",
                table: "AppAdmissionInquiryLeads");

            migrationBuilder.DropColumn(
                name: "InquiryTypeSnapshot",
                table: "AppAdmissionInquiryLeads");
        }
    }
}
