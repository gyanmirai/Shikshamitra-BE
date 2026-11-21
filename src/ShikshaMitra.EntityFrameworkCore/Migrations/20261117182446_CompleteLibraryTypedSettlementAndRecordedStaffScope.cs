using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class CompleteLibraryTypedSettlementAndRecordedStaffScope : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            PreflightLegacySettlementMoney(migrationBuilder);
            migrationBuilder.DropCheckConstraint(
                name: "CK_PaymentMethodRecords_Payer",
                table: "Finance_PaymentMethodRecords");

            migrationBuilder.DropCheckConstraint(
                name: "CK_FeeRefundRequests_Payer",
                table: "Finance_FeeRefundRequests");

            migrationBuilder.DropCheckConstraint(
                name: "CK_FeeRecordMetadatas_Payer",
                table: "Finance_FeeRecordMetadatas");

            migrationBuilder.DropCheckConstraint(
                name: "CK_FeePaymentReceipts_Payer",
                table: "Finance_FeePaymentReceipts");

            migrationBuilder.AddColumn<Guid>(
                name: "EmployeeId",
                table: "Finance_Transactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "EmployeeId",
                table: "Finance_PaymentMethodRecords",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "EmployeeId",
                table: "Finance_FeeRefundRequests",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "FinanceExternalSourceLinkId",
                table: "Finance_FeeRefundRequests",
                type: "uuid",
                nullable: true);

            ConvertLegacySettlementMoney(migrationBuilder, "Finance_FeeRecordMetadatas", "TotalPaidFee");

            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "Finance_FeeRecordMetadatas",
                type: "character varying(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "EmployeeId",
                table: "Finance_FeeRecordMetadatas",
                type: "uuid",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "EducationMediumId",
                table: "Finance_FeePaymentReceipts",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<Guid>(
                name: "BoardId",
                table: "Finance_FeePaymentReceipts",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "Finance_FeePaymentReceipts",
                type: "character varying(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "EmployeeId",
                table: "Finance_FeePaymentReceipts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ResultEntityId",
                table: "Finance_ExternalReceivableCommands",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResultEntityType",
                table: "Finance_ExternalReceivableCommands",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "StudentId",
                table: "Finance_ChargeWaiverApplications",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<string>(
                name: "Reason",
                table: "Finance_ChargeWaiverApplications",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500);

            migrationBuilder.AddColumn<Guid>(
                name: "EmployeeId",
                table: "Finance_ChargeWaiverApplications",
                type: "uuid",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "StudentId",
                table: "Finance_ChargeCollectionRecords",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            ConvertLegacySettlementMoney(migrationBuilder, "Finance_ChargeCollectionRecords", "Amount");

            migrationBuilder.AddColumn<Guid>(
                name: "EmployeeId",
                table: "Finance_ChargeCollectionRecords",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "EmployeeId",
                table: "Finance_CashCollections",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AfterBorrowerScopeCaptureId",
                table: "AppLibraryReservationEvents",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BeforeBorrowerScopeCaptureId",
                table: "AppLibraryReservationEvents",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AfterBorrowerScopeCaptureId",
                table: "AppLibraryLoanEvents",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BeforeBorrowerScopeCaptureId",
                table: "AppLibraryLoanEvents",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BorrowerScopeCaptureId",
                table: "AppBookReservations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BorrowerScopeCaptureVersion",
                table: "AppBookReservations",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BorrowerScopeCaptureId",
                table: "AppBookIssues",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BorrowerScopeCaptureVersion",
                table: "AppBookIssues",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BorrowerScopeProvenance",
                table: "AppBookIssues",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "AppLibraryBorrowerScopeCaptures",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceKind = table.Column<int>(type: "integer", nullable: false),
                    SourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    CanonicalEmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    AcademicYearId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    Provenance = table.Column<int>(type: "integer", nullable: false),
                    IsComplete = table.Column<bool>(type: "boolean", nullable: false),
                    ExpectedAssignmentCount = table.Column<int>(type: "integer", nullable: false),
                    IncompleteReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SourceName = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    CapturedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ActorKind = table.Column<int>(type: "integer", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: true),
                    SystemPurpose = table.Column<int>(type: "integer", nullable: true),
                    AuditJson = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppLibraryBorrowerScopeCaptures", x => x.Id);
                    table.UniqueConstraint("AK_AppLibraryBorrowerScopeCaptures_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.CheckConstraint("CK_LibraryBorrowerScopeCaptures_Actor", "(\"ActorKind\" = 1 AND \"ActorId\" IS NOT NULL AND \"SystemPurpose\" IS NULL) OR (\"ActorKind\" = 2 AND \"ActorId\" IS NULL AND \"SystemPurpose\" IS NOT NULL AND \"SystemPurpose\" IN (1, 2))");
                    table.CheckConstraint("CK_LibraryBorrowerScopeCaptures_AssignmentCount", "\"ExpectedAssignmentCount\" >= 0");
                    table.CheckConstraint("CK_LibraryBorrowerScopeCaptures_Source", "\"SourceKind\" IN (1, 2) AND \"Version\" > 0 AND \"Provenance\" IN (1, 2)");
                });

            migrationBuilder.CreateTable(
                name: "Finance_ChargeWaiverReversals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    WaiverApplicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    ReversedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Finance_ChargeWaiverReversals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Finance_ChargeWaiverReversals_Finance_ChargeWaiverApplicati~",
                        column: x => x.WaiverApplicationId,
                        principalTable: "Finance_ChargeWaiverApplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Finance_ExternalSettlementPolicies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceLinkId = table.Column<Guid>(type: "uuid", nullable: false),
                    CollectionPolicyVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    RefundPolicyVersionId = table.Column<Guid>(type: "uuid", nullable: true),
                    BoundByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Finance_ExternalSettlementPolicies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Finance_ExternalSettlementPolicies_Finance_ExternalReceivab~",
                        column: x => x.SourceLinkId,
                        principalTable: "Finance_ExternalReceivableSourceLinks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Finance_ExternalSettlementPolicies_Finance_PolicyVersions_C~",
                        column: x => x.CollectionPolicyVersionId,
                        principalTable: "Finance_PolicyVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Finance_ExternalSettlementPolicies_Finance_PolicyVersions_R~",
                        column: x => x.RefundPolicyVersionId,
                        principalTable: "Finance_PolicyVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Finance_ExternalWaiverRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceLinkId = table.Column<Guid>(type: "uuid", nullable: false),
                    PolicyVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    RequiresApproval = table.Column<bool>(type: "boolean", nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false),
                    RequestedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    DecidedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    DecidedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DecisionReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    AppliedWaiverId = table.Column<Guid>(type: "uuid", nullable: true),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Finance_ExternalWaiverRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Finance_ExternalWaiverRequests_Finance_ChargeWaiverApplicat~",
                        column: x => x.AppliedWaiverId,
                        principalTable: "Finance_ChargeWaiverApplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Finance_ExternalWaiverRequests_Finance_ExternalReceivableSo~",
                        column: x => x.SourceLinkId,
                        principalTable: "Finance_ExternalReceivableSourceLinks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Finance_ExternalWaiverRequests_Finance_PolicyVersions_Polic~",
                        column: x => x.PolicyVersionId,
                        principalTable: "Finance_PolicyVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AppLibraryBorrowerAssignmentSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CaptureId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationAssignmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    StageId = table.Column<Guid>(type: "uuid", nullable: false),
                    MediumId = table.Column<Guid>(type: "uuid", nullable: true),
                    BoardId = table.Column<Guid>(type: "uuid", nullable: true),
                    AcademicYearId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppLibraryBorrowerAssignmentSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppLibraryBorrowerAssignmentSnapshots_AppLibraryBorrowerSco~",
                        columns: x => new { x.TenantId, x.CaptureId },
                        principalTable: "AppLibraryBorrowerScopeCaptures",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Finance_Transactions_EmployeeId",
                table: "Finance_Transactions",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_Finance_Transactions_TenantId_EmployeeId_AcademicYearId",
                table: "Finance_Transactions",
                columns: new[] { "TenantId", "EmployeeId", "AcademicYearId" },
                filter: "\"EmployeeId\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Transactions_EmployeePayer",
                table: "Finance_Transactions",
                sql: "(\"EmployeeId\" IS NULL OR (\"StudentId\" IS NULL AND \"FinanceFeeAccountId\" IS NULL))");

            migrationBuilder.CreateIndex(
                name: "IX_Finance_PaymentMethodRecords_EmployeeId",
                table: "Finance_PaymentMethodRecords",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_Finance_PaymentMethodRecords_TenantId_EmployeeId",
                table: "Finance_PaymentMethodRecords",
                columns: new[] { "TenantId", "EmployeeId" },
                filter: "\"EmployeeId\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PaymentMethodRecords_Payer",
                table: "Finance_PaymentMethodRecords",
                sql: "((\"EmployeeId\" IS NULL AND (\"StudentId\" IS NOT NULL OR \"FinanceFeeAccountId\" IS NOT NULL)) OR (\"EmployeeId\" IS NOT NULL AND \"StudentId\" IS NULL AND \"FinanceFeeAccountId\" IS NULL))");

            migrationBuilder.CreateIndex(
                name: "IX_Finance_FeeRefundRequests_EmployeeId",
                table: "Finance_FeeRefundRequests",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_Finance_FeeRefundRequests_FinanceExternalSourceLinkId",
                table: "Finance_FeeRefundRequests",
                column: "FinanceExternalSourceLinkId");

            migrationBuilder.CreateIndex(
                name: "IX_Finance_FeeRefundRequests_TenantId_EmployeeId_AcademicYearId",
                table: "Finance_FeeRefundRequests",
                columns: new[] { "TenantId", "EmployeeId", "AcademicYearId" },
                filter: "\"EmployeeId\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_FeeRefundRequests_Payer",
                table: "Finance_FeeRefundRequests",
                sql: "((\"EmployeeId\" IS NULL AND (\"StudentId\" IS NOT NULL OR \"FinanceFeeAccountId\" IS NOT NULL)) OR (\"EmployeeId\" IS NOT NULL AND \"StudentId\" IS NULL AND \"FinanceFeeAccountId\" IS NULL))");

            migrationBuilder.CreateIndex(
                name: "IX_Finance_FeeRecordMetadatas_EmployeeId",
                table: "Finance_FeeRecordMetadatas",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_Finance_FeeRecordMetadatas_TenantId_EmployeeId_AcademicYear~",
                table: "Finance_FeeRecordMetadatas",
                columns: new[] { "TenantId", "EmployeeId", "AcademicYearId" },
                filter: "\"EmployeeId\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_FeeRecordMetadatas_Payer",
                table: "Finance_FeeRecordMetadatas",
                sql: "((\"EmployeeId\" IS NULL AND (\"StudentId\" IS NOT NULL OR \"FinanceFeeAccountId\" IS NOT NULL)) OR (\"EmployeeId\" IS NOT NULL AND \"StudentId\" IS NULL AND \"FinanceFeeAccountId\" IS NULL))");

            migrationBuilder.CreateIndex(
                name: "IX_Finance_FeePaymentReceipts_EmployeeId",
                table: "Finance_FeePaymentReceipts",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_Finance_FeePaymentReceipts_TenantId_EmployeeId_AcademicYear~",
                table: "Finance_FeePaymentReceipts",
                columns: new[] { "TenantId", "EmployeeId", "AcademicYearId" },
                filter: "\"EmployeeId\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_FeePaymentReceipts_Payer",
                table: "Finance_FeePaymentReceipts",
                sql: "((\"EmployeeId\" IS NULL AND (\"StudentId\" IS NOT NULL OR \"FinanceFeeAccountId\" IS NOT NULL)) OR (\"EmployeeId\" IS NOT NULL AND \"StudentId\" IS NULL AND \"FinanceFeeAccountId\" IS NULL))");

            migrationBuilder.CreateIndex(
                name: "IX_Finance_ChargeWaiverApplications_EmployeeId",
                table: "Finance_ChargeWaiverApplications",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_Finance_ChargeWaiverApplications_TenantId_EmployeeId",
                table: "Finance_ChargeWaiverApplications",
                columns: new[] { "TenantId", "EmployeeId" },
                filter: "\"EmployeeId\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ChargeWaiverApplications_Payer",
                table: "Finance_ChargeWaiverApplications",
                sql: "((\"StudentId\" IS NOT NULL AND \"EmployeeId\" IS NULL) OR (\"StudentId\" IS NULL AND \"EmployeeId\" IS NOT NULL))");

            migrationBuilder.CreateIndex(
                name: "IX_Finance_ChargeCollectionRecords_EmployeeId",
                table: "Finance_ChargeCollectionRecords",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_Finance_ChargeCollectionRecords_TenantId_EmployeeId",
                table: "Finance_ChargeCollectionRecords",
                columns: new[] { "TenantId", "EmployeeId" },
                filter: "\"EmployeeId\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ChargeCollectionRecords_Payer",
                table: "Finance_ChargeCollectionRecords",
                sql: "((\"StudentId\" IS NOT NULL AND \"EmployeeId\" IS NULL) OR (\"StudentId\" IS NULL AND \"EmployeeId\" IS NOT NULL))");

            migrationBuilder.CreateIndex(
                name: "IX_Finance_CashCollections_EmployeeId",
                table: "Finance_CashCollections",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_Finance_CashCollections_TenantId_EmployeeId_AcademicYearId",
                table: "Finance_CashCollections",
                columns: new[] { "TenantId", "EmployeeId", "AcademicYearId" },
                filter: "\"EmployeeId\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CashCollections_EmployeePayer",
                table: "Finance_CashCollections",
                sql: "(\"EmployeeId\" IS NULL OR (\"StudentId\" IS NULL AND \"FinanceFeeAccountId\" IS NULL))");

            migrationBuilder.CreateIndex(
                name: "IX_AppLibraryReservationEvents_TenantId_AfterBorrowerScopeCapt~",
                table: "AppLibraryReservationEvents",
                columns: new[] { "TenantId", "AfterBorrowerScopeCaptureId" });

            migrationBuilder.CreateIndex(
                name: "IX_AppLibraryReservationEvents_TenantId_BeforeBorrowerScopeCap~",
                table: "AppLibraryReservationEvents",
                columns: new[] { "TenantId", "BeforeBorrowerScopeCaptureId" });

            migrationBuilder.CreateIndex(
                name: "IX_AppLibraryLoanEvents_TenantId_AfterBorrowerScopeCaptureId",
                table: "AppLibraryLoanEvents",
                columns: new[] { "TenantId", "AfterBorrowerScopeCaptureId" });

            migrationBuilder.CreateIndex(
                name: "IX_AppLibraryLoanEvents_TenantId_BeforeBorrowerScopeCaptureId",
                table: "AppLibraryLoanEvents",
                columns: new[] { "TenantId", "BeforeBorrowerScopeCaptureId" });

            migrationBuilder.CreateIndex(
                name: "IX_AppBookReservations_TenantId_BorrowerScopeCaptureId",
                table: "AppBookReservations",
                columns: new[] { "TenantId", "BorrowerScopeCaptureId" });

            migrationBuilder.CreateIndex(
                name: "IX_AppBookIssues_TenantId_BorrowerScopeCaptureId",
                table: "AppBookIssues",
                columns: new[] { "TenantId", "BorrowerScopeCaptureId" });

            migrationBuilder.CreateIndex(
                name: "IX_AppLibraryBorrowerAssignmentSnapshots_TenantId_CaptureId_Ac~",
                table: "AppLibraryBorrowerAssignmentSnapshots",
                columns: new[] { "TenantId", "CaptureId", "AcademicYearId", "StageId" });

            migrationBuilder.CreateIndex(
                name: "IX_AppLibraryBorrowerAssignmentSnapshots_TenantId_CaptureId_Or~",
                table: "AppLibraryBorrowerAssignmentSnapshots",
                columns: new[] { "TenantId", "CaptureId", "OrganizationAssignmentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppLibraryBorrowerScopeCaptures_TenantId_SourceKind_SourceI~",
                table: "AppLibraryBorrowerScopeCaptures",
                columns: new[] { "TenantId", "SourceKind", "SourceId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Finance_ChargeWaiverReversals_TenantId_WaiverApplicationId",
                table: "Finance_ChargeWaiverReversals",
                columns: new[] { "TenantId", "WaiverApplicationId" });

            migrationBuilder.CreateIndex(
                name: "IX_Finance_ChargeWaiverReversals_WaiverApplicationId",
                table: "Finance_ChargeWaiverReversals",
                column: "WaiverApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_Finance_ExternalSettlementPolicies_CollectionPolicyVersionId",
                table: "Finance_ExternalSettlementPolicies",
                column: "CollectionPolicyVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_Finance_ExternalSettlementPolicies_RefundPolicyVersionId",
                table: "Finance_ExternalSettlementPolicies",
                column: "RefundPolicyVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_Finance_ExternalSettlementPolicies_SourceLinkId",
                table: "Finance_ExternalSettlementPolicies",
                column: "SourceLinkId");

            migrationBuilder.CreateIndex(
                name: "IX_Finance_ExternalSettlementPolicies_TenantId_SourceLinkId",
                table: "Finance_ExternalSettlementPolicies",
                columns: new[] { "TenantId", "SourceLinkId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Finance_ExternalWaiverRequests_AppliedWaiverId",
                table: "Finance_ExternalWaiverRequests",
                column: "AppliedWaiverId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Finance_ExternalWaiverRequests_PolicyVersionId",
                table: "Finance_ExternalWaiverRequests",
                column: "PolicyVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_Finance_ExternalWaiverRequests_SourceLinkId",
                table: "Finance_ExternalWaiverRequests",
                column: "SourceLinkId");

            migrationBuilder.CreateIndex(
                name: "IX_Finance_ExternalWaiverRequests_TenantId_SourceLinkId_State",
                table: "Finance_ExternalWaiverRequests",
                columns: new[] { "TenantId", "SourceLinkId", "State" });

            migrationBuilder.AddForeignKey(
                name: "FK_AppBookIssues_AppLibraryBorrowerScopeCaptures_TenantId_Borr~",
                table: "AppBookIssues",
                columns: new[] { "TenantId", "BorrowerScopeCaptureId" },
                principalTable: "AppLibraryBorrowerScopeCaptures",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AppBookReservations_AppLibraryBorrowerScopeCaptures_TenantI~",
                table: "AppBookReservations",
                columns: new[] { "TenantId", "BorrowerScopeCaptureId" },
                principalTable: "AppLibraryBorrowerScopeCaptures",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AppLibraryLoanEvents_AppLibraryBorrowerScopeCaptures_Tenant~",
                table: "AppLibraryLoanEvents",
                columns: new[] { "TenantId", "AfterBorrowerScopeCaptureId" },
                principalTable: "AppLibraryBorrowerScopeCaptures",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AppLibraryLoanEvents_AppLibraryBorrowerScopeCaptures_Tenan~1",
                table: "AppLibraryLoanEvents",
                columns: new[] { "TenantId", "BeforeBorrowerScopeCaptureId" },
                principalTable: "AppLibraryBorrowerScopeCaptures",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AppLibraryReservationEvents_AppLibraryBorrowerScopeCaptures~",
                table: "AppLibraryReservationEvents",
                columns: new[] { "TenantId", "AfterBorrowerScopeCaptureId" },
                principalTable: "AppLibraryBorrowerScopeCaptures",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AppLibraryReservationEvents_AppLibraryBorrowerScopeCapture~1",
                table: "AppLibraryReservationEvents",
                columns: new[] { "TenantId", "BeforeBorrowerScopeCaptureId" },
                principalTable: "AppLibraryBorrowerScopeCaptures",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Finance_CashCollections_AppEmployees_EmployeeId",
                table: "Finance_CashCollections",
                column: "EmployeeId",
                principalTable: "AppEmployees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Finance_ChargeCollectionRecords_AppEmployees_EmployeeId",
                table: "Finance_ChargeCollectionRecords",
                column: "EmployeeId",
                principalTable: "AppEmployees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Finance_ChargeWaiverApplications_AppEmployees_EmployeeId",
                table: "Finance_ChargeWaiverApplications",
                column: "EmployeeId",
                principalTable: "AppEmployees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Finance_FeePaymentReceipts_AppEmployees_EmployeeId",
                table: "Finance_FeePaymentReceipts",
                column: "EmployeeId",
                principalTable: "AppEmployees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Finance_FeeRecordMetadatas_AppEmployees_EmployeeId",
                table: "Finance_FeeRecordMetadatas",
                column: "EmployeeId",
                principalTable: "AppEmployees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Finance_FeeRefundRequests_AppEmployees_EmployeeId",
                table: "Finance_FeeRefundRequests",
                column: "EmployeeId",
                principalTable: "AppEmployees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Finance_FeeRefundRequests_Finance_ExternalReceivableSourceL~",
                table: "Finance_FeeRefundRequests",
                column: "FinanceExternalSourceLinkId",
                principalTable: "Finance_ExternalReceivableSourceLinks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Finance_PaymentMethodRecords_AppEmployees_EmployeeId",
                table: "Finance_PaymentMethodRecords",
                column: "EmployeeId",
                principalTable: "AppEmployees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Finance_Transactions_AppEmployees_EmployeeId",
                table: "Finance_Transactions",
                column: "EmployeeId",
                principalTable: "AppEmployees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
            InstallTypedSettlementEvidenceGuards(migrationBuilder);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            RefuseLossyTypedSettlementRollback(migrationBuilder);
            RemoveTypedSettlementEvidenceGuards(migrationBuilder);
            migrationBuilder.DropForeignKey(
                name: "FK_AppBookIssues_AppLibraryBorrowerScopeCaptures_TenantId_Borr~",
                table: "AppBookIssues");

            migrationBuilder.DropForeignKey(
                name: "FK_AppBookReservations_AppLibraryBorrowerScopeCaptures_TenantI~",
                table: "AppBookReservations");

            migrationBuilder.DropForeignKey(
                name: "FK_AppLibraryLoanEvents_AppLibraryBorrowerScopeCaptures_Tenant~",
                table: "AppLibraryLoanEvents");

            migrationBuilder.DropForeignKey(
                name: "FK_AppLibraryLoanEvents_AppLibraryBorrowerScopeCaptures_Tenan~1",
                table: "AppLibraryLoanEvents");

            migrationBuilder.DropForeignKey(
                name: "FK_AppLibraryReservationEvents_AppLibraryBorrowerScopeCaptures~",
                table: "AppLibraryReservationEvents");

            migrationBuilder.DropForeignKey(
                name: "FK_AppLibraryReservationEvents_AppLibraryBorrowerScopeCapture~1",
                table: "AppLibraryReservationEvents");

            migrationBuilder.DropForeignKey(
                name: "FK_Finance_CashCollections_AppEmployees_EmployeeId",
                table: "Finance_CashCollections");

            migrationBuilder.DropForeignKey(
                name: "FK_Finance_ChargeCollectionRecords_AppEmployees_EmployeeId",
                table: "Finance_ChargeCollectionRecords");

            migrationBuilder.DropForeignKey(
                name: "FK_Finance_ChargeWaiverApplications_AppEmployees_EmployeeId",
                table: "Finance_ChargeWaiverApplications");

            migrationBuilder.DropForeignKey(
                name: "FK_Finance_FeePaymentReceipts_AppEmployees_EmployeeId",
                table: "Finance_FeePaymentReceipts");

            migrationBuilder.DropForeignKey(
                name: "FK_Finance_FeeRecordMetadatas_AppEmployees_EmployeeId",
                table: "Finance_FeeRecordMetadatas");

            migrationBuilder.DropForeignKey(
                name: "FK_Finance_FeeRefundRequests_AppEmployees_EmployeeId",
                table: "Finance_FeeRefundRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_Finance_FeeRefundRequests_Finance_ExternalReceivableSourceL~",
                table: "Finance_FeeRefundRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_Finance_PaymentMethodRecords_AppEmployees_EmployeeId",
                table: "Finance_PaymentMethodRecords");

            migrationBuilder.DropForeignKey(
                name: "FK_Finance_Transactions_AppEmployees_EmployeeId",
                table: "Finance_Transactions");

            migrationBuilder.DropTable(
                name: "AppLibraryBorrowerAssignmentSnapshots");

            migrationBuilder.DropTable(
                name: "Finance_ChargeWaiverReversals");

            migrationBuilder.DropTable(
                name: "Finance_ExternalSettlementPolicies");

            migrationBuilder.DropTable(
                name: "Finance_ExternalWaiverRequests");

            migrationBuilder.DropTable(
                name: "AppLibraryBorrowerScopeCaptures");

            migrationBuilder.DropIndex(
                name: "IX_Finance_Transactions_EmployeeId",
                table: "Finance_Transactions");

            migrationBuilder.DropIndex(
                name: "IX_Finance_Transactions_TenantId_EmployeeId_AcademicYearId",
                table: "Finance_Transactions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Transactions_EmployeePayer",
                table: "Finance_Transactions");

            migrationBuilder.DropIndex(
                name: "IX_Finance_PaymentMethodRecords_EmployeeId",
                table: "Finance_PaymentMethodRecords");

            migrationBuilder.DropIndex(
                name: "IX_Finance_PaymentMethodRecords_TenantId_EmployeeId",
                table: "Finance_PaymentMethodRecords");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PaymentMethodRecords_Payer",
                table: "Finance_PaymentMethodRecords");

            migrationBuilder.DropIndex(
                name: "IX_Finance_FeeRefundRequests_EmployeeId",
                table: "Finance_FeeRefundRequests");

            migrationBuilder.DropIndex(
                name: "IX_Finance_FeeRefundRequests_FinanceExternalSourceLinkId",
                table: "Finance_FeeRefundRequests");

            migrationBuilder.DropIndex(
                name: "IX_Finance_FeeRefundRequests_TenantId_EmployeeId_AcademicYearId",
                table: "Finance_FeeRefundRequests");

            migrationBuilder.DropCheckConstraint(
                name: "CK_FeeRefundRequests_Payer",
                table: "Finance_FeeRefundRequests");

            migrationBuilder.DropIndex(
                name: "IX_Finance_FeeRecordMetadatas_EmployeeId",
                table: "Finance_FeeRecordMetadatas");

            migrationBuilder.DropIndex(
                name: "IX_Finance_FeeRecordMetadatas_TenantId_EmployeeId_AcademicYear~",
                table: "Finance_FeeRecordMetadatas");

            migrationBuilder.DropCheckConstraint(
                name: "CK_FeeRecordMetadatas_Payer",
                table: "Finance_FeeRecordMetadatas");

            migrationBuilder.DropIndex(
                name: "IX_Finance_FeePaymentReceipts_EmployeeId",
                table: "Finance_FeePaymentReceipts");

            migrationBuilder.DropIndex(
                name: "IX_Finance_FeePaymentReceipts_TenantId_EmployeeId_AcademicYear~",
                table: "Finance_FeePaymentReceipts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_FeePaymentReceipts_Payer",
                table: "Finance_FeePaymentReceipts");

            migrationBuilder.DropIndex(
                name: "IX_Finance_ChargeWaiverApplications_EmployeeId",
                table: "Finance_ChargeWaiverApplications");

            migrationBuilder.DropIndex(
                name: "IX_Finance_ChargeWaiverApplications_TenantId_EmployeeId",
                table: "Finance_ChargeWaiverApplications");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ChargeWaiverApplications_Payer",
                table: "Finance_ChargeWaiverApplications");

            migrationBuilder.DropIndex(
                name: "IX_Finance_ChargeCollectionRecords_EmployeeId",
                table: "Finance_ChargeCollectionRecords");

            migrationBuilder.DropIndex(
                name: "IX_Finance_ChargeCollectionRecords_TenantId_EmployeeId",
                table: "Finance_ChargeCollectionRecords");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ChargeCollectionRecords_Payer",
                table: "Finance_ChargeCollectionRecords");

            migrationBuilder.DropIndex(
                name: "IX_Finance_CashCollections_EmployeeId",
                table: "Finance_CashCollections");

            migrationBuilder.DropIndex(
                name: "IX_Finance_CashCollections_TenantId_EmployeeId_AcademicYearId",
                table: "Finance_CashCollections");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CashCollections_EmployeePayer",
                table: "Finance_CashCollections");

            migrationBuilder.DropIndex(
                name: "IX_AppLibraryReservationEvents_TenantId_AfterBorrowerScopeCapt~",
                table: "AppLibraryReservationEvents");

            migrationBuilder.DropIndex(
                name: "IX_AppLibraryReservationEvents_TenantId_BeforeBorrowerScopeCap~",
                table: "AppLibraryReservationEvents");

            migrationBuilder.DropIndex(
                name: "IX_AppLibraryLoanEvents_TenantId_AfterBorrowerScopeCaptureId",
                table: "AppLibraryLoanEvents");

            migrationBuilder.DropIndex(
                name: "IX_AppLibraryLoanEvents_TenantId_BeforeBorrowerScopeCaptureId",
                table: "AppLibraryLoanEvents");

            migrationBuilder.DropIndex(
                name: "IX_AppBookReservations_TenantId_BorrowerScopeCaptureId",
                table: "AppBookReservations");

            migrationBuilder.DropIndex(
                name: "IX_AppBookIssues_TenantId_BorrowerScopeCaptureId",
                table: "AppBookIssues");

            migrationBuilder.DropColumn(
                name: "EmployeeId",
                table: "Finance_Transactions");

            migrationBuilder.DropColumn(
                name: "EmployeeId",
                table: "Finance_PaymentMethodRecords");

            migrationBuilder.DropColumn(
                name: "EmployeeId",
                table: "Finance_FeeRefundRequests");

            migrationBuilder.DropColumn(
                name: "FinanceExternalSourceLinkId",
                table: "Finance_FeeRefundRequests");

            migrationBuilder.DropColumn(
                name: "Currency",
                table: "Finance_FeeRecordMetadatas");

            migrationBuilder.DropColumn(
                name: "EmployeeId",
                table: "Finance_FeeRecordMetadatas");

            migrationBuilder.DropColumn(
                name: "Currency",
                table: "Finance_FeePaymentReceipts");

            migrationBuilder.DropColumn(
                name: "EmployeeId",
                table: "Finance_FeePaymentReceipts");

            migrationBuilder.DropColumn(
                name: "ResultEntityId",
                table: "Finance_ExternalReceivableCommands");

            migrationBuilder.DropColumn(
                name: "ResultEntityType",
                table: "Finance_ExternalReceivableCommands");

            migrationBuilder.DropColumn(
                name: "EmployeeId",
                table: "Finance_ChargeWaiverApplications");

            migrationBuilder.DropColumn(
                name: "EmployeeId",
                table: "Finance_ChargeCollectionRecords");

            migrationBuilder.DropColumn(
                name: "EmployeeId",
                table: "Finance_CashCollections");

            migrationBuilder.DropColumn(
                name: "AfterBorrowerScopeCaptureId",
                table: "AppLibraryReservationEvents");

            migrationBuilder.DropColumn(
                name: "BeforeBorrowerScopeCaptureId",
                table: "AppLibraryReservationEvents");

            migrationBuilder.DropColumn(
                name: "AfterBorrowerScopeCaptureId",
                table: "AppLibraryLoanEvents");

            migrationBuilder.DropColumn(
                name: "BeforeBorrowerScopeCaptureId",
                table: "AppLibraryLoanEvents");

            migrationBuilder.DropColumn(
                name: "BorrowerScopeCaptureId",
                table: "AppBookReservations");

            migrationBuilder.DropColumn(
                name: "BorrowerScopeCaptureVersion",
                table: "AppBookReservations");

            migrationBuilder.DropColumn(
                name: "BorrowerScopeCaptureId",
                table: "AppBookIssues");

            migrationBuilder.DropColumn(
                name: "BorrowerScopeCaptureVersion",
                table: "AppBookIssues");

            migrationBuilder.DropColumn(
                name: "BorrowerScopeProvenance",
                table: "AppBookIssues");

            migrationBuilder.AlterColumn<double>(
                name: "TotalPaidFee",
                table: "Finance_FeeRecordMetadatas",
                type: "double precision",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<Guid>(
                name: "EducationMediumId",
                table: "Finance_FeePaymentReceipts",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "BoardId",
                table: "Finance_FeePaymentReceipts",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "StudentId",
                table: "Finance_ChargeWaiverApplications",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Reason",
                table: "Finance_ChargeWaiverApplications",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(1000)",
                oldMaxLength: 1000);

            migrationBuilder.AlterColumn<Guid>(
                name: "StudentId",
                table: "Finance_ChargeCollectionRecords",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<double>(
                name: "Amount",
                table: "Finance_ChargeCollectionRecords",
                type: "double precision",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AddCheckConstraint(
                name: "CK_PaymentMethodRecords_Payer",
                table: "Finance_PaymentMethodRecords",
                sql: "\"StudentId\" IS NOT NULL OR \"FinanceFeeAccountId\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_FeeRefundRequests_Payer",
                table: "Finance_FeeRefundRequests",
                sql: "\"StudentId\" IS NOT NULL OR \"FinanceFeeAccountId\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_FeeRecordMetadatas_Payer",
                table: "Finance_FeeRecordMetadatas",
                sql: "\"StudentId\" IS NOT NULL OR \"FinanceFeeAccountId\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_FeePaymentReceipts_Payer",
                table: "Finance_FeePaymentReceipts",
                sql: "\"StudentId\" IS NOT NULL OR \"FinanceFeeAccountId\" IS NOT NULL");
        }
    }
}
