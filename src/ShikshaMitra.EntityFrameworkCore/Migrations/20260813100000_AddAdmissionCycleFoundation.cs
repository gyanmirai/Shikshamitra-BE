using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ShikshaMitra.EntityFrameworkCore;

#nullable disable

namespace ShikshaMitra.Migrations;

[DbContext(typeof(ShikshaMitraDbContext))]
[Migration("20260813100000_AddAdmissionCycleFoundation")]
public partial class AddAdmissionCycleFoundation : Migration
{
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.CreateTable(
			name: "AppAdmissionCycles",
			columns: table => new
			{
				Id = table.Column<Guid>(type: "uuid", nullable: false),
				TenantId = table.Column<Guid>(type: "uuid", nullable: true),
				Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
				Code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
				AcademicYearId = table.Column<Guid>(type: "uuid", nullable: false),
				StartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
				EndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
				Status = table.Column<int>(type: "integer", nullable: false),
				PublishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
				PublishedBy = table.Column<Guid>(type: "uuid", nullable: true),
				ExtraProperties = table.Column<string>(type: "text", nullable: false),
				ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
				CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
				CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
				LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
				LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
				IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
				DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
				DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
			},
			constraints: table => table.PrimaryKey("PK_AppAdmissionCycles", x => x.Id));

		migrationBuilder.CreateIndex(
			name: "IX_AppAdmissionCycles_TenantId_Code",
			table: "AppAdmissionCycles",
			columns: new[] { "TenantId", "Code" },
			unique: true);

		migrationBuilder.CreateIndex(
			name: "IX_AppAdmissionCycles_TenantId_AcademicYearId_Status",
			table: "AppAdmissionCycles",
			columns: new[] { "TenantId", "AcademicYearId", "Status" });
	}

	protected override void Down(MigrationBuilder migrationBuilder) =>
		migrationBuilder.DropTable(name: "AppAdmissionCycles");
}
