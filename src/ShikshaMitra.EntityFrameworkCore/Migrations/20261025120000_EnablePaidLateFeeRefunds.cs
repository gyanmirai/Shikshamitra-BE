using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations;

public partial class EnablePaidLateFeeRefunds : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint("CK_FeeRefundLines_Source", "Finance_FeeRefundLines");
        migrationBuilder.AddColumn<Guid>("LateFeeDueFeeId", "Finance_FeeRefundLines", type: "uuid", nullable: true);
        migrationBuilder.CreateIndex("IX_Finance_FeeRefundLines_LateFeeDueFeeId", "Finance_FeeRefundLines", "LateFeeDueFeeId");
        migrationBuilder.AddCheckConstraint("CK_FeeRefundLines_Source", "Finance_FeeRefundLines",
            "(CASE WHEN \"DueFeeId\" IS NULL THEN 0 ELSE 1 END + CASE WHEN \"ChargeId\" IS NULL THEN 0 ELSE 1 END + CASE WHEN \"LateFeeDueFeeId\" IS NULL THEN 0 ELSE 1 END) = 1");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DO $$ BEGIN
              IF EXISTS (SELECT 1 FROM "Finance_FeeRefundLines" WHERE "LateFeeDueFeeId" IS NOT NULL) THEN
                RAISE EXCEPTION 'Cannot remove paid late fee refund support after it has been used.';
              END IF;
            END $$;
            """);
        migrationBuilder.DropCheckConstraint("CK_FeeRefundLines_Source", "Finance_FeeRefundLines");
        migrationBuilder.DropIndex("IX_Finance_FeeRefundLines_LateFeeDueFeeId", "Finance_FeeRefundLines");
        migrationBuilder.DropColumn("LateFeeDueFeeId", "Finance_FeeRefundLines");
        migrationBuilder.AddCheckConstraint("CK_FeeRefundLines_Source", "Finance_FeeRefundLines",
            "(\"DueFeeId\" IS NOT NULL AND \"ChargeId\" IS NULL) OR (\"DueFeeId\" IS NULL AND \"ChargeId\" IS NOT NULL)");
    }
}
