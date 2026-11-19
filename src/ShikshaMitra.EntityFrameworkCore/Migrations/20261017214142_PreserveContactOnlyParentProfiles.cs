using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class PreserveContactOnlyParentProfiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // FullName is the presence marker for these optional owned profiles.
            // Preserve existing email/phone-only contacts without inventing a person's name.
            migrationBuilder.Sql("""
                UPDATE "StudentFamilyDetails"
                SET "FatherDetails_FullName" = ''
                WHERE "FatherDetails_FullName" IS NULL
                  AND (NULLIF(BTRIM("FatherDetails_Email"), '') IS NOT NULL
                    OR NULLIF(BTRIM("FatherDetails_ContactNo"), '') IS NOT NULL);

                UPDATE "StudentFamilyDetails"
                SET "MotherDetails_FullName" = ''
                WHERE "MotherDetails_FullName" IS NULL
                  AND (NULLIF(BTRIM("MotherDetails_Email"), '') IS NOT NULL
                    OR NULLIF(BTRIM("MotherDetails_ContactNo"), '') IS NOT NULL);

                UPDATE "StudentFamilyDetails"
                SET "GuardianDetails_FullName" = ''
                WHERE "GuardianDetails_FullName" IS NULL
                  AND (NULLIF(BTRIM("GuardianDetails_Email"), '') IS NOT NULL
                    OR NULLIF(BTRIM("GuardianDetails_ContactNo"), '') IS NOT NULL);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Retain recovered contacts: an empty name cannot be distinguished from a
            // subsequently saved contact-only profile, so rollback must not hide them again.
        }
    }
}
