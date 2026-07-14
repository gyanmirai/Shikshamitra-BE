using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
	/// <inheritdoc />
	public partial class AddRoomSeatingLayoutMetadata : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.Sql("""
                ALTER TABLE IF EXISTS "AppRooms"
                    ADD COLUMN IF NOT EXISTS "AccessibleSeatNumbersCsv" character varying(2000),
                    ADD COLUMN IF NOT EXISTS "AisleAfterColumnsCsv" character varying(500),
                    ADD COLUMN IF NOT EXISTS "BoardPosition" character varying(20),
                    ADD COLUMN IF NOT EXISTS "DisabledSeatNumbersCsv" character varying(2000),
                    ADD COLUMN IF NOT EXISTS "EntrancePosition" character varying(20),
                    ADD COLUMN IF NOT EXISTS "ReservedSeatNumbersCsv" character varying(2000);
                """);
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.Sql("""
                ALTER TABLE IF EXISTS "AppRooms"
                    DROP COLUMN IF EXISTS "AccessibleSeatNumbersCsv",
                    DROP COLUMN IF EXISTS "AisleAfterColumnsCsv",
                    DROP COLUMN IF EXISTS "BoardPosition",
                    DROP COLUMN IF EXISTS "DisabledSeatNumbersCsv",
                    DROP COLUMN IF EXISTS "EntrancePosition",
                    DROP COLUMN IF EXISTS "ReservedSeatNumbersCsv";
                """);
		}
	}
}
