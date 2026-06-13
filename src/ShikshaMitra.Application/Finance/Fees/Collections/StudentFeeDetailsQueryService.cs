using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Finance.Fees.Charges;
using ShikshaMitra.Finance.Fees.Collections.Dtos;
using ShikshaMitra.Finance.Fees.ConcessionRequests;
using ShikshaMitra.Finance.Fees.Concessions;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.Receipts;
using ShikshaMitra.Finance.Fees.Settings.ConcessionTypes;
using ShikshaMitra.Finance.Fees.Settings.FeeStructures;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment;
using ShikshaMitra.Students;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using ShikshaMitra.TenantSettings.EducationBoards;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Timing;
using Volo.Abp.Validation;

namespace ShikshaMitra.Finance.Fees.Collections;

[RemoteService(false)]
public class StudentFeeDetailsQueryService(
	IReadOnlyRepository<Student, Guid> studentRepository,
	IDueFeeRepository dueFeeRepository,
	IRepository<FeeRecordMetadata, Guid> feeRecordMetadataRepository,
	IRepository<FeeCollectionRecord, Guid> feeCollectionRecordRepository,
	IRepository<FeeCollectionDueFeeAllocation, Guid> dueFeeAllocationRepository,
	IRepository<ChargeCollectionRecord, Guid> chargeCollectionRecordRepository,
	IRepository<PaymentMethodRecord, Guid> paymentMethodRecordRepository,
	IRepository<FeeHead, Guid> feeHeadRepository,
	IRepository<Charge, Guid> chargeRepository,
	IRepository<StudentFeeStructure, Guid> studentFeeStructureRepository,
	IRepository<FeeStructureDetail, Guid> feeStructureDetailRepository,
	IRepository<ConcessionType, Guid> concessionTypeRepository,
	IConcessionRequestRepository concessionRequestRepository,
	FinanceScopeAccessService financeScopeAccessService,
	IClock clock
) : ApplicationService, ITransientDependency
{
	private static void EnsureNotEmpty(Guid value, string fieldName)
	{
		if (value != Guid.Empty) return;

		throw new AbpValidationException(
			"Validation failed",
			[
				new ValidationResult($"{fieldName} is required.", new[] { fieldName })
			]);
	}

	public async Task<FeeCollectFormDto> GetCollectFormAsync(
		Guid studentId, Guid academicYearId, CancellationToken cancellationToken = default)
	{
		EnsureNotEmpty(academicYearId, "academicYearId");

		await financeScopeAccessService.EnsureStudentAccessibleAsync(studentId, cancellationToken);

		var student = await GetStudentSnapshotAsync(studentId, cancellationToken);

		var studentInfo = BuildStudentInfo(student);

		var feeStructureAssignment = await studentFeeStructureRepository
			.FirstOrDefaultAsync(x => x.StudentId == studentId);

		var ledger = new List<FeeLedgerMonthDto>();
		var feeSummary = new FeeSummaryDto();

		if (feeStructureAssignment != null)
		{
			var dueFeeQ = await dueFeeRepository.GetQueryableAsync();
			var dueFees = dueFeeQ
				.Where(d => d.StudentId == studentId)
				.OrderBy(d => d.Year).ThenBy(d => d.Month)
				.ToList();

			var feeHeadIds = dueFees.Select(d => d.FeeHeadId).Distinct().ToList();
			var feeHeads = await feeHeadRepository.GetListAsync(fh => feeHeadIds.Contains(fh.Id));
			var feeHeadNameMap = feeHeads.ToDictionary(fh => fh.Id, fh => fh.Name);

			// ── Reconciliation amounts per DueFee ─────────────────────────────────
			var reconByDueFee = await GetInReconciliationByDueFeeAsync(studentId);

			var grouped = dueFees
				.GroupBy(d => (d.Year, d.Month))
				.OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month);

			foreach (var group in grouped)
			{
				var lines = group.Select(d => new FeeLedgerLineDto
				{
					DueFeeId = d.Id,
					FeeHeadId = d.FeeHeadId,
					FeeHeadName = feeHeadNameMap.GetValueOrDefault(d.FeeHeadId, string.Empty),
					Total = d.OriginalAmount,
					Paid = d.PaidAmount,
					Due = d.OutstandingAmount,
					ReconciliationAmount = reconByDueFee.GetValueOrDefault(d.Id, 0d),
					Status = d.OutstandingAmount <= 0d
						? FeeCollectionStatus.Paid
						: (d.PaidAmount > 0d ? FeeCollectionStatus.Partial : FeeCollectionStatus.Pending),
				}).ToList();

				ledger.Add(new FeeLedgerMonthDto
				{
					Month = group.Key.Month.ToString(),
					Year = group.Key.Year,
					TotalDue = lines.Sum(l => l.Due),
					Items = lines,
				});
			}

			feeSummary = new FeeSummaryDto
			{
				TotalFee = dueFees.Sum(d => d.OriginalAmount),
				PaidFee = dueFees.Sum(d => d.PaidAmount),
				PendingFee = dueFees.Sum(d => d.OutstandingAmount),
				ReconciliationAmount = reconByDueFee.Values.Sum(),
			};
		}

		var chargesQ = await chargeRepository.GetQueryableAsync();
		var approvedCharges = chargesQ
			.Where(c => c.StudentId == studentId
					 && c.AcademicYearId == academicYearId
					 && c.Status == ChargeStatus.Approved)
			.ToList();

		var chargeDtos = new List<ChargeDetailDto>();
		if (approvedCharges.Count > 0)
		{
			var chargeCollQ  = await chargeCollectionRecordRepository.GetQueryableAsync();
			var chargeMetaQ  = await feeRecordMetadataRepository.GetQueryableAsync();

			var reversedTxIds = chargeMetaQ
				.Where(m => m.Status == FeeCollectionStatus.Reversed)
				.Select(m => m.Id);

			var paidPerCharge = chargeCollQ
				.Where(r => r.StudentId == studentId
						 && !reversedTxIds.Contains(r.FeeRecordMetadataId))
				.GroupBy(r => r.ChargeId)
				.Select(g => new { ChargeId = g.Key, Paid = g.Sum(r => r.Amount) })
				.ToDictionary(x => x.ChargeId, x => x.Paid);

			chargeDtos = approvedCharges.Select(c =>
			{
				var net = (double)c.Amount;
				var paid = paidPerCharge.GetValueOrDefault(c.Id, 0d);
				var pending = Math.Max(0d, net - paid);
				return new ChargeDetailDto
				{
					ChargeId = c.Id,
					Name = string.IsNullOrWhiteSpace(c.ChargeName) ? c.PurposeDescription : c.ChargeName,
					Amount = net,
					PaidAmount = paid,
					PendingAmount = pending,
					Status = pending <= 0d ? FeeCollectionStatus.Paid : (paid > 0d ? FeeCollectionStatus.Partial : FeeCollectionStatus.Pending),
				};
			}).ToList();
		}

		return new FeeCollectFormDto
		{
			Student = studentInfo,
			Summary = feeSummary,
			Ledger = ledger,
			Charges = chargeDtos,
		};
	}

	public async Task<StudentFeeDetailsDto> GetStudentFeeDetailsAsync(
		Guid studentId, Guid academicYearId, CancellationToken cancellationToken = default)
	{
		EnsureNotEmpty(academicYearId, "academicYearId");

		await financeScopeAccessService.EnsureStudentAccessibleAsync(studentId, cancellationToken);

		var student        = await GetStudentSnapshotAsync(studentId, cancellationToken);
		var studentSummary = BuildStudentSummary(student);

		var feeStructureAssignment = await studentFeeStructureRepository
			.FirstOrDefaultAsync(x => x.StudentId == studentId);

		var dueFees     = await GetDueFeesAsync(studentId);
		var overdueFee  = CalculateOverdueFee(dueFees);
		var feeHeads    = await BuildFeeHeadSummariesAsync(studentId, feeStructureAssignment, dueFees);
		var charges     = await BuildChargeDetailsAsync(studentId, academicYearId);
		var payments    = await BuildPaymentHistoryAsync(studentId, academicYearId);
		var concessions = await BuildConcessionDetailsAsync(studentId);
		var feeOverview = BuildFeeOverview(feeHeads, concessions, overdueFee);

		return new StudentFeeDetailsDto
		{
			Student     = studentSummary,
			FeeOverview = feeOverview,
			FeeHeads    = feeHeads,
			Charges     = charges,
			Payments    = payments,
			Concessions = concessions,
		};
	}

	// ── Private query helpers ─────────────────────────────────────────────────────

	/// <summary>Returns all due fees for a student, ordered by year then month.</summary>
	private async Task<List<DueFee>> GetDueFeesAsync(Guid studentId)
	{
		var dueFeesQ = await dueFeeRepository.GetQueryableAsync();
		return dueFeesQ
			.Where(d => d.StudentId == studentId)
			.ToList();
	}

	/// <summary>
	/// Returns a queryable of <c>FeeRecordMetadata</c> IDs whose status is
	/// <c>Reversed</c>. Used as a subquery filter to exclude reversed transactions
	/// from paid-amount calculations.
	/// </summary>
	private async Task<IQueryable<Guid>> GetReversedTransactionIdsAsync()
	{
		var metaQ = await feeRecordMetadataRepository.GetQueryableAsync();
		return metaQ
			.Where(m => m.Status == FeeCollectionStatus.Reversed)
			.Select(m => m.Id);
	}

	/// <summary>
	/// Builds per-fee-head payment summaries for the student.
	/// Returns an empty list when no fee-structure assignment or due fees exist.
	/// </summary>
	private async Task<List<FeeHeadSummaryDto>> BuildFeeHeadSummariesAsync(
		Guid studentId, StudentFeeStructure? assignment, List<DueFee> dueFees)
	{
		if (assignment is null || dueFees.Count == 0)
			return [];

		var detailsQ  = await feeStructureDetailRepository.GetQueryableAsync();
		var feeHeadsQ = await feeHeadRepository.GetQueryableAsync();

		var dueFeeHeadIds = dueFees
			.Select(d => d.FeeHeadId)
			.Distinct()
			.ToList();

		var feeHeadDefs = (from detail in detailsQ
						   join fh in feeHeadsQ on detail.FeeHeadId equals fh.Id
						   where detail.FeeStructureId == assignment.FeeStructureStandardId
								 && dueFeeHeadIds.Contains(detail.FeeHeadId)
						   select new
						   {
							   detail.FeeHeadId,
							   FeeHeadName = fh.Name,
							   Description = fh.Description ?? string.Empty,
							   NetAmount   = detail.Amount,
						   }).ToList();

		var feeCollQ      = await feeCollectionRecordRepository.GetQueryableAsync();
		var reversedTxIds = await GetReversedTransactionIdsAsync();

		var paidPerHead = feeCollQ
			.Where(r => r.StudentId == studentId
					 && !reversedTxIds.Contains(r.FeeRecordMetadataId))
			.GroupBy(r => r.FeeHeadId)
			.Select(g => new { FeeHeadId = g.Key, Paid = g.Sum(r => r.Amount) })
			.ToDictionary(x => x.FeeHeadId, x => x.Paid);

		// ── Reconciliation amounts per fee head ───────────────────────────────
		var reconByFeeHead = await GetInReconciliationByFeeHeadAsync(studentId);

		return feeHeadDefs.Select(def =>
		{
			var paid    = paidPerHead.GetValueOrDefault(def.FeeHeadId, 0d);
			var pending = Math.Max(0d, def.NetAmount - paid);
			var recon   = reconByFeeHead.GetValueOrDefault(def.FeeHeadId, 0d);
			return new FeeHeadSummaryDto
			{
				FeeHeadId            = def.FeeHeadId,
				FeeHeadName          = def.FeeHeadName,
				Amount               = def.NetAmount,
				PaidAmount           = paid,
				PendingAmount        = pending,
				Description          = def.Description,
				ReconciliationAmount = recon > 0d ? recon : null,
				Status               = pending <= 0d
					? FeeCollectionStatus.Paid
					: (paid > 0d ? FeeCollectionStatus.Partial : FeeCollectionStatus.Pending),
			};
		}).ToList();
	}

	/// <summary>
	/// Builds charge-level payment details for the student in the given academic year.
	/// Returns <c>null</c> when the student has no approved charges.
	/// </summary>
	private async Task<List<FeeChargeDetailDto>?> BuildChargeDetailsAsync(Guid studentId, Guid academicYearId)
	{
		var chargesQ = await chargeRepository.GetQueryableAsync();
		var studentCharges = chargesQ
			.Where(c => c.StudentId == studentId
					 && c.AcademicYearId == academicYearId
					 && c.Status == ChargeStatus.Approved)
			.ToList();

		if (studentCharges.Count == 0)
			return null;

		var chargeCollQ   = await chargeCollectionRecordRepository.GetQueryableAsync();
		var reversedTxIds = await GetReversedTransactionIdsAsync();

		var paidPerCharge = chargeCollQ
			.Where(r => r.StudentId == studentId
					 && !reversedTxIds.Contains(r.FeeRecordMetadataId))
			.GroupBy(r => r.ChargeId)
			.Select(g => new { ChargeId = g.Key, Paid = g.Sum(r => r.Amount) })
			.ToDictionary(x => x.ChargeId, x => x.Paid);

		return studentCharges.Select(c =>
		{
			var net     = (double)c.Amount;
			var paid    = paidPerCharge.GetValueOrDefault(c.Id, 0d);
			var pending = Math.Max(0d, net - paid);
			return new FeeChargeDetailDto
			{
				ChargeId      = c.Id,
				Name          = string.IsNullOrWhiteSpace(c.ChargeName) ? c.PurposeDescription : c.ChargeName,
				Amount        = net,
				PaidAmount    = paid,
				PendingAmount = pending,
				Status        = pending <= 0d
					? FeeCollectionStatus.Paid
					: (paid > 0d ? FeeCollectionStatus.Partial : FeeCollectionStatus.Pending),
			};
		}).ToList();
	}

	/// <summary>
	/// Builds the payment transaction history for the student in the given academic year,
	/// enriched with payment methods per receipt. Returns <c>null</c> when no payments exist.
	/// </summary>
	private async Task<List<FeePaymentHistoryDto>?> BuildPaymentHistoryAsync(Guid studentId, Guid academicYearId)
	{
		var metaQ = await feeRecordMetadataRepository.GetQueryableAsync();

		var metadataForStudent = metaQ
			.Where(m => m.StudentId == studentId && m.AcademicYearId == academicYearId)
			.OrderByDescending(m => m.CreationTime)
			.ToList();

		if (metadataForStudent.Count == 0)
			return null;

		var paymentMethodQ = await paymentMethodRecordRepository.GetQueryableAsync();
		var metadataIds    = metadataForStudent.Select(m => m.Id).ToList();

		var methodsByMetadata = paymentMethodQ
			.Where(p => metadataIds.Contains(p.FeeRecordMetadataId))
			.GroupBy(p => p.FeeRecordMetadataId)
			.Select(g => new { MetadataId = g.Key, PaymentMethods = g.Select(p => p.PaymentMethod).ToList() })
			.ToDictionary(x => x.MetadataId, x => x.PaymentMethods);

		return metadataForStudent.Select(m => new FeePaymentHistoryDto
		{
			PaymentId      = m.Id,
			ReceiptNumber  = m.ReceiptNumber,
			Date           = DateOnly.FromDateTime(m.CreationTime.Date),
			Amount         = m.TotalPaidFee,
			PaymentMethods = methodsByMetadata.TryGetValue(m.Id, out var methods) ? methods : [],
			ReceiptUrl     = null,
			Status         = m.Status,
		}).ToList();
	}

	/// <summary>
	/// Builds applied-concession details for the student.
	/// Returns <c>null</c> when no approved concessions exist.
	/// </summary>
	private async Task<List<ConcessionDetailDto>?> BuildConcessionDetailsAsync(Guid studentId)
	{
		var approvedConcessions = await concessionRequestRepository.GetListAsync(
			r => r.StudentId == studentId && r.Status == ConcessionRequestStatus.Approved);

		if (approvedConcessions.Count == 0)
			return null;

		var typeIds        = approvedConcessions.Select(r => r.ConcessionTypeId).Distinct().ToList();
		var concessionTypes = await concessionTypeRepository.GetListAsync(ct => typeIds.Contains(ct.Id));
		var typeMap        = concessionTypes.ToDictionary(ct => ct.Id, ct => ct.Name);

		return approvedConcessions.Select(r =>
		{
			var total = r.ConcessionFeeHeadMappings.Sum(m => (double)m.FeeHeadConcessionAmount);
			return new ConcessionDetailDto
			{
				ConcessionRequestConcessionTypeId = r.Id,
				ConcessionTypeName                = typeMap.GetValueOrDefault(r.ConcessionTypeId, string.Empty),
				Amount                            = total > 0d ? total : null,
			};
		}).ToList();
	}

	/// <summary>
	/// Returns the total outstanding amount for fee periods that have already passed
	/// the current month. Uses <c>MonthEnum</c> (0-indexed) to align with <c>DateTime.Month</c>
	/// (1-indexed), so <c>currentMonth = now.Month - 1</c>.
	/// </summary>
	private double CalculateOverdueFee(List<DueFee> dueFees)
	{
		var now          = clock.Now;
		var currentYear  = now.Year;
		var currentMonth = now.Month - 1; // MonthEnum is 0-indexed (January = 0)

		return dueFees
			.Where(d => d.OutstandingAmount > 0d
					 && (d.Year < currentYear
					 || (d.Year == currentYear && (int)d.Month < currentMonth)))
			.Sum(d => d.OutstandingAmount);
	}

	/// <summary>
	/// Aggregates fee-head summaries, concessions, and overdue amount into the
	/// top-level financial overview. Pure calculation — no repository access.
	/// </summary>
	private static FeeOverviewDto BuildFeeOverview(
		List<FeeHeadSummaryDto> feeHeads, List<ConcessionDetailDto>? concessions, double overdueFee)
	{
		var totalConcession = concessions?.Sum(c => c.Amount ?? 0d) ?? 0d;
		var totalRecon      = feeHeads.Sum(fh => fh.ReconciliationAmount ?? 0d);

		return new FeeOverviewDto
		{
			TotalFee             = feeHeads.Sum(fh => fh.Amount),
			PaidFee              = feeHeads.Sum(fh => fh.PaidAmount),
			PendingFee           = feeHeads.Sum(fh => fh.PendingAmount ?? 0d),
			ConcessionAmount     = totalConcession > 0d ? totalConcession : null,
			OverdueFee           = overdueFee > 0d ? overdueFee : null,
			ReconciliationAmount = totalRecon > 0d ? totalRecon : null,
		};
	}

	private async Task<StudentSnapshot> GetStudentSnapshotAsync(Guid studentId, CancellationToken cancellationToken)
	{
		var student = await studentRepository.FindAsync(studentId) ?? throw new BusinessException("Student not found");

		return new StudentSnapshot
		{
			Id = student.Id,
			FirstName = student.FirstName,
			MiddleName = student.MiddleName,
			LastName = student.LastName,
			AcademicBoardId = student.AcademicBoardId,
			BoardType = student.AcademicBoard?.Type,
			AcademicMediumId = student.AcademicMediumId,
			EducationMediumName = student.AcademicMedium?.Name ?? string.Empty,
			StandardId = student.StandardId,
			StandardName = student.Standard?.Name ?? string.Empty,
			DivisionId = student.DivisionId,
			DivisionName = student.Division?.Name ?? string.Empty,
			GrNo = student.GrNo,
			RollNo = student.RollNo,
			ContactNo = student.ContactDetail?.PrimaryContactNo ?? string.Empty,
			Email = student.ContactDetail?.Email ?? string.Empty,
			FeeStructureId = student.FeeStructureId,
		};
	}

	private static StudentInfoDto BuildStudentInfo(StudentSnapshot student) => new()
	{
		StudentId = student.Id,
		StudentName = BuildFullName(student.FirstName, student.MiddleName, student.LastName),
		GrNo = student.GrNo?.ToString() ?? string.Empty,
		RollNo = student.RollNo?.ToString() ?? string.Empty,
		BoardId = student.AcademicBoardId ?? Guid.Empty,
		BoardName = student.BoardType?.ToString() ?? string.Empty,
		EducationMediumId = student.AcademicMediumId,
		EducationMediumName = student.EducationMediumName,
		StandardId = student.StandardId ?? Guid.Empty,
		StandardName = student.StandardName,
		DivisionId = student.DivisionId ?? Guid.Empty,
		DivisionName = student.DivisionName,
		ContactNo = student.ContactNo,
		Email = student.Email,
		FeeStructureName = string.Empty,
		IsFeeStructureAssigned = student.FeeStructureId.HasValue,
	};

	private static StudentSummaryDto BuildStudentSummary(StudentSnapshot student) => new()
	{
		Id = student.Id,
		Name = BuildFullName(student.FirstName, student.MiddleName, student.LastName),
		StandardName = student.StandardName,
		DivisionName = student.DivisionName,
		RollNo = student.RollNo?.ToString() ?? string.Empty,
		MobileNo = string.IsNullOrWhiteSpace(student.ContactNo) ? null : student.ContactNo,
	};

	private static string BuildFullName(string? first, string? middle, string? last)
	{
		var parts = new[] { first, middle, last };
		return string.Join(" ", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
	}

	// ── Reconciliation helpers ────────────────────────────────────────────────────

	/// <summary>
	/// Returns a map of DueFeeId → sum of amounts that are currently
	/// <c>InReconciliation</c> (cheque/DD submitted but not yet cleared).
	/// Reversed transactions are excluded.
	/// </summary>
	private async Task<Dictionary<Guid, double>> GetInReconciliationByDueFeeAsync(Guid studentId)
	{
		var metaQ    = await feeRecordMetadataRepository.GetQueryableAsync();
		var allocQ   = await dueFeeAllocationRepository.GetQueryableAsync();

		var reconMetaIds = metaQ
			.Where(m => m.StudentId == studentId && m.Status == FeeCollectionStatus.InReconciliation)
			.Select(m => m.Id);

		return allocQ
			.Where(a => reconMetaIds.Contains(a.FeeRecordMetadataId))
			.GroupBy(a => a.DueFeeId)
			.Select(g => new { DueFeeId = g.Key, Amount = g.Sum(a => a.Amount) })
			.ToDictionary(x => x.DueFeeId, x => x.Amount);
	}

	/// <summary>
	/// Returns a map of FeeHeadId → sum of amounts that are currently
	/// <c>InReconciliation</c> (cheque/DD submitted but not yet cleared).
	/// Derived via <c>FeeCollectionRecord</c> (which stores <c>FeeHeadId</c> directly).
	/// Reversed transactions are excluded.
	/// </summary>
	private async Task<Dictionary<Guid, double>> GetInReconciliationByFeeHeadAsync(Guid studentId)
	{
		var metaQ   = await feeRecordMetadataRepository.GetQueryableAsync();
		var collQ   = await feeCollectionRecordRepository.GetQueryableAsync();

		var reconMetaIds = metaQ
			.Where(m => m.StudentId == studentId && m.Status == FeeCollectionStatus.InReconciliation)
			.Select(m => m.Id);

		return collQ
			.Where(r => r.StudentId == studentId && reconMetaIds.Contains(r.FeeRecordMetadataId))
			.GroupBy(r => r.FeeHeadId)
			.Select(g => new { FeeHeadId = g.Key, Amount = g.Sum(r => r.Amount) })
			.ToDictionary(x => x.FeeHeadId, x => x.Amount);
	}

	private sealed class StudentSnapshot
	{
		public Guid Id { get; init; }
		public string? FirstName { get; init; }
		public string? MiddleName { get; init; }
		public string? LastName { get; init; }
		public Guid? AcademicBoardId { get; init; }
		public EducationBoardType? BoardType { get; init; }
		public Guid AcademicMediumId { get; init; }
		public string EducationMediumName { get; init; } = string.Empty;
		public Guid? StandardId { get; init; }
		public string StandardName { get; init; } = string.Empty;
		public Guid? DivisionId { get; init; }
		public string DivisionName { get; init; } = string.Empty;
		public int? GrNo { get; init; }
		public int? RollNo { get; init; }
		public string ContactNo { get; init; } = string.Empty;
		public string Email { get; init; } = string.Empty;
		public Guid? FeeStructureId { get; set; }
	}
}
