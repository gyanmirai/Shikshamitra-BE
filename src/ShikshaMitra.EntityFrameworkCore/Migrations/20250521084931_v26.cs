using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
	/// <inheritdoc />
	public partial class v26 : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropColumn(
				name: "Id",
				table: "AppSubjectTeacher");

			migrationBuilder.DropColumn(
				name: "BoardId",
				table: "AppSubjects");

			migrationBuilder.DropColumn(
				name: "MediumId",
				table: "AppSubjects");

			migrationBuilder.RenameColumn(
				name: "YearId",
				table: "AppSubjects",
				newName: "AcademicMediumId");

			migrationBuilder.AddColumn<Guid>(
				name: "TenantId",
				table: "AppSubjectTeacher",
				type: "uuid",
				nullable: true);

			migrationBuilder.CreateTable(
				name: "AppBookCategories",
				columns: table => new
				{
					Id = table.Column<Guid>(type: "uuid", nullable: false),
					Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
					Description = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
					ExtraProperties = table.Column<string>(type: "text", nullable: false),
					ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
					CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
					CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
					LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
					LastModifierId = table.Column<Guid>(type: "uuid", nullable: true)
				},
				constraints: table =>
				{
					table.PrimaryKey("PK_AppBookCategories", x => x.Id);
				});

			migrationBuilder.CreateTable(
				name: "AppLibraryTimings",
				columns: table => new
				{
					Id = table.Column<Guid>(type: "uuid", nullable: false),
					Day = table.Column<int>(type: "integer", nullable: false),
					OpeningTime = table.Column<TimeSpan>(type: "interval", nullable: false),
					ClosingTime = table.Column<TimeSpan>(type: "interval", nullable: false),
					ExtraProperties = table.Column<string>(type: "text", nullable: false),
					ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
					CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
					CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
					LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
					LastModifierId = table.Column<Guid>(type: "uuid", nullable: true)
				},
				constraints: table =>
				{
					table.PrimaryKey("PK_AppLibraryTimings", x => x.Id);
				});

			migrationBuilder.CreateIndex(
				name: "IX_AppLibraryTimings_Day",
				table: "AppLibraryTimings",
				column: "Day",
				unique: true);
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropTable(
				name: "AppBookCategories");

			migrationBuilder.DropTable(
				name: "AppLibraryTimings");

			migrationBuilder.DropColumn(
				name: "TenantId",
				table: "AppSubjectTeacher");

			migrationBuilder.RenameColumn(
				name: "AcademicMediumId",
				table: "AppSubjects",
				newName: "YearId");

			migrationBuilder.AddColumn<Guid>(
				name: "Id",
				table: "AppSubjectTeacher",
				type: "uuid",
				nullable: false,
				defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

			migrationBuilder.AddColumn<Guid>(
				name: "BoardId",
				table: "AppSubjects",
				type: "uuid",
				nullable: false,
				defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

			migrationBuilder.AddColumn<Guid>(
				name: "MediumId",
				table: "AppSubjects",
				type: "uuid",
				nullable: false,
				defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));
		}
	}
}