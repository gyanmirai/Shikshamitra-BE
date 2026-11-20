using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class AllowUnknownLibraryCataloguePrice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "Price",
                table: "AppBooks",
                type: "numeric(18,2)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
			migrationBuilder.Sql("""
				LOCK TABLE "AppBooks" IN ACCESS EXCLUSIVE MODE;
				DO $catalogue_price_rollback$
				BEGIN
					IF EXISTS (SELECT 1 FROM "AppBooks" WHERE "Price" IS NULL) THEN
						RAISE EXCEPTION 'Catalogue price rollback refused: an Unknown price cannot be replaced with zero.'
							USING ERRCODE = 'P0001';
					END IF;
				END;
				$catalogue_price_rollback$;
				""");

            migrationBuilder.AlterColumn<decimal>(
                name: "Price",
                table: "AppBooks",
                type: "numeric(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldNullable: true);
        }
    }
}
