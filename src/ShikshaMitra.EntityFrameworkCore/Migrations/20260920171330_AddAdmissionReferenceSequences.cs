using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class AddAdmissionReferenceSequences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppAdmissionReferenceSequences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StoredTenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    NextValue = table.Column<long>(type: "bigint", nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(
                        type: "character varying(40)",
                        maxLength: 40,
                        nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppAdmissionReferenceSequences", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "UX_AdmissionReferenceSequences_Tenant_Year_Kind",
                table: "AppAdmissionReferenceSequences",
                columns: new[] { "StoredTenantId", "Year", "Kind" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "AppAdmissionReferenceSequences");
        }
    }
}
