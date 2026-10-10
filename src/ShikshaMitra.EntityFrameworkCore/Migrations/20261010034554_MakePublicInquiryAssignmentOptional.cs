using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class MakePublicInquiryAssignmentOptional : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "CounsellorAssignmentProfileId",
                table: "AppAdmissionPublicInquiryLinks",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM "AppAdmissionPublicInquiryLinks" WHERE "CounsellorAssignmentProfileId" IS NULL) THEN
                        RAISE EXCEPTION 'Cannot roll back optional inquiry routing while manual-assignment links exist. Configure or remove those links first.';
                    END IF;
                END $$;
                """);
            migrationBuilder.AlterColumn<Guid>(
                name: "CounsellorAssignmentProfileId",
                table: "AppAdmissionPublicInquiryLinks",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
