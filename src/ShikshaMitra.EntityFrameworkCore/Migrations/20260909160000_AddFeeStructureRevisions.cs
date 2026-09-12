using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ShikshaMitra.EntityFrameworkCore;

namespace ShikshaMitra.Migrations;

[DbContext(typeof(ShikshaMitraDbContext))]
[Migration("20260909160000_AddFeeStructureRevisions")]
public class AddFeeStructureRevisions : Migration
{
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.AddColumn<Guid>("RevisionRootId", "Finance_FeeStructures", type: "uuid", nullable: false, defaultValue: Guid.Empty);
		migrationBuilder.AddColumn<int>("RevisionNumber", "Finance_FeeStructures", type: "integer", nullable: false, defaultValue: 1);
		migrationBuilder.AddColumn<Guid>("PreviousRevisionId", "Finance_FeeStructures", type: "uuid", nullable: true);
		migrationBuilder.AddColumn<Guid>("ReceiptBookId", "Finance_FeeStructures", type: "uuid", nullable: true);
		migrationBuilder.AddColumn<string>("NormalizedName", "Finance_FeeStructures", type: "character varying(256)", maxLength: 256, nullable: false, defaultValue: "");
		migrationBuilder.Sql("""UPDATE "Finance_FeeStructures" SET "RevisionRootId" = "Id";""");
		migrationBuilder.Sql("""UPDATE "Finance_FeeStructures" SET "NormalizedName" = upper(trim("Name"));""");
		// Existing duplicate live root names must be reviewed before rollout; never rename financial history automatically.
		migrationBuilder.CreateIndex("IX_Finance_FeeStructures_StandardId_NormalizedName", "Finance_FeeStructures", new[] { "StandardId", "NormalizedName" }, unique: true, filter: "\"PreviousRevisionId\" IS NULL AND NOT \"IsDeleted\"");
		migrationBuilder.CreateIndex("IX_Finance_FeeStructures_RevisionRootId_RevisionNumber", "Finance_FeeStructures", new[] { "RevisionRootId", "RevisionNumber" }, unique: true);
		migrationBuilder.CreateIndex("IX_Finance_FeeStructures_PreviousRevisionId", "Finance_FeeStructures", "PreviousRevisionId");
		migrationBuilder.CreateIndex("IX_Finance_FeeStructures_ReceiptBookId", "Finance_FeeStructures", "ReceiptBookId");
		migrationBuilder.AddForeignKey("FK_Finance_FeeStructures_PreviousRevisionId", "Finance_FeeStructures", "PreviousRevisionId", "Finance_FeeStructures", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
		migrationBuilder.AddForeignKey("FK_Finance_FeeStructures_ReceiptBookId", "Finance_FeeStructures", "ReceiptBookId", "Finance_ReceiptBooks", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
	}

	protected override void Down(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.DropForeignKey("FK_Finance_FeeStructures_PreviousRevisionId", "Finance_FeeStructures");
		migrationBuilder.DropIndex("IX_Finance_FeeStructures_StandardId_NormalizedName", "Finance_FeeStructures");
		migrationBuilder.DropColumn("NormalizedName", "Finance_FeeStructures");
		migrationBuilder.DropForeignKey("FK_Finance_FeeStructures_ReceiptBookId", "Finance_FeeStructures");
		migrationBuilder.DropIndex("IX_Finance_FeeStructures_RevisionRootId_RevisionNumber", "Finance_FeeStructures");
		migrationBuilder.DropIndex("IX_Finance_FeeStructures_PreviousRevisionId", "Finance_FeeStructures");
		migrationBuilder.DropIndex("IX_Finance_FeeStructures_ReceiptBookId", "Finance_FeeStructures");
		migrationBuilder.DropColumn("RevisionRootId", "Finance_FeeStructures");
		migrationBuilder.DropColumn("RevisionNumber", "Finance_FeeStructures");
		migrationBuilder.DropColumn("PreviousRevisionId", "Finance_FeeStructures");
		migrationBuilder.DropColumn("ReceiptBookId", "Finance_FeeStructures");
	}
}
