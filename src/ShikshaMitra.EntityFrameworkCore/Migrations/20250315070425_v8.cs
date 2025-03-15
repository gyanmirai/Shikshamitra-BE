using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
	/// <inheritdoc />
	public partial class v8 : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			// Drop old state/country columns first
			migrationBuilder.DropColumn(
				name: "State",
				table: "AppEmployees");

			migrationBuilder.DropColumn(
				name: "SpouseDetails_State",
				table: "AppEmployees");

			migrationBuilder.DropColumn(
				name: "MotherDetails_State",
				table: "AppEmployees");

			migrationBuilder.DropColumn(
				name: "FatherDetails_State",
				table: "AppEmployees");

			// Add new columns with proper naming convention
			migrationBuilder.AddColumn<string>(
				name: "StateName",
				table: "AppEmployees",
				type: "text",
				nullable: true);

			migrationBuilder.AddColumn<string>(
				name: "StateIsoCode",
				table: "AppEmployees",
				type: "text",
				nullable: true);

			migrationBuilder.AddColumn<string>(
				name: "CountryIsoCode",
				table: "AppEmployees",
				type: "text",
				nullable: true);

			migrationBuilder.AddColumn<string>(
				name: "CountryName",
				table: "AppEmployees",
				type: "text",
				nullable: true);

			migrationBuilder.AddColumn<string>(
				name: "FatherDetails_CountryIsoCode",
				table: "AppEmployees",
				type: "text",
				nullable: true);

			migrationBuilder.AddColumn<string>(
				name: "FatherDetails_CountryName",
				table: "AppEmployees",
				type: "text",
				nullable: true);

			migrationBuilder.AddColumn<string>(
				name: "FatherDetails_StateIsoCode",
				table: "AppEmployees",
				type: "text",
				nullable: true);

			migrationBuilder.AddColumn<string>(
				name: "FatherDetails_StateName",
				table: "AppEmployees",
				type: "text",
				nullable: true);

			migrationBuilder.AddColumn<string>(
				name: "MotherDetails_CountryIsoCode",
				table: "AppEmployees",
				type: "text",
				nullable: true);

			migrationBuilder.AddColumn<string>(
				name: "MotherDetails_CountryName",
				table: "AppEmployees",
				type: "text",
				nullable: true);

			migrationBuilder.AddColumn<string>(
				name: "MotherDetails_StateIsoCode",
				table: "AppEmployees",
				type: "text",
				nullable: true);

			migrationBuilder.AddColumn<string>(
				name: "MotherDetails_StateName",
				table: "AppEmployees",
				type: "text",
				nullable: true);

			migrationBuilder.AddColumn<string>(
				name: "SpouseDetails_CountryIsoCode",
				table: "AppEmployees",
				type: "text",
				nullable: true);

			migrationBuilder.AddColumn<string>(
				name: "SpouseDetails_CountryName",
				table: "AppEmployees",
				type: "text",
				nullable: true);

			migrationBuilder.AddColumn<string>(
				name: "SpouseDetails_StateIsoCode",
				table: "AppEmployees",
				type: "text",
				nullable: true);

			migrationBuilder.AddColumn<string>(
				name: "SpouseDetails_StateName",
				table: "AppEmployees",
				type: "text",
				nullable: true);
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			// Drop all new columns first
			migrationBuilder.DropColumn(
				name: "StateName",
				table: "AppEmployees");

			migrationBuilder.DropColumn(
				name: "StateIsoCode",
				table: "AppEmployees");

			migrationBuilder.DropColumn(
				name: "CountryIsoCode",
				table: "AppEmployees");

			migrationBuilder.DropColumn(
				name: "CountryName",
				table: "AppEmployees");

			migrationBuilder.DropColumn(
				name: "FatherDetails_CountryIsoCode",
				table: "AppEmployees");

			migrationBuilder.DropColumn(
				name: "FatherDetails_CountryName",
				table: "AppEmployees");

			migrationBuilder.DropColumn(
				name: "FatherDetails_StateIsoCode",
				table: "AppEmployees");

			migrationBuilder.DropColumn(
				name: "FatherDetails_StateName",
				table: "AppEmployees");

			migrationBuilder.DropColumn(
				name: "MotherDetails_CountryIsoCode",
				table: "AppEmployees");

			migrationBuilder.DropColumn(
				name: "MotherDetails_CountryName",
				table: "AppEmployees");

			migrationBuilder.DropColumn(
				name: "MotherDetails_StateIsoCode",
				table: "AppEmployees");

			migrationBuilder.DropColumn(
				name: "MotherDetails_StateName",
				table: "AppEmployees");

			migrationBuilder.DropColumn(
				name: "SpouseDetails_CountryIsoCode",
				table: "AppEmployees");

			migrationBuilder.DropColumn(
				name: "SpouseDetails_CountryName",
				table: "AppEmployees");

			migrationBuilder.DropColumn(
				name: "SpouseDetails_StateIsoCode",
				table: "AppEmployees");

			migrationBuilder.DropColumn(
				name: "SpouseDetails_StateName",
				table: "AppEmployees");

			// Add back original columns
			migrationBuilder.AddColumn<string>(
				name: "State",
				table: "AppEmployees",
				type: "text",
				nullable: true);

			migrationBuilder.AddColumn<string>(
				name: "SpouseDetails_State",
				table: "AppEmployees",
				type: "text",
				nullable: true);

			migrationBuilder.AddColumn<string>(
				name: "MotherDetails_State",
				table: "AppEmployees",
				type: "text",
				nullable: true);

			migrationBuilder.AddColumn<string>(
				name: "FatherDetails_State",
				table: "AppEmployees",
				type: "text",
				nullable: true);
		}
	}
}