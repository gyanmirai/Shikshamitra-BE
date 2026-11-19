using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations;

public partial class EnablePaidChargeRefunds : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<Guid>("DueFeeId", "Finance_FeeRefundLines", type: "uuid", nullable: true,
            oldClrType: typeof(Guid), oldType: "uuid");
        migrationBuilder.AlterColumn<Guid>("FeeHeadId", "Finance_FeeRefundLines", type: "uuid", nullable: true,
            oldClrType: typeof(Guid), oldType: "uuid");
        migrationBuilder.AlterColumn<Guid>("DueFeeId", "Finance_FeeRefundReservations", type: "uuid", nullable: true,
            oldClrType: typeof(Guid), oldType: "uuid");
        migrationBuilder.AddColumn<Guid>("ChargeId", "Finance_FeeRefundLines", type: "uuid", nullable: true);
        migrationBuilder.AddColumn<Guid>("ChargeId", "Finance_FeeRefundReservations", type: "uuid", nullable: true);
        migrationBuilder.CreateIndex("IX_Finance_FeeRefundLines_ChargeId", "Finance_FeeRefundLines", "ChargeId");
        migrationBuilder.CreateIndex("IX_Finance_FeeRefundReservations_ChargeId_Status", "Finance_FeeRefundReservations", new[] { "ChargeId", "Status" });
        migrationBuilder.AddCheckConstraint("CK_FeeRefundLines_Source", "Finance_FeeRefundLines",
            "(\"DueFeeId\" IS NOT NULL AND \"ChargeId\" IS NULL) OR (\"DueFeeId\" IS NULL AND \"ChargeId\" IS NOT NULL)");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // A downgrade cannot discard immutable charge-refund evidence.
        migrationBuilder.Sql("""
            DO $$ BEGIN
              IF EXISTS (SELECT 1 FROM "Finance_FeeRefundLines" WHERE "ChargeId" IS NOT NULL) THEN
                RAISE EXCEPTION 'Cannot remove paid charge refund support after it has been used.';
              END IF;
            END $$;
            """);
        migrationBuilder.DropCheckConstraint("CK_FeeRefundLines_Source", "Finance_FeeRefundLines");
        migrationBuilder.DropIndex("IX_Finance_FeeRefundLines_ChargeId", "Finance_FeeRefundLines");
        migrationBuilder.DropIndex("IX_Finance_FeeRefundReservations_ChargeId_Status", "Finance_FeeRefundReservations");
        migrationBuilder.DropColumn("ChargeId", "Finance_FeeRefundLines");
        migrationBuilder.DropColumn("ChargeId", "Finance_FeeRefundReservations");
        migrationBuilder.AlterColumn<Guid>("DueFeeId", "Finance_FeeRefundLines", type: "uuid", nullable: false,
            oldClrType: typeof(Guid), oldType: "uuid", oldNullable: true);
        migrationBuilder.AlterColumn<Guid>("FeeHeadId", "Finance_FeeRefundLines", type: "uuid", nullable: false,
            oldClrType: typeof(Guid), oldType: "uuid", oldNullable: true);
        migrationBuilder.AlterColumn<Guid>("DueFeeId", "Finance_FeeRefundReservations", type: "uuid", nullable: false,
            oldClrType: typeof(Guid), oldType: "uuid", oldNullable: true);
    }
}
