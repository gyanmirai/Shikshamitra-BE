using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class v6 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AppStudents_AppEducationMediums_EducationMediumId",
                table: "AppStudents");

            migrationBuilder.DropIndex(
                name: "IX_AppStudents_EducationMediumId",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "EducationMediumId",
                table: "AppStudents");

            migrationBuilder.RenameColumn(
                name: "Mother_OfficeAddress_State",
                table: "StudentFamilyDetails",
                newName: "Mother_OfficeAddress_State_Name");

            migrationBuilder.RenameColumn(
                name: "Mother_OfficeAddress_Country",
                table: "StudentFamilyDetails",
                newName: "Mother_OfficeAddress_Country_Name");

            migrationBuilder.RenameColumn(
                name: "Mother_LocalAddress_State",
                table: "StudentFamilyDetails",
                newName: "Mother_LocalAddress_State_Name");

            migrationBuilder.RenameColumn(
                name: "Mother_LocalAddress_Country",
                table: "StudentFamilyDetails",
                newName: "Mother_LocalAddress_Country_Name");

            migrationBuilder.RenameColumn(
                name: "Guardian_OfficeAddress_State",
                table: "StudentFamilyDetails",
                newName: "Guardian_OfficeAddress_State_Name");

            migrationBuilder.RenameColumn(
                name: "Guardian_OfficeAddress_Country",
                table: "StudentFamilyDetails",
                newName: "Guardian_OfficeAddress_Country_Name");

            migrationBuilder.RenameColumn(
                name: "Guardian_LocalAddress_State",
                table: "StudentFamilyDetails",
                newName: "Guardian_LocalAddress_State_Name");

            migrationBuilder.RenameColumn(
                name: "Guardian_LocalAddress_Country",
                table: "StudentFamilyDetails",
                newName: "Guardian_LocalAddress_Country_Name");

            migrationBuilder.RenameColumn(
                name: "Father_OfficeAddress_State",
                table: "StudentFamilyDetails",
                newName: "Father_OfficeAddress_State_Name");

            migrationBuilder.RenameColumn(
                name: "Father_OfficeAddress_Country",
                table: "StudentFamilyDetails",
                newName: "Father_OfficeAddress_Country_Name");

            migrationBuilder.RenameColumn(
                name: "Father_LocalAddress_State",
                table: "StudentFamilyDetails",
                newName: "Father_LocalAddress_State_Name");

            migrationBuilder.RenameColumn(
                name: "Father_LocalAddress_Country",
                table: "StudentFamilyDetails",
                newName: "Father_LocalAddress_Country_Name");

            migrationBuilder.RenameColumn(
                name: "PermanentAddress_State",
                table: "StudentCommunicationDetails",
                newName: "PermanentAddress_State_Name");

            migrationBuilder.RenameColumn(
                name: "PermanentAddress_Country",
                table: "StudentCommunicationDetails",
                newName: "PermanentAddress_Country_Name");

            migrationBuilder.RenameColumn(
                name: "LocalAddress_State",
                table: "StudentCommunicationDetails",
                newName: "LocalAddress_State_Name");

            migrationBuilder.RenameColumn(
                name: "LocalAddress_Country",
                table: "StudentCommunicationDetails",
                newName: "LocalAddress_Country_Name");

            migrationBuilder.RenameColumn(
                name: "EmergencyAddress_State",
                table: "StudentCommunicationDetails",
                newName: "EmergencyAddress_State_Name");

            migrationBuilder.RenameColumn(
                name: "EmergencyAddress_Country",
                table: "StudentCommunicationDetails",
                newName: "EmergencyAddress_Country_Name");

            migrationBuilder.AddColumn<string>(
                name: "Father_LocalAddress_Country_IsoCode",
                table: "StudentFamilyDetails",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Father_LocalAddress_State_IsoCode",
                table: "StudentFamilyDetails",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Father_OfficeAddress_Country_IsoCode",
                table: "StudentFamilyDetails",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Father_OfficeAddress_State_IsoCode",
                table: "StudentFamilyDetails",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Guardian_LocalAddress_Country_IsoCode",
                table: "StudentFamilyDetails",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Guardian_LocalAddress_State_IsoCode",
                table: "StudentFamilyDetails",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Guardian_OfficeAddress_Country_IsoCode",
                table: "StudentFamilyDetails",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Guardian_OfficeAddress_State_IsoCode",
                table: "StudentFamilyDetails",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Mother_LocalAddress_Country_IsoCode",
                table: "StudentFamilyDetails",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Mother_LocalAddress_State_IsoCode",
                table: "StudentFamilyDetails",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Mother_OfficeAddress_Country_IsoCode",
                table: "StudentFamilyDetails",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Mother_OfficeAddress_State_IsoCode",
                table: "StudentFamilyDetails",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmergencyAddress_Country_IsoCode",
                table: "StudentCommunicationDetails",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmergencyAddress_State_IsoCode",
                table: "StudentCommunicationDetails",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LocalAddress_Country_IsoCode",
                table: "StudentCommunicationDetails",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LocalAddress_State_IsoCode",
                table: "StudentCommunicationDetails",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PermanentAddress_Country_IsoCode",
                table: "StudentCommunicationDetails",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PermanentAddress_State_IsoCode",
                table: "StudentCommunicationDetails",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "HR_SalarySlips",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    MonthYear = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    Month = table.Column<int>(type: "integer", nullable: false),
                    TotalEarning = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalDeduction = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    NetSalary = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ProcessedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Remarks = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Earnings = table.Column<string>(type: "jsonb", nullable: false),
                    Deductions = table.Column<string>(type: "jsonb", nullable: false),
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
                    table.PrimaryKey("PK_HR_SalarySlips", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppStudents_AcademicMediumId",
                table: "AppStudents",
                column: "AcademicMediumId");

            migrationBuilder.CreateIndex(
                name: "IX_HR_SalarySlips_EmployeeId",
                table: "HR_SalarySlips",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_HR_SalarySlips_EmployeeId_MonthYear",
                table: "HR_SalarySlips",
                columns: new[] { "EmployeeId", "MonthYear" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HR_SalarySlips_Status",
                table: "HR_SalarySlips",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_HR_SalarySlips_TenantId",
                table: "HR_SalarySlips",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_HR_SalarySlips_Year_Month",
                table: "HR_SalarySlips",
                columns: new[] { "Year", "Month" });

            migrationBuilder.AddForeignKey(
                name: "FK_AppStudents_AppEducationMediums_AcademicMediumId",
                table: "AppStudents",
                column: "AcademicMediumId",
                principalTable: "AppEducationMediums",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AppStudents_AppEducationMediums_AcademicMediumId",
                table: "AppStudents");

            migrationBuilder.DropTable(
                name: "HR_SalarySlips");

            migrationBuilder.DropIndex(
                name: "IX_AppStudents_AcademicMediumId",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "Father_LocalAddress_Country_IsoCode",
                table: "StudentFamilyDetails");

            migrationBuilder.DropColumn(
                name: "Father_LocalAddress_State_IsoCode",
                table: "StudentFamilyDetails");

            migrationBuilder.DropColumn(
                name: "Father_OfficeAddress_Country_IsoCode",
                table: "StudentFamilyDetails");

            migrationBuilder.DropColumn(
                name: "Father_OfficeAddress_State_IsoCode",
                table: "StudentFamilyDetails");

            migrationBuilder.DropColumn(
                name: "Guardian_LocalAddress_Country_IsoCode",
                table: "StudentFamilyDetails");

            migrationBuilder.DropColumn(
                name: "Guardian_LocalAddress_State_IsoCode",
                table: "StudentFamilyDetails");

            migrationBuilder.DropColumn(
                name: "Guardian_OfficeAddress_Country_IsoCode",
                table: "StudentFamilyDetails");

            migrationBuilder.DropColumn(
                name: "Guardian_OfficeAddress_State_IsoCode",
                table: "StudentFamilyDetails");

            migrationBuilder.DropColumn(
                name: "Mother_LocalAddress_Country_IsoCode",
                table: "StudentFamilyDetails");

            migrationBuilder.DropColumn(
                name: "Mother_LocalAddress_State_IsoCode",
                table: "StudentFamilyDetails");

            migrationBuilder.DropColumn(
                name: "Mother_OfficeAddress_Country_IsoCode",
                table: "StudentFamilyDetails");

            migrationBuilder.DropColumn(
                name: "Mother_OfficeAddress_State_IsoCode",
                table: "StudentFamilyDetails");

            migrationBuilder.DropColumn(
                name: "EmergencyAddress_Country_IsoCode",
                table: "StudentCommunicationDetails");

            migrationBuilder.DropColumn(
                name: "EmergencyAddress_State_IsoCode",
                table: "StudentCommunicationDetails");

            migrationBuilder.DropColumn(
                name: "LocalAddress_Country_IsoCode",
                table: "StudentCommunicationDetails");

            migrationBuilder.DropColumn(
                name: "LocalAddress_State_IsoCode",
                table: "StudentCommunicationDetails");

            migrationBuilder.DropColumn(
                name: "PermanentAddress_Country_IsoCode",
                table: "StudentCommunicationDetails");

            migrationBuilder.DropColumn(
                name: "PermanentAddress_State_IsoCode",
                table: "StudentCommunicationDetails");

            migrationBuilder.RenameColumn(
                name: "Mother_OfficeAddress_State_Name",
                table: "StudentFamilyDetails",
                newName: "Mother_OfficeAddress_State");

            migrationBuilder.RenameColumn(
                name: "Mother_OfficeAddress_Country_Name",
                table: "StudentFamilyDetails",
                newName: "Mother_OfficeAddress_Country");

            migrationBuilder.RenameColumn(
                name: "Mother_LocalAddress_State_Name",
                table: "StudentFamilyDetails",
                newName: "Mother_LocalAddress_State");

            migrationBuilder.RenameColumn(
                name: "Mother_LocalAddress_Country_Name",
                table: "StudentFamilyDetails",
                newName: "Mother_LocalAddress_Country");

            migrationBuilder.RenameColumn(
                name: "Guardian_OfficeAddress_State_Name",
                table: "StudentFamilyDetails",
                newName: "Guardian_OfficeAddress_State");

            migrationBuilder.RenameColumn(
                name: "Guardian_OfficeAddress_Country_Name",
                table: "StudentFamilyDetails",
                newName: "Guardian_OfficeAddress_Country");

            migrationBuilder.RenameColumn(
                name: "Guardian_LocalAddress_State_Name",
                table: "StudentFamilyDetails",
                newName: "Guardian_LocalAddress_State");

            migrationBuilder.RenameColumn(
                name: "Guardian_LocalAddress_Country_Name",
                table: "StudentFamilyDetails",
                newName: "Guardian_LocalAddress_Country");

            migrationBuilder.RenameColumn(
                name: "Father_OfficeAddress_State_Name",
                table: "StudentFamilyDetails",
                newName: "Father_OfficeAddress_State");

            migrationBuilder.RenameColumn(
                name: "Father_OfficeAddress_Country_Name",
                table: "StudentFamilyDetails",
                newName: "Father_OfficeAddress_Country");

            migrationBuilder.RenameColumn(
                name: "Father_LocalAddress_State_Name",
                table: "StudentFamilyDetails",
                newName: "Father_LocalAddress_State");

            migrationBuilder.RenameColumn(
                name: "Father_LocalAddress_Country_Name",
                table: "StudentFamilyDetails",
                newName: "Father_LocalAddress_Country");

            migrationBuilder.RenameColumn(
                name: "PermanentAddress_State_Name",
                table: "StudentCommunicationDetails",
                newName: "PermanentAddress_State");

            migrationBuilder.RenameColumn(
                name: "PermanentAddress_Country_Name",
                table: "StudentCommunicationDetails",
                newName: "PermanentAddress_Country");

            migrationBuilder.RenameColumn(
                name: "LocalAddress_State_Name",
                table: "StudentCommunicationDetails",
                newName: "LocalAddress_State");

            migrationBuilder.RenameColumn(
                name: "LocalAddress_Country_Name",
                table: "StudentCommunicationDetails",
                newName: "LocalAddress_Country");

            migrationBuilder.RenameColumn(
                name: "EmergencyAddress_State_Name",
                table: "StudentCommunicationDetails",
                newName: "EmergencyAddress_State");

            migrationBuilder.RenameColumn(
                name: "EmergencyAddress_Country_Name",
                table: "StudentCommunicationDetails",
                newName: "EmergencyAddress_Country");

            migrationBuilder.AddColumn<Guid>(
                name: "EducationMediumId",
                table: "AppStudents",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppStudents_EducationMediumId",
                table: "AppStudents",
                column: "EducationMediumId");

            migrationBuilder.AddForeignKey(
                name: "FK_AppStudents_AppEducationMediums_EducationMediumId",
                table: "AppStudents",
                column: "EducationMediumId",
                principalTable: "AppEducationMediums",
                principalColumn: "Id");
        }
    }
}
