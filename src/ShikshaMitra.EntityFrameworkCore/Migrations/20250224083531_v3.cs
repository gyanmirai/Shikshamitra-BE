using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
	/// <inheritdoc />
	public partial class v3 : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			// Step 1: Add a new column with the correct data type (UUID)
			migrationBuilder.AddColumn<Guid>(
				name: "StudentId_New",
				table: "StudentCertificates",
				type: "uuid",
				nullable: false,
				defaultValueSql: "gen_random_uuid()"); // Default new entries to a new UUID

			// Step 2: Convert existing data (if possible) from int to UUID
			migrationBuilder.Sql(@"
                UPDATE ""StudentCertificates""
                SET ""StudentId_New"" = ('00000000-0000-0000-0000-' || LPAD(""StudentId""::text, 12, '0'))::uuid;
            ");

			// Step 3: Drop the old column
			migrationBuilder.DropColumn(
				name: "StudentId",
				table: "StudentCertificates");

			// Step 4: Rename the new column to match the old column name
			migrationBuilder.RenameColumn(
				name: "StudentId_New",
				table: "StudentCertificates",
				newName: "StudentId");
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			// Step 1: Add the old column back as an integer
			migrationBuilder.AddColumn<int>(
				name: "StudentId_Old",
				table: "StudentCertificates",
				type: "integer",
				nullable: false,
				defaultValue: 0);

			// Step 2: Convert UUID back to integer (Not always possible; needs custom logic)
			migrationBuilder.Sql(@"
                UPDATE ""StudentCertificates""
                SET ""StudentId_Old"" = substring(""StudentId""::text from 25)::int;
            ");

			// Step 3: Drop the UUID column
			migrationBuilder.DropColumn(
				name: "StudentId",
				table: "StudentCertificates");

			// Step 4: Rename the old column back
			migrationBuilder.RenameColumn(
				name: "StudentId_Old",
				table: "StudentCertificates",
				newName: "StudentId");
		}
	}
}
