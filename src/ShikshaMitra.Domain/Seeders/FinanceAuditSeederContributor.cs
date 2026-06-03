using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ShikshaMitra.Finance.Audit;
using ShikshaMitra.Onboarding.AcademicYears;
using ShikshaMitra.Students;
using ShikshaMitra.TenantSettings.AcademicYears;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace ShikshaMitra.Seeders;

/// <summary>
/// Seeds <see cref="FinanceAuditEvent"/> rows for manual Audit Trail UI testing.
/// Idempotent: skips when any event exists with correlation id prefix <see cref="SeedCorrelationPrefix"/>.
///
/// After running DbMigrator / data seed (Demo profile), search the Audit Trail UI using the
/// student names and GR numbers logged at Information level by this seeder.
/// </summary>
public class FinanceAuditSeederContributor : BaseSeederContributor
{
	public const string SeedCorrelationPrefix = "seed-finance-audit-v1";
	private const string SeedActorName = "Finance Demo Admin";
	private const string SeedActorRole = "Accountant";

	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

	private readonly IRepository<FinanceAuditEvent, Guid> _auditRepository;
	private readonly IRepository<Student, Guid> _studentRepository;
	private readonly IAcademicYearRepository _academicYearRepository;
	private readonly ILogger<FinanceAuditSeederContributor> _logger;

	public FinanceAuditSeederContributor(
		ICurrentTenant currentTenant,
		IGuidGenerator guidGenerator,
		IUnitOfWork unitOfWork,
		IRepository<FinanceAuditEvent, Guid> auditRepository,
		IRepository<Student, Guid> studentRepository,
		IAcademicYearRepository academicYearRepository,
		ILogger<FinanceAuditSeederContributor> logger)
		: base(currentTenant, guidGenerator, unitOfWork)
	{
		_auditRepository = auditRepository;
		_studentRepository = studentRepository;
		_academicYearRepository = academicYearRepository;
		_logger = logger;
	}

	public override async Task SeedAsync(DataSeedContext context)
	{
		if (await _auditRepository.AnyAsync(x =>
			    x.CorrelationId != null && x.CorrelationId.StartsWith(SeedCorrelationPrefix)))
		{
			_logger.LogInformation(
				"[FinanceAuditSeeder] Audit seed data already present (prefix {Prefix}). Skipping. TenantId={TenantId}",
				SeedCorrelationPrefix,
				CurrentTenant.Id);
			return;
		}

		var academicYear = await ResolveAcademicYearAsync();
		if (academicYear == null)
		{
			_logger.LogWarning("[FinanceAuditSeeder] No academic year found. Skipping.");
			return;
		}

		var students = await _studentRepository.GetListAsync(s =>
			s.AcademicYearId == academicYear.Id &&
			(s.Status == StudentLifeCycleStatusEnum.Enrolled ||
			 s.Status == StudentLifeCycleStatusEnum.Admission));

		if (students.Count == 0)
		{
			_logger.LogWarning(
				"[FinanceAuditSeeder] No students for AcademicYearId={AcademicYearId}. Run StudentSeeder first.",
				academicYear.Id);
			return;
		}

		students = students
			.OrderBy(s => s.GrNo ?? int.MaxValue)
			.ThenBy(s => s.RollNo ?? int.MaxValue)
			.Take(8)
			.ToList();

		foreach (var student in students.Where(s => s.Status == StudentLifeCycleStatusEnum.Admission))
		{
			student.ApproveAdmission();
			await _studentRepository.UpdateAsync(student, autoSave: false);
		}

		var events = BuildSeedEvents(academicYear.Id, students);
		await _auditRepository.InsertManyAsync(events, autoSave: false);
		await CurrentUnitOfWork.SaveChangesAsync();

		_logger.LogInformation(
			"[FinanceAuditSeeder] Inserted {Count} audit events for TenantId={TenantId}. UI search hints:",
			events.Count,
			CurrentTenant.Id);

		foreach (var student in students.Take(5))
		{
			var label = FormatStudentLabel(student);
			_logger.LogInformation(
				"[FinanceAuditSeeder]   • {Label} — search name \"{FirstName}\" or GR \"{GrNo}\" or roll \"{RollNo}\"",
				label,
				student.FirstName ?? "",
				student.GrNo?.ToString() ?? "",
				student.RollNo?.ToString() ?? "");
		}

		_logger.LogInformation(
			"[FinanceAuditSeeder]   • Keyword search: REC-SEED-000001, REC-SEED-000002, REF-SEED-000001, {Actor}",
			SeedActorName);
	}

	private async Task<AcademicYear?> ResolveAcademicYearAsync()
	{
		var currentYear = DateTime.Now.Year;
		var year = await _academicYearRepository.FirstOrDefaultAsync(y => y.Name.StartsWith(currentYear.ToString()));
		return year ?? await _academicYearRepository.FirstOrDefaultAsync();
	}

	private List<FinanceAuditEvent> BuildSeedEvents(Guid academicYearId, List<Student> students)
	{
		var now = DateTime.UtcNow;
		var events = new List<FinanceAuditEvent>();
		var day = 0;

		void Add(
			Student? student,
			string entityType,
			string action,
			string entityName,
			string summary,
			string correlationSuffix,
			string? reason = null,
			object[]? changedFields = null,
			object? context = null)
		{
			var entityId = GuidGenerator.Create();
			var correlationId = $"{SeedCorrelationPrefix}-{correlationSuffix}";
			var occurredAt = now.AddDays(-day);
			day++;

			events.Add(new FinanceAuditEvent(
				GuidGenerator.Create(),
				CurrentTenant.Id,
				academicYearId,
				FinanceAuditSubModuleCodes.FromEntityType(entityType),
				entityType,
				entityId,
				entityName,
				action,
				student?.Id,
				student != null ? BuildStudentDisplayName(student) : null,
				student?.GrNo?.ToString(),
				student?.RollNo?.ToString(),
				null,
				SeedActorName,
				SeedActorRole,
				occurredAt,
				summary,
				reason,
				changedFields is { Length: > 0 } ? JsonSerializer.Serialize(changedFields, JsonOptions) : null,
				context is null ? null : JsonSerializer.Serialize(context, JsonOptions),
				correlationId,
				null));
		}

		var s0 = students[0];
		var s1 = students.Count > 1 ? students[1] : s0;
		var s2 = students.Count > 2 ? students[2] : s0;
		var s3 = students.Count > 3 ? students[3] : s0;
		var s4 = students.Count > 4 ? students[4] : s0;

		var amountFields = new object[]
		{
			new { field = "amountPaid", oldValue = "0", newValue = "1000" },
			new { field = "paymentMethod", oldValue = (string?)null, newValue = "cash" },
		};

		// ── Collections (fee payment + receipt thread) ───────────────────────
		Add(s0, FinanceAuditEntityTypes.FeePayment, FinanceAuditActions.Payment,
			"REC-SEED-000001",
			$"Fee collected ₹1,000 for {FormatStudentLabel(s0)} (Cash, June tuition).",
			$"{s0.Id:N}-payment-june",
			changedFields: amountFields,
			context: new { receiptNumber = "REC-SEED-000001", paymentMethod = "cash" });

		Add(s0, FinanceAuditEntityTypes.FeeReceipt, FinanceAuditActions.GenerateReceipt,
			"REC-SEED-000001",
			"Receipt REC-SEED-000001 generated after fee collection.",
			$"{s0.Id:N}-receipt-june",
			context: new { receiptNumber = "REC-SEED-000001" });

		Add(s0, FinanceAuditEntityTypes.FeePayment, FinanceAuditActions.Payment,
			"REC-SEED-000002",
			$"Fee collected ₹1,000 for {FormatStudentLabel(s0)} (UPI, July tuition).",
			$"{s0.Id:N}-payment-july",
			changedFields: new object[]
			{
				new { field = "amountPaid", oldValue = "0", newValue = "1000" },
				new { field = "paymentMethod", oldValue = (string?)null, newValue = "upi" },
			},
			context: new { receiptNumber = "REC-SEED-000002", upiId = "school.fees@okicici" });

		Add(s1, FinanceAuditEntityTypes.FeePayment, FinanceAuditActions.Payment,
			"REC-SEED-000003",
			$"Partial fee ₹500 collected for {FormatStudentLabel(s1)} (Cash, June).",
			$"{s1.Id:N}-payment-partial",
			changedFields: new object[]
			{
				new { field = "amountPaid", oldValue = "0", newValue = "500" },
				new { field = "outstanding", oldValue = "1000", newValue = "500" },
			},
			context: new { receiptNumber = "REC-SEED-000003" });

		// ── Charges ──────────────────────────────────────────────────────────
		Add(s1, FinanceAuditEntityTypes.FeeCharge, FinanceAuditActions.AddCharge,
			"Seed – Library Late Fee",
			$"Charge ₹250 added for {FormatStudentLabel(s1)} (library late return).",
			$"{s1.Id:N}-charge",
			reason: "Library book returned after due date",
			changedFields: new object[]
			{
				new { field = "amount", oldValue = (string?)null, newValue = "250" },
				new { field = "status", oldValue = (string?)null, newValue = "approved" },
			});

		// ── Refunds ──────────────────────────────────────────────────────────
		Add(s2, FinanceAuditEntityTypes.FeeRefund, FinanceAuditActions.Create,
			"REF-SEED-000001",
			$"Refund request REF-SEED-000001 created for {FormatStudentLabel(s2)} (₹3,500).",
			$"{s2.Id:N}-refund-create",
			changedFields: new object[]
			{
				new { field = "status", oldValue = (string?)null, newValue = "pending" },
				new { field = "totalAmount", oldValue = (string?)null, newValue = "3500" },
			});

		Add(s2, FinanceAuditEntityTypes.FeeRefund, FinanceAuditActions.Approve,
			"REF-SEED-000003",
			$"Refund REF-SEED-000003 approved for {FormatStudentLabel(s2)}.",
			$"{s2.Id:N}-refund-approve",
			reason: "Verified overpayment on September installment");

		// ── Concessions ──────────────────────────────────────────────────────
		Add(s3, FinanceAuditEntityTypes.ConcessionRequest, FinanceAuditActions.Create,
			"Concession – Sibling discount",
			$"Concession request submitted for {FormatStudentLabel(s3)} (10% sibling discount).",
			$"{s3.Id:N}-concession",
			reason: "Sibling already enrolled");

		Add(s3, FinanceAuditEntityTypes.ConcessionRequest, FinanceAuditActions.Approve,
			"Concession – Sibling discount",
			$"Concession approved for {FormatStudentLabel(s3)}.",
			$"{s3.Id:N}-concession-approve");

		// ── Adjustments ──────────────────────────────────────────────────────
		Add(s4, FinanceAuditEntityTypes.FeeAdjustment, FinanceAuditActions.Adjustment,
			"Fee waiver – sports scholarship",
			$"Fee adjustment applied for {FormatStudentLabel(s4)} (₹500 waiver).",
			$"{s4.Id:N}-adjustment",
			changedFields: new object[]
			{
				new { field = "waiverAmount", oldValue = "0", newValue = "500" },
			});

		// ── Reconciliation (no student) ──────────────────────────────────────
		Add(null, FinanceAuditEntityTypes.BankReconciliation, FinanceAuditActions.Verify,
			"Bank statement Mar 2026",
			"Bank reconciliation verified for March 2026 statement.",
			"reconciliation-mar",
			context: new { statementPeriod = "2026-03", matchedRows = 42 });

		// ── Income / expense ─────────────────────────────────────────────────
		if (students.Count > 5)
		{
			var s5 = students[5];
			Add(s5, FinanceAuditEntityTypes.IncomeEntry, FinanceAuditActions.Create,
				"Donation – alumni fund",
				"Income entry recorded: alumni donation ₹10,000.",
				$"{s5.Id:N}-income",
				context: new { category = "Donation", amount = 10000 });
		}

		Add(null, FinanceAuditEntityTypes.ExpenseEntry, FinanceAuditActions.Create,
			"Stationery purchase",
			"Expense entry recorded: stationery supplies ₹2,400.",
			"expense-stationery",
			context: new { vendor = "Seed Stationers", amount = 2400 });

		return events;
	}

	private static string BuildStudentDisplayName(Student student)
	{
		var parts = new[] { student.FirstName, student.MiddleName, student.LastName }
			.Where(p => !string.IsNullOrWhiteSpace(p))
			.Select(p => p!.Trim());
		return string.Join(" ", parts);
	}

	private static string FormatStudentLabel(Student student) =>
		$"{BuildStudentDisplayName(student)} (GR {student.GrNo}, Roll {student.RollNo})";
}
