using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class AddFeeCategoryAndFeeHeadEnhancements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Finance_FeeHeads",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "Finance_FeeHeads",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<string>(
                name: "DeactivationReason",
                table: "Finance_FeeHeads",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsConcessionApplied",
                table: "Finance_FeeHeads",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Module",
                table: "Finance_FeeHeads",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Finance");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Finance_FeeCategories",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "Finance_FeeCategories",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<string>(
                name: "DeactivationReason",
                table: "Finance_FeeCategories",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Finance_FeeCategories",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateIndex(
                name: "IX_Finance_FeeHeads_TenantId_AcademicYearId_FeeCategoryId_Name",
                table: "Finance_FeeHeads",
                columns: new[] { "TenantId", "AcademicYearId", "FeeCategoryId", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_Finance_FeeCategories_TenantId_AcademicYearId_Name",
                table: "Finance_FeeCategories",
                columns: new[] { "TenantId", "AcademicYearId", "Name" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Finance_FeeHeads_TenantId_AcademicYearId_FeeCategoryId_Name",
                table: "Finance_FeeHeads");

            migrationBuilder.DropIndex(
                name: "IX_Finance_FeeCategories_TenantId_AcademicYearId_Name",
                table: "Finance_FeeCategories");

            migrationBuilder.DropColumn(
                name: "DeactivationReason",
                table: "Finance_FeeHeads");

            migrationBuilder.DropColumn(
                name: "IsConcessionApplied",
                table: "Finance_FeeHeads");

            migrationBuilder.DropColumn(
                name: "Module",
                table: "Finance_FeeHeads");

            migrationBuilder.DropColumn(
                name: "DeactivationReason",
                table: "Finance_FeeCategories");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Finance_FeeCategories");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Finance_FeeHeads",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(40)",
                oldMaxLength: 40);

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "Finance_FeeHeads",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Finance_FeeCategories",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "Finance_FeeCategories",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500,
                oldNullable: true);
        }
    }
}
