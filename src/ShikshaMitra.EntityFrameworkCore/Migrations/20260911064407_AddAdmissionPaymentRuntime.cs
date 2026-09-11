using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class AddAdmissionPaymentRuntime : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppAdmissionPaymentTransactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    ApplicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    StageInstanceId = table.Column<Guid>(type: "uuid", nullable: false),
                    AttemptNumber = table.Column<int>(type: "integer", nullable: false),
                    PurposeCode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Method = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CollectionMode = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    ExternalReference = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    Provider = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    ReceiptNumber = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    Notes = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    DecisionReason = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    RecordedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    RecordedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    VerifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    VerifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PolicySnapshotJson = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    LastOperationType = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    LastOperationIdempotencyKey = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppAdmissionPaymentTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppAdmissionPaymentTransactions_AppAdmissionApplicationStag~",
                        column: x => x.StageInstanceId,
                        principalTable: "AppAdmissionApplicationStageInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppAdmissionPaymentTransactions_AppAdmissionApplications_Ap~",
                        column: x => x.ApplicationId,
                        principalTable: "AppAdmissionApplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppAdmissionPaymentTransactions_ApplicationId",
                table: "AppAdmissionPaymentTransactions",
                column: "ApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_AppAdmissionPaymentTransactions_StageInstanceId",
                table: "AppAdmissionPaymentTransactions",
                column: "StageInstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_AppAdmissionPaymentTransactions_TenantId_ApplicationId_Stag~",
                table: "AppAdmissionPaymentTransactions",
                columns: new[] { "TenantId", "ApplicationId", "StageInstanceId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_AppAdmissionPaymentTransactions_TenantId_Provider_ExternalR~",
                table: "AppAdmissionPaymentTransactions",
                columns: new[] { "TenantId", "Provider", "ExternalReference" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppAdmissionPaymentTransactions_TenantId_ReceiptNumber",
                table: "AppAdmissionPaymentTransactions",
                columns: new[] { "TenantId", "ReceiptNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppAdmissionPaymentTransactions_TenantId_StageInstanceId_At~",
                table: "AppAdmissionPaymentTransactions",
                columns: new[] { "TenantId", "StageInstanceId", "AttemptNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppAdmissionPaymentTransactions_TenantId_StageInstanceId_Id~",
                table: "AppAdmissionPaymentTransactions",
                columns: new[] { "TenantId", "StageInstanceId", "IdempotencyKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppAdmissionPaymentTransactions");
        }
    }
}
