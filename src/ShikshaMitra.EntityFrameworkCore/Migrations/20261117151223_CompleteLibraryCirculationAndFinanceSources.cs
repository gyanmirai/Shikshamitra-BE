using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class CompleteLibraryCirculationAndFinanceSources : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            PreflightLegacySchoolDates(migrationBuilder);
            migrationBuilder.DropForeignKey(
                name: "FK_AppBookReservations_AppBooks_BookId",
                table: "AppBookReservations");

            migrationBuilder.AlterColumn<Guid>(
                name: "StudentId",
                table: "Finance_Charges",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<Guid>(
                name: "StandardId",
                table: "Finance_Charges",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<Guid>(
                name: "EducationMediumId",
                table: "Finance_Charges",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "DueDate",
                table: "Finance_Charges",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<Guid>(
                name: "DivisionId",
                table: "Finance_Charges",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<Guid>(
                name: "BoardId",
                table: "Finance_Charges",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<int>(
                name: "DueDateProvenance",
                table: "Finance_Charges",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "EmployeeId",
                table: "Finance_Charges",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "SchoolAssessmentDate",
                table: "Finance_Charges",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "SchoolDueDate",
                table: "Finance_Charges",
                type: "date",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "EducationMediumId",
                table: "Finance_ChargeRequests",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "DueDate",
                table: "Finance_ChargeRequests",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<Guid>(
                name: "BoardId",
                table: "Finance_ChargeRequests",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<int>(
                name: "DueDateProvenance",
                table: "Finance_ChargeRequests",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateOnly>(
                name: "SchoolDueDate",
                table: "Finance_ChargeRequests",
                type: "date",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "ActorId",
                table: "AppLibraryCopyEvents",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<int>(
                name: "ActorKind",
                table: "AppLibraryCopyEvents",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "SystemPurpose",
                table: "AppLibraryCopyEvents",
                type: "integer",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "DueDate",
                table: "AppLibraryCharges",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ChargeDate",
                table: "AppLibraryCharges",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AddColumn<string>(
                name: "AcademicYearEvidenceJson",
                table: "AppLibraryCharges",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AcademicYearId",
                table: "AppLibraryCharges",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AcademicYearProvenance",
                table: "AppLibraryCharges",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ChargeDateProvenance",
                table: "AppLibraryCharges",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "AppLibraryCharges",
                type: "character varying(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DueDateProvenance",
                table: "AppLibraryCharges",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "FinanceSourceLinkId",
                table: "AppLibraryCharges",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "SchoolChargeDate",
                table: "AppLibraryCharges",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "SchoolDueDate",
                table: "AppLibraryCharges",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceDateEvidenceJson",
                table: "AppLibraryCharges",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DueDateMode",
                table: "AppLibraryBorrowingPolicyVersions",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ReserveDueDate",
                table: "AppBookReservations",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<DateTime>(
                name: "RequestedDate",
                table: "AppBookReservations",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AddColumn<Guid>(
                name: "AcademicYearId",
                table: "AppBookReservations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BorrowerBoardId",
                table: "AppBookReservations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BorrowerDivisionId",
                table: "AppBookReservations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BorrowerMediumId",
                table: "AppBookReservations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BorrowerScopeProvenance",
                table: "AppBookReservations",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "BorrowerScopeSnapshotJson",
                table: "AppBookReservations",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BorrowerStageId",
                table: "AppBookReservations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BorrowerStandardId",
                table: "AppBookReservations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CanonicalMemberId",
                table: "AppBookReservations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "EndedAt",
                table: "AppBookReservations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MemberRole",
                table: "AppBookReservations",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OriginalMemberId",
                table: "AppBookReservations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PolicySnapshotJson",
                table: "AppBookReservations",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "QueueProvenance",
                table: "AppBookReservations",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<long>(
                name: "QueueSequence",
                table: "AppBookReservations",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RequestExpiryDateProvenance",
                table: "AppBookReservations",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "RequestedDateProvenance",
                table: "AppBookReservations",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ReviewReason",
                table: "AppBookReservations",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Revision",
                table: "AppBookReservations",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateOnly>(
                name: "SchoolRequestExpiryDate",
                table: "AppBookReservations",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "SchoolRequestedDate",
                table: "AppBookReservations",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "State",
                table: "AppBookReservations",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "TerminalReason",
                table: "AppBookReservations",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "IssueDate",
                table: "AppBookIssues",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<DateTime>(
                name: "DueDate",
                table: "AppBookIssues",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AddColumn<Guid>(
                name: "AcademicYearId",
                table: "AppBookIssues",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BorrowerBoardId",
                table: "AppBookIssues",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BorrowerDivisionId",
                table: "AppBookIssues",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BorrowerMediumId",
                table: "AppBookIssues",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BorrowerSnapshotJson",
                table: "AppBookIssues",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BorrowerStageId",
                table: "AppBookIssues",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BorrowerStandardId",
                table: "AppBookIssues",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DueDateProvenance",
                table: "AppBookIssues",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "IssueDateProvenance",
                table: "AppBookIssues",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "IssueRecordedAt",
                table: "AppBookIssues",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "IssuedByUserId",
                table: "AppBookIssues",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OriginalCalendarSnapshotJson",
                table: "AppBookIssues",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "OriginalSchoolDueDate",
                table: "AppBookIssues",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PolicySnapshotJson",
                table: "AppBookIssues",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RenewalCount",
                table: "AppBookIssues",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ReturnDateProvenance",
                table: "AppBookIssues",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateOnly>(
                name: "SchoolDueDate",
                table: "AppBookIssues",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "SchoolIssueDate",
                table: "AppBookIssues",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "SchoolReturnDate",
                table: "AppBookIssues",
                type: "date",
                nullable: true);

            BackfillUnconfirmedLibraryDates(migrationBuilder);

            migrationBuilder.CreateTable(
                name: "AppLibraryHolds",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReservationId = table.Column<Guid>(type: "uuid", nullable: false),
                    BookId = table.Column<Guid>(type: "uuid", nullable: false),
                    CopyId = table.Column<Guid>(type: "uuid", nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false),
                    AllocatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EndedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    SchoolPickupDeadline = table.Column<DateOnly>(type: "date", nullable: true),
                    PickupDeadlineProvenance = table.Column<int>(type: "integer", nullable: false),
                    TimeZoneId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    PolicySnapshotJson = table.Column<string>(type: "text", nullable: false),
                    CalendarSnapshotJson = table.Column<string>(type: "text", nullable: false),
                    EndReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    RelatedLoanId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppLibraryHolds", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppLibraryHolds_AppBookCopies_CopyId",
                        column: x => x.CopyId,
                        principalTable: "AppBookCopies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppLibraryHolds_AppBookIssues_RelatedLoanId",
                        column: x => x.RelatedLoanId,
                        principalTable: "AppBookIssues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppLibraryHolds_AppBookReservations_ReservationId",
                        column: x => x.ReservationId,
                        principalTable: "AppBookReservations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppLibraryHolds_AppBooks_BookId",
                        column: x => x.BookId,
                        principalTable: "AppBooks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AppLibraryLoanCommands",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Operation = table.Column<int>(type: "integer", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PayloadFingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    LoanId = table.Column<Guid>(type: "uuid", nullable: false),
                    ResultJson = table.Column<string>(type: "text", nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppLibraryLoanCommands", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppLibraryLoanCommands_AppBookIssues_LoanId",
                        column: x => x.LoanId,
                        principalTable: "AppBookIssues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AppLibraryLoanEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    LoanId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sequence = table.Column<long>(type: "bigint", nullable: false),
                    EventKind = table.Column<int>(type: "integer", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    BeforeJson = table.Column<string>(type: "text", nullable: false),
                    AfterJson = table.Column<string>(type: "text", nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppLibraryLoanEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppLibraryLoanEvents_AppBookIssues_LoanId",
                        column: x => x.LoanId,
                        principalTable: "AppBookIssues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Finance_ExternalReceivableSourceLinks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceKind = table.Column<int>(type: "integer", nullable: false),
                    SourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceVersion = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SourceFingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SourceSnapshotJson = table.Column<string>(type: "text", nullable: false),
                    FinanceChargeRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    FinanceChargeId = table.Column<Guid>(type: "uuid", nullable: false),
                    AcademicYearId = table.Column<Guid>(type: "uuid", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    CollectionPolicyVersionId = table.Column<Guid>(type: "uuid", nullable: true),
                    RefundPolicyVersionId = table.Column<Guid>(type: "uuid", nullable: true),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Finance_ExternalReceivableSourceLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Finance_ExternalReceivableSourceLinks_AppLibraryCharges_Sou~",
                        column: x => x.SourceId,
                        principalTable: "AppLibraryCharges",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Finance_ExternalReceivableSourceLinks_Finance_ChargeRequest~",
                        column: x => x.FinanceChargeRequestId,
                        principalTable: "Finance_ChargeRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Finance_ExternalReceivableSourceLinks_Finance_Charges_Finan~",
                        column: x => x.FinanceChargeId,
                        principalTable: "Finance_Charges",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Finance_ExternalReceivableSourceLinks_Finance_PolicyVersion~",
                        column: x => x.CollectionPolicyVersionId,
                        principalTable: "Finance_PolicyVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Finance_ExternalReceivableSourceLinks_Finance_PolicyVersio~1",
                        column: x => x.RefundPolicyVersionId,
                        principalTable: "Finance_PolicyVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AppLibraryReservationEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReservationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sequence = table.Column<long>(type: "bigint", nullable: false),
                    EventType = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    HoldId = table.Column<Guid>(type: "uuid", nullable: true),
                    CopyId = table.Column<Guid>(type: "uuid", nullable: true),
                    LoanId = table.Column<Guid>(type: "uuid", nullable: true),
                    PreviousState = table.Column<int>(type: "integer", nullable: false),
                    NextState = table.Column<int>(type: "integer", nullable: false),
                    ActorKind = table.Column<int>(type: "integer", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: true),
                    SystemPurpose = table.Column<int>(type: "integer", nullable: true),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    BeforeSnapshotJson = table.Column<string>(type: "text", nullable: false),
                    AfterSnapshotJson = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppLibraryReservationEvents", x => x.Id);
                    table.CheckConstraint("CK_LibraryReservationEvents_Actor", "(\"ActorKind\" = 1 AND \"ActorId\" IS NOT NULL AND \"SystemPurpose\" IS NULL) OR (\"ActorKind\" = 2 AND \"ActorId\" IS NULL AND \"SystemPurpose\" IS NOT NULL AND \"SystemPurpose\" IN (1, 2))");
                    table.ForeignKey(
                        name: "FK_AppLibraryReservationEvents_AppBookCopies_CopyId",
                        column: x => x.CopyId,
                        principalTable: "AppBookCopies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppLibraryReservationEvents_AppBookIssues_LoanId",
                        column: x => x.LoanId,
                        principalTable: "AppBookIssues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppLibraryReservationEvents_AppBookReservations_Reservation~",
                        column: x => x.ReservationId,
                        principalTable: "AppBookReservations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppLibraryReservationEvents_AppLibraryHolds_HoldId",
                        column: x => x.HoldId,
                        principalTable: "AppLibraryHolds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Finance_ExternalReceivableCommands",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Operation = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CommandFingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SourceLinkId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Finance_ExternalReceivableCommands", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Finance_ExternalReceivableCommands_Finance_ExternalReceivab~",
                        column: x => x.SourceLinkId,
                        principalTable: "Finance_ExternalReceivableSourceLinks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Finance_Charges_EmployeeId",
                table: "Finance_Charges",
                column: "EmployeeId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_FinanceCharges_Payer",
                table: "Finance_Charges",
                sql: "(\"StudentId\" IS NOT NULL AND \"EmployeeId\" IS NULL) OR (\"StudentId\" IS NULL AND \"EmployeeId\" IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_LibraryCopyEvents_Actor",
                table: "AppLibraryCopyEvents",
                sql: "(\"ActorKind\" = 1 AND \"ActorId\" IS NOT NULL AND \"SystemPurpose\" IS NULL) OR (\"ActorKind\" = 2 AND \"ActorId\" IS NULL AND \"SystemPurpose\" IS NOT NULL AND \"SystemPurpose\" IN (1, 2))");

            migrationBuilder.CreateIndex(
                name: "IX_AppLibraryCharges_FinanceSourceLinkId",
                table: "AppLibraryCharges",
                column: "FinanceSourceLinkId");

            migrationBuilder.CreateIndex(
                name: "IX_AppBookReservations_TenantId_BookId_CanonicalMemberId",
                table: "AppBookReservations",
                columns: new[] { "TenantId", "BookId", "CanonicalMemberId" },
                unique: true,
                filter: "\"State\" IN (0, 1) AND \"CanonicalMemberId\" IS NOT NULL")
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_AppBookReservations_TenantId_QueueSequence",
                table: "AppBookReservations",
                columns: new[] { "TenantId", "QueueSequence" },
                unique: true,
                filter: "\"QueueSequence\" IS NOT NULL")
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_AppBookIssues_TenantId_AcademicYearId_Status_SchoolDueDate",
                table: "AppBookIssues",
                columns: new[] { "TenantId", "AcademicYearId", "Status", "SchoolDueDate" });

            migrationBuilder.CreateIndex(
                name: "IX_AppLibraryHolds_BookId",
                table: "AppLibraryHolds",
                column: "BookId");

            migrationBuilder.CreateIndex(
                name: "IX_AppLibraryHolds_CopyId",
                table: "AppLibraryHolds",
                column: "CopyId");

            migrationBuilder.CreateIndex(
                name: "IX_AppLibraryHolds_RelatedLoanId",
                table: "AppLibraryHolds",
                column: "RelatedLoanId");

            migrationBuilder.CreateIndex(
                name: "IX_AppLibraryHolds_ReservationId",
                table: "AppLibraryHolds",
                column: "ReservationId");

            migrationBuilder.CreateIndex(
                name: "IX_AppLibraryHolds_TenantId_CopyId",
                table: "AppLibraryHolds",
                columns: new[] { "TenantId", "CopyId" },
                unique: true,
                filter: "\"State\" = 0")
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_AppLibraryHolds_TenantId_ReservationId",
                table: "AppLibraryHolds",
                columns: new[] { "TenantId", "ReservationId" },
                unique: true,
                filter: "\"State\" = 0")
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_AppLibraryLoanCommands_LoanId",
                table: "AppLibraryLoanCommands",
                column: "LoanId");

            migrationBuilder.CreateIndex(
                name: "IX_AppLibraryLoanCommands_TenantId_ActorId_Operation_Idempoten~",
                table: "AppLibraryLoanCommands",
                columns: new[] { "TenantId", "ActorId", "Operation", "IdempotencyKey" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_AppLibraryLoanEvents_LoanId",
                table: "AppLibraryLoanEvents",
                column: "LoanId");

            migrationBuilder.CreateIndex(
                name: "IX_AppLibraryLoanEvents_TenantId_LoanId_Sequence",
                table: "AppLibraryLoanEvents",
                columns: new[] { "TenantId", "LoanId", "Sequence" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_AppLibraryReservationEvents_CopyId",
                table: "AppLibraryReservationEvents",
                column: "CopyId");

            migrationBuilder.CreateIndex(
                name: "IX_AppLibraryReservationEvents_HoldId",
                table: "AppLibraryReservationEvents",
                column: "HoldId");

            migrationBuilder.CreateIndex(
                name: "IX_AppLibraryReservationEvents_LoanId",
                table: "AppLibraryReservationEvents",
                column: "LoanId");

            migrationBuilder.CreateIndex(
                name: "IX_AppLibraryReservationEvents_ReservationId",
                table: "AppLibraryReservationEvents",
                column: "ReservationId");

            migrationBuilder.CreateIndex(
                name: "IX_AppLibraryReservationEvents_TenantId_ReservationId_Sequence",
                table: "AppLibraryReservationEvents",
                columns: new[] { "TenantId", "ReservationId", "Sequence" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_Finance_ExternalReceivableCommands_SourceLinkId",
                table: "Finance_ExternalReceivableCommands",
                column: "SourceLinkId");

            migrationBuilder.CreateIndex(
                name: "UX_ExternalReceivableCommands_Tenant_Operation_Key",
                table: "Finance_ExternalReceivableCommands",
                columns: new[] { "TenantId", "Operation", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Finance_ExternalReceivableSourceLinks_CollectionPolicyVersi~",
                table: "Finance_ExternalReceivableSourceLinks",
                column: "CollectionPolicyVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_Finance_ExternalReceivableSourceLinks_FinanceChargeId",
                table: "Finance_ExternalReceivableSourceLinks",
                column: "FinanceChargeId");

            migrationBuilder.CreateIndex(
                name: "IX_Finance_ExternalReceivableSourceLinks_FinanceChargeRequestId",
                table: "Finance_ExternalReceivableSourceLinks",
                column: "FinanceChargeRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_Finance_ExternalReceivableSourceLinks_RefundPolicyVersionId",
                table: "Finance_ExternalReceivableSourceLinks",
                column: "RefundPolicyVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_Finance_ExternalReceivableSourceLinks_SourceId",
                table: "Finance_ExternalReceivableSourceLinks",
                column: "SourceId");

            migrationBuilder.CreateIndex(
                name: "UX_ExternalReceivableSources_Tenant_Charge",
                table: "Finance_ExternalReceivableSourceLinks",
                columns: new[] { "TenantId", "FinanceChargeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_ExternalReceivableSources_Tenant_Request",
                table: "Finance_ExternalReceivableSourceLinks",
                columns: new[] { "TenantId", "FinanceChargeRequestId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_ExternalReceivableSources_Tenant_Source",
                table: "Finance_ExternalReceivableSourceLinks",
                columns: new[] { "TenantId", "SourceKind", "SourceId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_AppBookReservations_AppBooks_BookId",
                table: "AppBookReservations",
                column: "BookId",
                principalTable: "AppBooks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AppLibraryCharges_Finance_ExternalReceivableSourceLinks_Fin~",
                table: "AppLibraryCharges",
                column: "FinanceSourceLinkId",
                principalTable: "Finance_ExternalReceivableSourceLinks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Finance_Charges_AppEmployees_EmployeeId",
                table: "Finance_Charges",
                column: "EmployeeId",
                principalTable: "AppEmployees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            InstallCirculationEvidenceGuards(migrationBuilder);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            RefuseLossyCirculationRollback(migrationBuilder);
            migrationBuilder.DropForeignKey(
                name: "FK_AppBookReservations_AppBooks_BookId",
                table: "AppBookReservations");

            migrationBuilder.DropForeignKey(
                name: "FK_AppLibraryCharges_Finance_ExternalReceivableSourceLinks_Fin~",
                table: "AppLibraryCharges");

            migrationBuilder.DropForeignKey(
                name: "FK_Finance_Charges_AppEmployees_EmployeeId",
                table: "Finance_Charges");

            migrationBuilder.DropTable(
                name: "AppLibraryLoanCommands");

            migrationBuilder.DropTable(
                name: "AppLibraryLoanEvents");

            migrationBuilder.DropTable(
                name: "AppLibraryReservationEvents");

            migrationBuilder.DropTable(
                name: "Finance_ExternalReceivableCommands");

            migrationBuilder.DropTable(
                name: "AppLibraryHolds");

            migrationBuilder.DropTable(
                name: "Finance_ExternalReceivableSourceLinks");

            DropCirculationEvidenceGuards(migrationBuilder);

            migrationBuilder.DropIndex(
                name: "IX_Finance_Charges_EmployeeId",
                table: "Finance_Charges");

            migrationBuilder.DropCheckConstraint(
                name: "CK_FinanceCharges_Payer",
                table: "Finance_Charges");

            migrationBuilder.DropCheckConstraint(
                name: "CK_LibraryCopyEvents_Actor",
                table: "AppLibraryCopyEvents");

            migrationBuilder.DropIndex(
                name: "IX_AppLibraryCharges_FinanceSourceLinkId",
                table: "AppLibraryCharges");

            migrationBuilder.DropIndex(
                name: "IX_AppBookReservations_TenantId_BookId_CanonicalMemberId",
                table: "AppBookReservations");

            migrationBuilder.DropIndex(
                name: "IX_AppBookReservations_TenantId_QueueSequence",
                table: "AppBookReservations");

            migrationBuilder.DropIndex(
                name: "IX_AppBookIssues_TenantId_AcademicYearId_Status_SchoolDueDate",
                table: "AppBookIssues");

            migrationBuilder.DropColumn(
                name: "DueDateProvenance",
                table: "Finance_Charges");

            migrationBuilder.DropColumn(
                name: "EmployeeId",
                table: "Finance_Charges");

            migrationBuilder.DropColumn(
                name: "SchoolAssessmentDate",
                table: "Finance_Charges");

            migrationBuilder.DropColumn(
                name: "SchoolDueDate",
                table: "Finance_Charges");

            migrationBuilder.DropColumn(
                name: "DueDateProvenance",
                table: "Finance_ChargeRequests");

            migrationBuilder.DropColumn(
                name: "SchoolDueDate",
                table: "Finance_ChargeRequests");

            migrationBuilder.DropColumn(
                name: "ActorKind",
                table: "AppLibraryCopyEvents");

            migrationBuilder.DropColumn(
                name: "SystemPurpose",
                table: "AppLibraryCopyEvents");

            migrationBuilder.DropColumn(
                name: "AcademicYearEvidenceJson",
                table: "AppLibraryCharges");

            migrationBuilder.DropColumn(
                name: "AcademicYearId",
                table: "AppLibraryCharges");

            migrationBuilder.DropColumn(
                name: "AcademicYearProvenance",
                table: "AppLibraryCharges");

            migrationBuilder.DropColumn(
                name: "ChargeDateProvenance",
                table: "AppLibraryCharges");

            migrationBuilder.DropColumn(
                name: "Currency",
                table: "AppLibraryCharges");

            migrationBuilder.DropColumn(
                name: "DueDateProvenance",
                table: "AppLibraryCharges");

            migrationBuilder.DropColumn(
                name: "FinanceSourceLinkId",
                table: "AppLibraryCharges");

            migrationBuilder.DropColumn(
                name: "SchoolChargeDate",
                table: "AppLibraryCharges");

            migrationBuilder.DropColumn(
                name: "SchoolDueDate",
                table: "AppLibraryCharges");

            migrationBuilder.DropColumn(
                name: "SourceDateEvidenceJson",
                table: "AppLibraryCharges");

            migrationBuilder.DropColumn(
                name: "DueDateMode",
                table: "AppLibraryBorrowingPolicyVersions");

            migrationBuilder.DropColumn(
                name: "AcademicYearId",
                table: "AppBookReservations");

            migrationBuilder.DropColumn(
                name: "BorrowerBoardId",
                table: "AppBookReservations");

            migrationBuilder.DropColumn(
                name: "BorrowerDivisionId",
                table: "AppBookReservations");

            migrationBuilder.DropColumn(
                name: "BorrowerMediumId",
                table: "AppBookReservations");

            migrationBuilder.DropColumn(
                name: "BorrowerScopeProvenance",
                table: "AppBookReservations");

            migrationBuilder.DropColumn(
                name: "BorrowerScopeSnapshotJson",
                table: "AppBookReservations");

            migrationBuilder.DropColumn(
                name: "BorrowerStageId",
                table: "AppBookReservations");

            migrationBuilder.DropColumn(
                name: "BorrowerStandardId",
                table: "AppBookReservations");

            migrationBuilder.DropColumn(
                name: "CanonicalMemberId",
                table: "AppBookReservations");

            migrationBuilder.DropColumn(
                name: "EndedAt",
                table: "AppBookReservations");

            migrationBuilder.DropColumn(
                name: "MemberRole",
                table: "AppBookReservations");

            migrationBuilder.DropColumn(
                name: "OriginalMemberId",
                table: "AppBookReservations");

            migrationBuilder.DropColumn(
                name: "PolicySnapshotJson",
                table: "AppBookReservations");

            migrationBuilder.DropColumn(
                name: "QueueProvenance",
                table: "AppBookReservations");

            migrationBuilder.DropColumn(
                name: "QueueSequence",
                table: "AppBookReservations");

            migrationBuilder.DropColumn(
                name: "RequestExpiryDateProvenance",
                table: "AppBookReservations");

            migrationBuilder.DropColumn(
                name: "RequestedDateProvenance",
                table: "AppBookReservations");

            migrationBuilder.DropColumn(
                name: "ReviewReason",
                table: "AppBookReservations");

            migrationBuilder.DropColumn(
                name: "Revision",
                table: "AppBookReservations");

            migrationBuilder.DropColumn(
                name: "SchoolRequestExpiryDate",
                table: "AppBookReservations");

            migrationBuilder.DropColumn(
                name: "SchoolRequestedDate",
                table: "AppBookReservations");

            migrationBuilder.DropColumn(
                name: "State",
                table: "AppBookReservations");

            migrationBuilder.DropColumn(
                name: "TerminalReason",
                table: "AppBookReservations");

            migrationBuilder.DropColumn(
                name: "AcademicYearId",
                table: "AppBookIssues");

            migrationBuilder.DropColumn(
                name: "BorrowerBoardId",
                table: "AppBookIssues");

            migrationBuilder.DropColumn(
                name: "BorrowerDivisionId",
                table: "AppBookIssues");

            migrationBuilder.DropColumn(
                name: "BorrowerMediumId",
                table: "AppBookIssues");

            migrationBuilder.DropColumn(
                name: "BorrowerSnapshotJson",
                table: "AppBookIssues");

            migrationBuilder.DropColumn(
                name: "BorrowerStageId",
                table: "AppBookIssues");

            migrationBuilder.DropColumn(
                name: "BorrowerStandardId",
                table: "AppBookIssues");

            migrationBuilder.DropColumn(
                name: "DueDateProvenance",
                table: "AppBookIssues");

            migrationBuilder.DropColumn(
                name: "IssueDateProvenance",
                table: "AppBookIssues");

            migrationBuilder.DropColumn(
                name: "IssueRecordedAt",
                table: "AppBookIssues");

            migrationBuilder.DropColumn(
                name: "IssuedByUserId",
                table: "AppBookIssues");

            migrationBuilder.DropColumn(
                name: "OriginalCalendarSnapshotJson",
                table: "AppBookIssues");

            migrationBuilder.DropColumn(
                name: "OriginalSchoolDueDate",
                table: "AppBookIssues");

            migrationBuilder.DropColumn(
                name: "PolicySnapshotJson",
                table: "AppBookIssues");

            migrationBuilder.DropColumn(
                name: "RenewalCount",
                table: "AppBookIssues");

            migrationBuilder.DropColumn(
                name: "ReturnDateProvenance",
                table: "AppBookIssues");

            migrationBuilder.DropColumn(
                name: "SchoolDueDate",
                table: "AppBookIssues");

            migrationBuilder.DropColumn(
                name: "SchoolIssueDate",
                table: "AppBookIssues");

            migrationBuilder.DropColumn(
                name: "SchoolReturnDate",
                table: "AppBookIssues");

            migrationBuilder.AlterColumn<Guid>(
                name: "StudentId",
                table: "Finance_Charges",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "StandardId",
                table: "Finance_Charges",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "EducationMediumId",
                table: "Finance_Charges",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "DueDate",
                table: "Finance_Charges",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "DivisionId",
                table: "Finance_Charges",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "BoardId",
                table: "Finance_Charges",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "EducationMediumId",
                table: "Finance_ChargeRequests",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "DueDate",
                table: "Finance_ChargeRequests",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "BoardId",
                table: "Finance_ChargeRequests",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "ActorId",
                table: "AppLibraryCopyEvents",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "DueDate",
                table: "AppLibraryCharges",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ChargeDate",
                table: "AppLibraryCharges",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ReserveDueDate",
                table: "AppBookReservations",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "RequestedDate",
                table: "AppBookReservations",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "IssueDate",
                table: "AppBookIssues",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "DueDate",
                table: "AppBookIssues",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_AppBookReservations_AppBooks_BookId",
                table: "AppBookReservations",
                column: "BookId",
                principalTable: "AppBooks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
