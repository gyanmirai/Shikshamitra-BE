using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
	/// <inheritdoc />
	public partial class AddExamSeatingPlans : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.CreateTable(
				name: "AppExamSeatingPlans",
				columns: table => new
				{
					Id = table.Column<Guid>(type: "uuid", nullable: false),
					TenantId = table.Column<Guid>(type: "uuid", nullable: true),
					ExamId = table.Column<Guid>(type: "uuid", nullable: false),
					Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false, defaultValue: "All Subjects"),
					ScopeType = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
					Status = table.Column<int>(type: "integer", nullable: false),
					RoomIdsCsv = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
					AllocationRulesJson = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
					ExcludedStudentIdsCsv = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
					SpecialRequirementStudentIdsCsv = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
					IsLocked = table.Column<bool>(type: "boolean", nullable: false),
					FinalizedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
					FinalizedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
					PublishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
					PublishedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
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
					table.PrimaryKey("PK_AppExamSeatingPlans", x => x.Id);
				});

			migrationBuilder.CreateIndex(
				name: "IX_AppExamSeatingPlans_ExamId",
				table: "AppExamSeatingPlans",
				column: "ExamId",
				unique: true);
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropTable(
				name: "AppExamSeatingPlans");
		}
	}
}
