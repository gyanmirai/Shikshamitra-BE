using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class AddConcessionPolicyRequestApplicability : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ApplicableConcessionTypeIdsJson",
                table: "Finance_ConcessionPolicies",
                type: "text",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "ApplicableFeeHeadIdsJson",
                table: "Finance_ConcessionPolicies",
                type: "text",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<int>(
                name: "FeeHeadScope",
                table: "Finance_ConcessionPolicies",
                type: "integer",
                nullable: false,
                defaultValue: 0);

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ApplicableConcessionTypeIdsJson",
                table: "Finance_ConcessionPolicies");

            migrationBuilder.DropColumn(
                name: "ApplicableFeeHeadIdsJson",
                table: "Finance_ConcessionPolicies");

            migrationBuilder.DropColumn(
                name: "FeeHeadScope",
                table: "Finance_ConcessionPolicies");
        }
    }
}
