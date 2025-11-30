using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class v36_AddUserScopeAssignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "EducationMediumStageId",
                table: "AppStudentEnrollments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "EducationBoardId",
                table: "AppHomeworkAssignments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "EducationMediumId",
                table: "AppHomeworkAssignments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "EducationMediumStageId",
                table: "AppHomeworkAssignments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "EducationBoardId",
                table: "AppClassTeachers",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "EducationMediumId",
                table: "AppClassTeachers",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "EducationMediumStageId",
                table: "AppClassTeachers",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "UserScopeAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    RoleName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    AcademicYearId = table.Column<Guid>(type: "uuid", nullable: true),
                    EducationBoardId = table.Column<Guid>(type: "uuid", nullable: true),
                    EducationMediumId = table.Column<Guid>(type: "uuid", nullable: true),
                    EducationMediumStageId = table.Column<Guid>(type: "uuid", nullable: true),
                    StandardId = table.Column<Guid>(type: "uuid", nullable: true),
                    DivisionId = table.Column<Guid>(type: "uuid", nullable: true),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserScopeAssignments", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserScopeAssignments_Lookup",
                table: "UserScopeAssignments",
                columns: new[] { "TenantId", "UserId", "IsActive", "IsDeleted" })
                .Annotation("Npgsql:IndexInclude", new[] { "EducationBoardId", "EducationMediumId", "EducationMediumStageId", "StandardId", "DivisionId" });

            migrationBuilder.CreateIndex(
                name: "IX_UserScopeAssignments_Role",
                table: "UserScopeAssignments",
                columns: new[] { "TenantId", "RoleName", "IsActive", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "UQ_UserScopeAssignment",
                table: "UserScopeAssignments",
                columns: new[] { "TenantId", "UserId", "RoleName", "AcademicYearId", "EducationBoardId", "EducationMediumId", "EducationMediumStageId", "StandardId", "DivisionId" },
                unique: true,
                filter: "\"IsDeleted\" = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserScopeAssignments");

            migrationBuilder.DropColumn(
                name: "EducationMediumStageId",
                table: "AppStudentEnrollments");

            migrationBuilder.DropColumn(
                name: "EducationBoardId",
                table: "AppHomeworkAssignments");

            migrationBuilder.DropColumn(
                name: "EducationMediumId",
                table: "AppHomeworkAssignments");

            migrationBuilder.DropColumn(
                name: "EducationMediumStageId",
                table: "AppHomeworkAssignments");

            migrationBuilder.DropColumn(
                name: "EducationBoardId",
                table: "AppClassTeachers");

            migrationBuilder.DropColumn(
                name: "EducationMediumId",
                table: "AppClassTeachers");

            migrationBuilder.DropColumn(
                name: "EducationMediumStageId",
                table: "AppClassTeachers");
        }
    }
}
