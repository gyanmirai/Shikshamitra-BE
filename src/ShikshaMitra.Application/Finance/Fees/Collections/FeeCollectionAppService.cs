using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ClosedXML.Excel;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Finance.Charges;
using ShikshaMitra.Finance.Fees.Charges;
using ShikshaMitra.Finance.FeeCollection;
using ShikshaMitra.Finance.Fees.Collections.Dtos;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.Receipts;
using ShikshaMitra.Finance.Receipts;
using ShikshaMitra.Finance.Fees.ConcessionRequests;
using ShikshaMitra.Finance.Fees.Concessions;
using ShikshaMitra.Finance.Fees.Settings.ConcessionTypes;
using ShikshaMitra.Finance.Fees.Settings.FeeStructures;
using ShikshaMitra.Finance.Audit;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment;
using ShikshaMitra.Students;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.Timing;
using Volo.Abp.Validation;

namespace ShikshaMitra.Finance.Fees.Collections;

[Authorize(ShikshaMitraSubModulePermissions.FeeCollection)]
[RemoteService(false)]
public class FeeCollectionAppService(
	IReadOnlyRepository<Student, Guid> studentRepository,
	IReadOnlyRepository<EducationBoard, Guid> educationBoardRepository,
	IReadOnlyRepository<EducationMedium, Guid> educationMediumRepository,
	IReadOnlyRepository<Standard, Guid> standardRepository,
	IReadOnlyRepository<Division, Guid> divisionRepository,
	IReadOnlyRepository<StudentContactDetail, Guid> studentContactDetailRepository,
	IDueFeeRepository dueFeeRepository,
	IRepository<FeeRecordMetadata, Guid> feeRecordMetadataRepository,
	IRepository<FeeCollectionRecord, Guid> feeCollectionRecordRepository,
	IRepository<ChargeCollectionRecord, Guid> chargeCollectionRecordRepository,
	IRepository<PaymentMethodRecord, Guid> paymentMethodRecordRepository,
	IRepository<FeeHead, Guid> feeHeadRepository,
	IRepository<Charge, Guid> chargeRepository,
	IRepository<AcademicYear, Guid> academicYearRepository,
	IRepository<StudentFeeStructure, Guid> studentFeeStructureRepository,
	IRepository<FeeStructure, Guid> feeStructureRepository,
	IRepository<FeeStructureDetail, Guid> feeStructureDetailRepository,
	IRepository<FeeCategory, Guid> feeCategoryRepository,
	IRepository<ConcessionType, Guid> concessionTypeRepository,
	IConcessionRequestRepository concessionRequestRepository,
	FeeCollectionManager feeCollectionManager,
	FinanceAuditWriter financeAuditWriter,
	FeeReceiptManager feeReceiptManager,
	IFeePaymentReceiptRepository feePaymentReceiptRepository,
	IRepository<FeeCollectionDueFeeAllocation, Guid> dueFeeAllocationRepository,
	FinanceScopeAccessService financeScopeAccessService,
	IAsyncQueryableExecuter asyncExecuter,
	IClock clock
) : ApplicationService, IFeeCollectionAppService
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

	private static void EnsureSummaryContextIsValid(GetFeeCollectionSummaryInput input)
	{
		EnsureNotEmpty(input.AcademicYearId, nameof(input.AcademicYearId));
		EnsureNotEmpty(input.BoardId, nameof(input.BoardId));
		EnsureNotEmpty(input.EducationMediumId, nameof(input.EducationMediumId));
	}

	public async Task<PagedResultDto<FeeCollectionSummaryItemDto>> GetSummaryAsync(
		GetFeeCollectionSummaryInput input,
		CancellationToken cancellationToken = default)
	{
		await financeScopeAccessService.EnsureAcademicContextAccessibleAsync(
			input.AcademicYearId,
			input.BoardId,
			input.EducationMediumId,
			input.StandardId,
			input.DivisionId,
			cancellationToken);

		var allRows = await BuildRawRowsAsync(input, cancellationToken);
		var filtered = ApplyStatusFilter(allRows, input.Status);

		var totalCount = filtered.Count;
		if (totalCount == 0)
			return new PagedResultDto<FeeCollectionSummaryItemDto>(0, []);

		var page = ApplySorting(filtered, input.Sorting)
			.Skip(input.SkipCount)
			.Take(input.MaxResultCount)
			.ToList();

		return new PagedResultDto<FeeCollectionSummaryItemDto>(totalCount, MapToDto(page));
	}

	public async Task<byte[]> ExportAsync(
		FeeCollectionExportRequestDto input,
		CancellationToken cancellationToken = default)
	{
		await financeScopeAccessService.EnsureAcademicContextAccessibleAsync(
			input.AcademicYearId,
			input.BoardId,
			input.EducationMediumId,
			input.StandardId,
			input.DivisionId,
			cancellationToken);

		var filterInput = new GetFeeCollectionSummaryInput
		{
			AcademicYearId    = input.AcademicYearId,
			BoardId           = input.BoardId,
			EducationMediumId = input.EducationMediumId,
			StandardId        = input.StandardId,
			DivisionId        = input.DivisionId,
			Search            = input.Search,
			Status            = input.Status,
		};

		var allRows = await BuildRawRowsAsync(filterInput, cancellationToken);
		var filtered = ApplyStatusFilter(allRows, input.Status);
		var sorted   = ApplySorting(filtered, sorting: null).ToList();
		var rows     = MapToDto(sorted);

		return input.Format == FeeCollectionExportFormat.Excel
			? BuildExcel(rows)
			: BuildCsv(rows);
	}

	// ── Raw data fetch: separate DB queries joined in memory ──────────────
	// Using a complex multi-join IQueryable caused EF Core to emit split queries,
	// which threw "Nullable object must have a value" when materialising
	// non-nullable value-type columns from LEFT JOIN NULL rows. Separate queries
	// joined in memory is reliable, avoids EF Core translation limits, and is
	// perfectly acceptable for school-scale datasets.

	private async Task<List<FeeCollectionRawRow>> BuildRawRowsAsync(
		GetFeeCollectionSummaryInput input,
		CancellationToken cancellationToken = default)
	{
		EnsureSummaryContextIsValid(input);

		var now          = clock.Now;
		var currentMonth = now.Month - 1; // MonthEnum is 0-indexed
		var currentYear  = now.Year;

		// ── 1. Students ────────────────────────────────────────────────────
		var studentQ = await studentRepository.GetQueryableAsync();
		studentQ = studentQ.Where(s =>
			s.AcademicYearId   == input.AcademicYearId  &&
			s.AcademicBoardId  == input.BoardId          &&
			s.AcademicMediumId == input.EducationMediumId &&
			s.Status           == StudentLifeCycleStatusEnum.Enrolled);

		if (input.StandardId.HasValue)
			studentQ = studentQ.Where(s => s.StandardId == input.StandardId.Value);
		if (input.DivisionId.HasValue)
			studentQ = studentQ.Where(s => s.DivisionId == input.DivisionId.Value);
		if (!string.IsNullOrWhiteSpace(input.Search) && input.Search.Length >= 2)
		{
			var term = input.Search.Trim().ToLower();
			studentQ = studentQ.Where(s =>
				(s.FirstName  != null && s.FirstName.ToLower().Contains(term))  ||
				(s.MiddleName != null && s.MiddleName.ToLower().Contains(term)) ||
				(s.LastName   != null && s.LastName.ToLower().Contains(term))   ||
				(s.RollNo     != null && s.RollNo.ToString()!.Contains(term)));
		}

		var students = await asyncExecuter.ToListAsync(
			studentQ.Select(s => new
			{
				s.Id, s.FirstName, s.MiddleName, s.LastName,
				s.GrNo, s.RollNo, s.StandardId, s.DivisionId,
			}),
			cancellationToken);

		if (students.Count == 0)
			return [];

		var studentIds = students.Select(s => s.Id).ToList();

		// ── 2. Fee aggregates per student ──────────────────────────────────
		var dueFeeQ = await dueFeeRepository.GetQueryableAsync();

		var feeAggs = await asyncExecuter.ToListAsync(
			dueFeeQ
				.Where(d => studentIds.Contains(d.StudentId))
				.GroupBy(d => d.StudentId)
				.Select(g => new
				{
					StudentId   = g.Key,
					Total       = (double?)g.Sum(d => (double?)d.OriginalAmount) ?? 0d,
					Paid        = (double?)g.Sum(d => (double?)d.PaidAmount) ?? 0d,
					Outstanding = (double?)g.Sum(d => (double?)d.OutstandingAmount) ?? 0d,
				}),
			cancellationToken);

		var overdueAggs = await asyncExecuter.ToListAsync(
			dueFeeQ
				.Where(d => studentIds.Contains(d.StudentId)
						 && d.OutstandingAmount > 0d
						 && (d.Year < currentYear
						 || (d.Year == currentYear && (int)d.Month < currentMonth)))
				.GroupBy(d => d.StudentId)
				.Select(g => new
				{
					StudentId = g.Key,
					Overdue   = (double?)g.Sum(d => d.OutstandingAmount),
				}),
			cancellationToken);

		// ── 3. Standard / Division name lookups ────────────────────────────
		var standardIds = students
			.Where(s => s.StandardId.HasValue)
			.Select(s => s.StandardId!.Value)
			.Distinct().ToList();
		var divisionIds = students
			.Where(s => s.DivisionId.HasValue)
			.Select(s => s.DivisionId!.Value)
			.Distinct().ToList();

		var standardQ = await standardRepository.GetQueryableAsync();
		var divisionQ = await divisionRepository.GetQueryableAsync();

		var standardDict = (await asyncExecuter.ToListAsync(
			standardQ
				.Where(s => standardIds.Contains(s.Id))
				.Select(s => new { s.Id, s.Name }),
			cancellationToken))
			.ToDictionary(s => s.Id, s => s.Name);

		var divisionDict = (await asyncExecuter.ToListAsync(
			divisionQ
				.Where(d => divisionIds.Contains(d.Id))
				.Select(d => new { d.Id, d.Name }),
			cancellationToken))
			.ToDictionary(d => d.Id, d => d.Name);

		// ── 4. Charge aggregates per student ───────────────────────────────
		var chargeAggDict = await BuildChargeAggregatesByStudentAsync(
			studentIds,
			input.AcademicYearId,
			cancellationToken);

		// ── 5. Join in memory ──────────────────────────────────────────────
		var feeDict    = feeAggs.ToDictionary(fa => fa.StudentId);
		var overdueDict = overdueAggs.ToDictionary(ov => ov.StudentId);

		return students.Select(s =>
		{
			feeDict.TryGetValue(s.Id, out var fa);
			overdueDict.TryGetValue(s.Id, out var ov);
			chargeAggDict.TryGetValue(s.Id, out var ca);
			standardDict.TryGetValue(s.StandardId ?? Guid.Empty, out var standardName);
			divisionDict.TryGetValue(s.DivisionId ?? Guid.Empty, out var divisionName);
			return new FeeCollectionRawRow
			{
				StudentId       = s.Id,
				FirstName       = s.FirstName,
				MiddleName      = s.MiddleName,
				LastName        = s.LastName,
				GrNo            = s.GrNo?.ToString() ?? string.Empty,
				RollNo          = s.RollNo?.ToString() ?? string.Empty,
				StandardName    = standardName ?? string.Empty,
				DivisionName    = divisionName ?? string.Empty,
				TotalFees       = fa?.Total ?? 0d,
				PaidFees        = fa?.Paid ?? 0d,
				OutstandingFees = fa?.Outstanding ?? 0d,
				OverdueFees     = ov?.Overdue ?? 0d,
				TotalCharges    = ca.TotalCharges,
				PendingCharges  = ca.PendingCharges,
			};
		}).ToList();
	}

	/// <summary>
	/// Approved charge totals and outstanding balances per student for an academic year.
	/// Excludes reversed fee transactions from collected charge amounts (same as collect-form).
	/// </summary>
	private async Task<Dictionary<Guid, StudentChargeAggregate>> BuildChargeAggregatesByStudentAsync(
		List<Guid> studentIds,
		Guid academicYearId,
		CancellationToken cancellationToken = default)
	{
		var result = studentIds.ToDictionary(id => id, _ => StudentChargeAggregate.Empty);

		if (studentIds.Count == 0)
			return result;

		var chargeQ = await chargeRepository.GetQueryableAsync();
		var approvedCharges = await asyncExecuter.ToListAsync(
			chargeQ
				.Where(c =>
					studentIds.Contains(c.StudentId) &&
					c.AcademicYearId == academicYearId &&
					c.Status == ChargeStatus.Approved)
				.Select(c => new { c.Id, c.StudentId, Amount = (double)c.Amount }),
			cancellationToken);

		if (approvedCharges.Count == 0)
			return result;

		var chargeIds = approvedCharges.Select(c => c.Id).Distinct().ToList();

		var metaQ = await feeRecordMetadataRepository.GetQueryableAsync();
		var reversedTxIds = await asyncExecuter.ToListAsync(
			metaQ
				.Where(m => m.Status == FeeCollectionStatus.Reversed)
				.Select(m => m.Id),
			cancellationToken);

		var chargeCollQ = await chargeCollectionRecordRepository.GetQueryableAsync();
		var paidPerCharge = await asyncExecuter.ToListAsync(
			chargeCollQ
				.Where(r =>
					studentIds.Contains(r.StudentId) &&
					chargeIds.Contains(r.ChargeId) &&
					!reversedTxIds.Contains(r.FeeRecordMetadataId))
				.GroupBy(r => r.ChargeId)
				.Select(g => new { ChargeId = g.Key, Paid = g.Sum(r => r.Amount) }),
			cancellationToken);

		var paidDict = paidPerCharge.ToDictionary(x => x.ChargeId, x => x.Paid);

		foreach (var group in approvedCharges.GroupBy(c => c.StudentId))
		{
			var total = 0d;
			var pending = 0d;
			foreach (var charge in group)
			{
				total += charge.Amount;
				pending += Math.Max(0d, charge.Amount - paidDict.GetValueOrDefault(charge.Id, 0d));
			}

			result[group.Key] = new StudentChargeAggregate(total, pending);
		}

		return result;
	}

	private readonly record struct StudentChargeAggregate(double TotalCharges, double PendingCharges)
	{
		public static StudentChargeAggregate Empty => new(0d, 0d);
	}

	// ── In-memory mapping (string concatenation, status derivation) ───────

	private static List<FeeCollectionSummaryItemDto> MapToDto(List<FeeCollectionRawRow> rows)
		=> rows.ConvertAll(r =>
		{
			var outstanding = r.OutstandingFees;
			var overdue     = r.OverdueFees;

			return new FeeCollectionSummaryItemDto
			{
				StudentId        = r.StudentId,
				StudentName      = BuildFullName(r.FirstName, r.MiddleName, r.LastName),
				GrNumber         = r.GrNo,
				RollNumber       = r.RollNo,
				StandardDivision = string.IsNullOrEmpty(r.DivisionName)
					? r.StandardName
					: $"{r.StandardName} - {r.DivisionName}",
				TotalFees   = r.TotalFees ?? 0d,
				PaidFees    = r.PaidFees ?? 0d,
				PendingFees = outstanding ?? 0d,
				OverdueFees = overdue ?? 0d,
				TotalCharges = r.TotalCharges ?? 0d,
				PendingCharges = r.PendingCharges ?? 0d,
				// Status:
				//   Paid    → nothing outstanding
				//   Overdue → has overdue balance
				//   Pending → has balance but none overdue yet
				Status = (outstanding ?? 0d) <= 0d
					? FeeCollectionStatus.Paid
					: (overdue ?? 0d) > 0d
						? FeeCollectionStatus.Overdue
						: FeeCollectionStatus.Pending,
			};
		});

	private static string BuildFullName(string? first, string? middle, string? last)
	{
		var parts = new[] { first, middle, last };
		return string.Join(" ", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
	}

	// ── Filters ───────────────────────────────────────────────────────────

	private static List<FeeCollectionRawRow> ApplyStatusFilter(
		List<FeeCollectionRawRow> rows,
		FeeCollectionStatus? status)
	{
		if (!status.HasValue)
			return rows;

		return status.Value switch
		{
			FeeCollectionStatus.Paid    => rows.Where(x => (x.OutstandingFees ?? 0d) <= 0d).ToList(),
			FeeCollectionStatus.Overdue => rows.Where(x => (x.OutstandingFees ?? 0d) > 0d && (x.OverdueFees ?? 0d) > 0d).ToList(),
			FeeCollectionStatus.Pending => rows.Where(x => (x.OutstandingFees ?? 0d) > 0d && (x.OverdueFees ?? 0d) <= 0d).ToList(),
			_                           => rows,
		};
	}

	// ── Sorting ───────────────────────────────────────────────────────────

	private static IOrderedEnumerable<FeeCollectionRawRow> ApplySorting(
		List<FeeCollectionRawRow> rows,
		string? sorting)
	{
		var sort = (sorting ?? string.Empty).Trim().ToLower();

		return sort switch
		{
			"studentname asc"      or "studentname"       => rows.OrderBy(x => x.LastName).ThenBy(x => x.FirstName),
			"studentname desc"                            => rows.OrderByDescending(x => x.LastName).ThenByDescending(x => x.FirstName),
			"rollnumber asc"       or "rollnumber"        => rows.OrderBy(x => x.RollNo),
			"rollnumber desc"                             => rows.OrderByDescending(x => x.RollNo),
			"grnumber asc"         or "grnumber"          => rows.OrderBy(x => x.GrNo),
			"grnumber desc"                               => rows.OrderByDescending(x => x.GrNo),
			"totalfees asc"        or "totalfees"         => rows.OrderBy(x => x.TotalFees),
			"totalfees desc"                              => rows.OrderByDescending(x => x.TotalFees),
			"paidfees asc"         or "paidfees"          => rows.OrderBy(x => x.PaidFees),
			"paidfees desc"                               => rows.OrderByDescending(x => x.PaidFees),
			"pendingfees asc"      or "pendingfees"       => rows.OrderBy(x => x.OutstandingFees),
			"pendingfees desc"                            => rows.OrderByDescending(x => x.OutstandingFees),
			"overduefees asc"      or "overduefees"       => rows.OrderBy(x => x.OverdueFees),
			"overduefees desc"                            => rows.OrderByDescending(x => x.OverdueFees),
			"totalcharges asc"     or "totalcharges"      => rows.OrderBy(x => x.TotalCharges),
			"totalcharges desc"                           => rows.OrderByDescending(x => x.TotalCharges),
			"pendingcharges asc"   or "pendingcharges"    => rows.OrderBy(x => x.PendingCharges),
			"pendingcharges desc"                         => rows.OrderByDescending(x => x.PendingCharges),
			"standarddivision asc" or "standarddivision"  => rows.OrderBy(x => x.StandardName).ThenBy(x => x.DivisionName),
			"standarddivision desc"                       => rows.OrderByDescending(x => x.StandardName).ThenByDescending(x => x.DivisionName),
			_                                             => rows.OrderBy(x => x.LastName).ThenBy(x => x.FirstName),
		};
	}

	// ── Export helpers ────────────────────────────────────────────────────

	private static byte[] BuildCsv(List<FeeCollectionSummaryItemDto> rows)
	{
		var config = new CsvConfiguration(System.Globalization.CultureInfo.InvariantCulture)
		{
			HasHeaderRecord = true,
		};

		using var ms     = new MemoryStream();
		using var writer = new StreamWriter(ms, leaveOpen: true);
		using var csv    = new CsvWriter(writer, config);

		csv.WriteField("Student Name");
		csv.WriteField("GR Number");
		csv.WriteField("Roll Number");
		csv.WriteField("Class / Division");
		csv.WriteField("Total Fees");
		csv.WriteField("Paid Fees");
		csv.WriteField("Pending Fees");
		csv.WriteField("Overdue Fees");
		csv.WriteField("Total Charges");
		csv.WriteField("Pending Charges");
		csv.WriteField("Status");
		csv.NextRecord();

		foreach (var r in rows)
		{
			csv.WriteField(r.StudentName);
			csv.WriteField(r.GrNumber);
			csv.WriteField(r.RollNumber);
			csv.WriteField(r.StandardDivision);
			csv.WriteField(r.TotalFees);
			csv.WriteField(r.PaidFees);
			csv.WriteField(r.PendingFees);
			csv.WriteField(r.OverdueFees);
			csv.WriteField(r.TotalCharges);
			csv.WriteField(r.PendingCharges);
			csv.WriteField(r.Status.ToString());
			csv.NextRecord();
		}

		writer.Flush();
		return ms.ToArray();
	}

	private static byte[] BuildExcel(List<FeeCollectionSummaryItemDto> rows)
	{
		using var workbook = new XLWorkbook();
		var sheet = workbook.Worksheets.Add("Fee Collection");

		var headers = new[]
		{
			"Student Name", "GR Number", "Roll Number", "Class / Division",
			"Total Fees", "Paid Fees", "Pending Fees", "Overdue Fees",
			"Total Charges", "Pending Charges", "Status",
		};

		for (var col = 0; col < headers.Length; col++)
		{
			var cell = sheet.Cell(1, col + 1);
			cell.Value = headers[col];
			cell.Style.Font.Bold = true;
			cell.Style.Fill.BackgroundColor = XLColor.LightBlue;
		}

		for (var i = 0; i < rows.Count; i++)
		{
			var r   = rows[i];
			var row = i + 2;
			sheet.Cell(row, 1).Value = r.StudentName;
			sheet.Cell(row, 2).Value = r.GrNumber;
			sheet.Cell(row, 3).Value = r.RollNumber;
			sheet.Cell(row, 4).Value = r.StandardDivision;
			sheet.Cell(row, 5).Value = r.TotalFees;
			sheet.Cell(row, 6).Value = r.PaidFees;
			sheet.Cell(row, 7).Value = r.PendingFees;
			sheet.Cell(row, 8).Value = r.OverdueFees;
			sheet.Cell(row, 9).Value = r.TotalCharges;
			sheet.Cell(row, 10).Value = r.PendingCharges;
			sheet.Cell(row, 11).Value = r.Status.ToString();

			if (r.Status == FeeCollectionStatus.Overdue)
				sheet.Row(row).Style.Fill.BackgroundColor = XLColor.LightSalmon;
		}

		sheet.Columns().AdjustToContents();

		using var ms = new MemoryStream();
		workbook.SaveAs(ms);
		return ms.ToArray();
	}

	// ── CollectFeeAsync ───────────────────────────────────────────────────

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_FeeCollections.Add)]
	public async Task<CollectFeeOutput> CollectFeeAsync(CollectFeeInput input)
	{
		// 1. Validate referenced academic year
		await academicYearRepository.GetAsync(input.AcademicYearId);

		// 1b. Load student for receipt context
		var student = await studentRepository.GetAsync(input.StudentId);
		if (student.AcademicYearId != input.AcademicYearId)
		{
			throw new BusinessException(FeeCollectionErrorCodes.StudentAcademicYearMismatch);
		}

		// 2. Validate and load referenced DueFees
		var dueFeeMap = new Dictionary<Guid, DueFee>();
		var feeHeadMap = new Dictionary<Guid, FeeHead>();
		if (input.DueFeePayments is { Count: > 0 })
		{
			var dueFeeIds = input.DueFeePayments.Select(dfp => dfp.DueFeeId).Distinct().ToList();
			var dueFees = await dueFeeRepository.GetListAsync(df => dueFeeIds.Contains(df.Id));
			if (dueFees.Count != dueFeeIds.Count)
			{
				throw new BusinessException(DueFeeErrorCodes.DueFeeNotFound);
			}

			dueFeeMap = dueFees.ToDictionary(df => df.Id);

			var feeHeadIds = input.DueFeePayments.Select(dfp => dfp.FeeHeadId).Distinct().ToList();
			var feeHeadsList = await feeHeadRepository.GetListAsync(fh => feeHeadIds.Contains(fh.Id));
			if (feeHeadsList.Count != feeHeadIds.Count)
			{
				throw new BusinessException(FeeCollectionErrorCodes.InvalidFeeHead);
			}
			feeHeadMap = feeHeadsList.ToDictionary(fh => fh.Id);

			foreach (var dfp in input.DueFeePayments)
			{
				var df = dueFeeMap[dfp.DueFeeId];
				if (df.StudentId != input.StudentId)
				{
					throw new BusinessException(FeeCollectionErrorCodes.InvalidDueFee);
				}
				if (df.FeeHeadId != dfp.FeeHeadId)
				{
					throw new BusinessException(FeeCollectionErrorCodes.InvalidFeeHead);
				}
				if (dfp.Amount > df.OutstandingAmount)
				{
					throw new BusinessException(FeeCollectionErrorCodes.AmountExceedsOutstanding)
						.WithData("DueFeeId", df.Id)
						.WithData("Outstanding", df.OutstandingAmount)
						.WithData("Amount", dfp.Amount);
				}
			}
		}

		// 3. Validate and load charges
		var chargeMap = new Dictionary<Guid, Charge>();
		var pendingChargeMap = new Dictionary<Guid, double>();
		if (input.ChargePayments is { Count: > 0 })
		{
			var chargeIds = input.ChargePayments.Select(cp => cp.ChargeId).Distinct().ToList();
			var charges = await chargeRepository.GetListAsync(c =>
				chargeIds.Contains(c.Id) && c.Status == ChargeStatus.Approved);
			if (charges.Count != chargeIds.Count)
			{
				throw new BusinessException(FeeCollectionErrorCodes.InvalidCharge);
			}

			chargeMap = charges.ToDictionary(c => c.Id);

			var chargeCollQ = await chargeCollectionRecordRepository.GetQueryableAsync();
			var chargeMetaQ = await feeRecordMetadataRepository.GetQueryableAsync();
			var reversedTxIds = chargeMetaQ
				.Where(m => m.Status == FeeCollectionStatus.Reversed)
				.Select(m => m.Id);

			var paidPerCharge = chargeCollQ
				.Where(r => r.StudentId == input.StudentId
						 && !reversedTxIds.Contains(r.FeeRecordMetadataId)
						 && chargeIds.Contains(r.ChargeId))
				.GroupBy(r => r.ChargeId)
				.Select(g => new { ChargeId = g.Key, Paid = g.Sum(r => r.Amount) })
				.ToDictionary(x => x.ChargeId, x => x.Paid);

			foreach (var cp in input.ChargePayments)
			{
				var charge = chargeMap[cp.ChargeId];
				if (charge.StudentId != input.StudentId || charge.AcademicYearId != input.AcademicYearId)
				{
					throw new BusinessException(FeeCollectionErrorCodes.InvalidCharge);
				}

				var paid = paidPerCharge.GetValueOrDefault(cp.ChargeId, 0d);
				var pending = Math.Max(0d, (double)charge.Amount - paid);
				pendingChargeMap[cp.ChargeId] = pending;

				if (cp.Amount > pending)
				{
					throw new BusinessException(FeeCollectionErrorCodes.AmountExceedsOutstanding)
						.WithData("ChargeId", charge.Id)
						.WithData("Pending", pending)
						.WithData("Amount", cp.Amount);
				}
			}
		}

		// 4. Determine collection status
		var received = input.DueFeePayments.Sum(dfp => dfp.Amount) + input.ChargePayments.Sum(cp => cp.Amount);
		var paymentMethods = input.Payments.Select(p => p.PaymentMethod).ToList();

		var isAnyPartial = false;
		foreach (var dfp in input.DueFeePayments)
		{
			var df = dueFeeMap[dfp.DueFeeId];
			if (dfp.Amount < df.OutstandingAmount)
			{
				isAnyPartial = true;
				break;
			}
		}
		if (!isAnyPartial)
		{
			foreach (var cp in input.ChargePayments)
			{
				var pending = pendingChargeMap[cp.ChargeId];
				if (cp.Amount < pending)
				{
					isAnyPartial = true;
					break;
				}
			}
		}

		var status = FeeReceiptPaymentPolicy.RequiresVerification(paymentMethods)
			? FeeCollectionStatus.InReconciliation
			: (isAnyPartial ? FeeCollectionStatus.Partial : FeeCollectionStatus.Paid);

		// 5. Generate receipt number
		var receiptNumber = await feeCollectionManager.GenerateReceiptNumberAsync();

		// 6. Create FeeRecordMetadata root
		var metadata = feeCollectionManager.CreateMetadata(
			input.StudentId, input.AcademicYearId, received, status, receiptNumber, input.Notes);
		await feeRecordMetadataRepository.InsertAsync(metadata, autoSave: false);

		// 7. Fee-head collection records (grouped by feeHeadId)
		var feeHeadGroups = input.DueFeePayments
			.GroupBy(dfp => dfp.FeeHeadId)
			.Select(g => (FeeHeadId: g.Key, Amount: g.Sum(dfp => dfp.Amount)))
			.ToList();
		var feeCollectionRecords = feeCollectionManager.CreateFeeCollectionRecords(
			input.StudentId,
			metadata.Id,
			feeHeadGroups);
		await feeCollectionRecordRepository.InsertManyAsync(feeCollectionRecords, autoSave: false);

		// 8. Charge collection records
		var chargeCollectionRecords = new List<ChargeCollectionRecord>();
		if (input.ChargePayments is { Count: > 0 })
		{
			chargeCollectionRecords = feeCollectionManager.CreateChargeCollectionRecords(
				input.StudentId,
				metadata.Id,
				input.ChargePayments.Select(cp => (cp.ChargeId, cp.Amount)));
			await chargeCollectionRecordRepository.InsertManyAsync(chargeCollectionRecords, autoSave: false);
		}

		// 9. Payment-method records
		var paymentRecords = feeCollectionManager.CreatePaymentRecords(
			input.StudentId,
			metadata.Id,
			input.Payments.Select(p => (
				p.PaymentMethod,
				p.Amount,
				FeeCollectionManager.MapPaymentDetails(p.PaymentMethod, p.Details))),
			input.Notes);
		await paymentMethodRecordRepository.InsertManyAsync(paymentRecords, autoSave: false);

		// 10. Apply DueFee allocations + persist
		var allAllocations = input.DueFeePayments
			.Select(dfp => (dfp.DueFeeId, dfp.Amount))
			.ToList();

		var dueFeeBeforeSnapshots = input.DueFeePayments
			.Select(dfp =>
			{
				var df = dueFeeMap[dfp.DueFeeId];
				return (dfp.DueFeeId, df.OutstandingAmount, df.PaidAmount, df.Status);
			})
			.ToDictionary(x => x.DueFeeId, x => (x.Item2, x.Item3, x.Status));

		if (allAllocations.Count > 0)
		{
			var dueFees = await feeCollectionManager.ApplyDueFeeAllocationsAsync(allAllocations);
			await dueFeeRepository.UpdateManyAsync(dueFees, autoSave: false);

			var allocationRows = allAllocations
				.Select(a => new FeeCollectionDueFeeAllocation(
					GuidGenerator.Create(),
					CurrentTenant.Id,
					metadata.Id,
					a.DueFeeId,
					a.Amount))
				.ToList();
			await dueFeeAllocationRepository.InsertManyAsync(allocationRows, autoSave: false);
		}

		// 11. Create provisional/final receipt(s) for this transaction
		var feeLinesTotal = feeCollectionRecords.Sum(r => r.Amount);
		var chargeLinesTotal = chargeCollectionRecords.Sum(r => r.Amount);

		var receipts = await feeReceiptManager.CreateReceiptsForTransactionAsync(
			feeCollectionTransactionId: metadata.Id,
			studentId: input.StudentId,
			academicYearId: input.AcademicYearId,
			boardId: student.AcademicBoardId ?? Guid.Empty,
			educationMediumId: student.AcademicMediumId,
			feeLinesTotal: feeLinesTotal,
			chargeLinesTotal: chargeLinesTotal,
			paymentMethods: paymentMethods,
			bankReceiptConfirmed: input.PaymentConfirmed,
			remarks: input.Notes);

		if (receipts.Count > 0)
			await feePaymentReceiptRepository.InsertManyAsync(receipts, autoSave: false);

		var correlationId = $"fc-{metadata.Id:N}";
		var studentContext = new FinanceAuditStudentContext
		{
			StudentId = input.StudentId,
			StudentName = BuildFullName(student.FirstName, student.MiddleName, student.LastName),
			GrNo = student.GrNo?.ToString(),
			RollNo = student.RollNo?.ToString()
		};

		var allocationLines = new List<FinanceAuditAllocationLineDto>();
		var paymentChangedFields = new List<FinanceAuditChangedFieldDto>
		{
			new() { Field = "totalAmount", OldValue = null, NewValue = received.ToString("F2") },
			new() { Field = "status", OldValue = null, NewValue = status.ToString() }
		};

		foreach (var dfp in input.DueFeePayments)
		{
			var df = dueFeeMap[dfp.DueFeeId];
			var before = dueFeeBeforeSnapshots[dfp.DueFeeId];
			var pendingAfter = Math.Max(0, before.Item1 - dfp.Amount);
			var feeHeadName = feeHeadMap[dfp.FeeHeadId].Name;
			var installmentLabel = $"{df.Month} {df.Year}";
			allocationLines.Add(new FinanceAuditAllocationLineDto
			{
				DueFeeId = dfp.DueFeeId,
				FeeHeadId = dfp.FeeHeadId,
				FeeHeadName = feeHeadName,
				InstallmentLabel = installmentLabel,
				AmountPaid = dfp.Amount,
				PendingBefore = before.Item1,
				PendingAfter = pendingAfter
			});
			paymentChangedFields.Add(new FinanceAuditChangedFieldDto
			{
				Field = $"dueFee_{dfp.DueFeeId}_pending",
				OldValue = before.Item1.ToString("F2"),
				NewValue = pendingAfter.ToString("F2")
			});
		}

		var paymentSummary =
			$"Fee payment of ₹{received:N0} received ({receiptNumber})";
		var paymentWrite = await financeAuditWriter.WriteAsync(
			input.AcademicYearId,
			FinanceAuditEntityTypes.FeePayment,
			metadata.Id,
			receiptNumber,
			FinanceAuditActions.Payment,
			paymentSummary,
			studentContext,
			input.Notes,
			paymentChangedFields,
			new
			{
				paymentMethods = paymentMethods.Select(m => m.ToString()).ToList(),
				receiptNumber,
				received,
				status = status.ToString()
			},
			correlationId);

		var auditEventIds = new List<Guid> { paymentWrite.EventId };

		var feeReceipt = receipts.FirstOrDefault(r => r.ReceiptScope == FeeReceiptScope.Fee);
		if (feeReceipt != null)
		{
			var receiptWrite = await financeAuditWriter.WriteAsync(
				input.AcademicYearId,
				FinanceAuditEntityTypes.FeeReceipt,
				feeReceipt.Id,
				feeReceipt.ReceiptNumber,
				FinanceAuditActions.GenerateReceipt,
				$"Receipt {feeReceipt.ReceiptNumber} generated",
				studentContext,
				null,
				[
					new FinanceAuditChangedFieldDto
					{
						Field = "receiptNumber",
						OldValue = null,
						NewValue = feeReceipt.ReceiptNumber
					},
					new FinanceAuditChangedFieldDto
					{
						Field = "amount",
						OldValue = null,
						NewValue = feeLinesTotal.ToString("F2")
					}
				],
				new { feeReceiptId = feeReceipt.Id, feeReceiptNumber = feeReceipt.ReceiptNumber },
				correlationId,
				paymentWrite.EventId);
			auditEventIds.Add(receiptWrite.EventId);
		}

		await CurrentUnitOfWork!.SaveChangesAsync();

		Logger.LogInformation(
			"Fee collected for student {StudentId}: receipt {ReceiptNumber}, amount {Amount}, receipts created {ReceiptCount}",
			input.StudentId, receiptNumber, received, receipts.Count);

		return new CollectFeeOutput
		{
			Id = metadata.Id,
			ReceiptNumber = receiptNumber,
			FeeReceiptNumber = feeReceipt?.ReceiptNumber,
			FeeReceiptId = feeReceipt?.Id,
			StudentId = input.StudentId,
			AcademicYearId = input.AcademicYearId,
			TotalAmount = received,
			ReceivedAmount = received,
			Status = status,
			CollectedBy = CurrentUser.Id ?? Guid.Empty,
			CollectedAt = clock.Now,
			CreatedAt = clock.Now,
			ReceiptUrl = null,
			PaidDueFees = input.DueFeePayments
				.Select(dfp => new PaidDueFeeInfoDto
				{
					DueFeeId = dfp.DueFeeId,
					FeeHeadId = dfp.FeeHeadId,
					AmountPaid = dfp.Amount,
				})
				.ToList(),
			PaidFeeHeads = feeCollectionRecords
				.Select(r => new PaidFeeHeadInfoDto
				{
					FeeHeadId = r.FeeHeadId,
					FeeHeadName = feeHeadMap[r.FeeHeadId].Name,
					AmountPaid = r.Amount,
				})
				.ToList(),
			PaidCharges = chargeCollectionRecords.Count > 0
				? chargeCollectionRecords
					.Select(r => new PaidChargeInfoDto
					{
						ChargeId = r.ChargeId,
						ChargeName = string.IsNullOrWhiteSpace(chargeMap[r.ChargeId].ChargeName)
							? chargeMap[r.ChargeId].PurposeDescription
							: chargeMap[r.ChargeId].ChargeName,
						AmountPaid = r.Amount,
					})
					.ToList()
				: null,
			CorrelationId = correlationId,
			AuditEventIds = auditEventIds,
			AuditSummary = new FinanceAuditSummaryDto
			{
				Headline = status == FeeCollectionStatus.InReconciliation
					? "Payment saved as pending verification"
					: "Fee collected successfully",
				TotalAmount = received,
				ChangedFields = paymentChangedFields,
				AllocationLines = allocationLines
			}
		};
	}

	// ── GetCollectFormAsync ───────────────────────────────────────────────

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_FeeCollections.View)]
	public async Task<FeeCollectFormDto> GetCollectFormAsync(
		Guid studentId, Guid academicYearId, CancellationToken cancellationToken = default)
	{
		EnsureNotEmpty(academicYearId, "academicYearId");

		var student = await GetStudentSnapshotAsync(studentId, cancellationToken);

		// ── Student info ─────────────────────────────────────────────────
		var studentInfo = BuildStudentInfo(student);

		// ── Fee-head structure ────────────────────────────────────────────
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

			// Group by Year+Month
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
			};
		}

		// ── Approved charges ──────────────────────────────────────────────
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

			// Exclude charges from reversed transactions (e.g. rejected cheque payments)
			// so that partial-pay recalculation reflects the true unpaid amount.
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

	// ── GetStudentFeeDetailsAsync ─────────────────────────────────────────

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_FeeCollections.View)]
	public async Task<StudentFeeDetailsDto> GetStudentFeeDetailsAsync(
		Guid studentId, Guid academicYearId, CancellationToken cancellationToken = default)
	{
		EnsureNotEmpty(academicYearId, "academicYearId");

		var student = await GetStudentSnapshotAsync(studentId, cancellationToken);
		var studentSummary = BuildStudentSummary(student);

		// ── Fee-head breakdown ────────────────────────────────────────────
		var feeHeadSummaries = new List<FeeHeadSummaryDto>();

		var feeStructureAssignment = await studentFeeStructureRepository
			.FirstOrDefaultAsync(x => x.StudentId == studentId);

		// Shared queryable — used for reversed-transaction filtering and payment history.
		var metaQ = await feeRecordMetadataRepository.GetQueryableAsync();

		if (feeStructureAssignment != null)
		{
			var detailsQ = await feeStructureDetailRepository.GetQueryableAsync();
			var feeHeadsQ = await feeHeadRepository.GetQueryableAsync();

			var feeHeadDefs = (from detail in detailsQ
							   join fh in feeHeadsQ on detail.FeeHeadId equals fh.Id
							   where detail.FeeStructureId == feeStructureAssignment.FeeStructureStandardId
							   select new
							   {
								   detail.FeeHeadId,
								   FeeHeadName = fh.Name,
								   Description = fh.Description ?? string.Empty,
								   NetAmount = detail.Amount,
							   }).ToList();

			var feeCollQ  = await feeCollectionRecordRepository.GetQueryableAsync();

			// Exclude fee records belonging to reversed (rejected) transactions so that
			// a bounced cheque / rejected reconciliation does not inflate paid amounts.
			var reversedTxIds = metaQ
				.Where(m => m.Status == FeeCollectionStatus.Reversed)
				.Select(m => m.Id);

			var paidPerHead = feeCollQ
				.Where(r => r.StudentId == studentId
						 && !reversedTxIds.Contains(r.FeeRecordMetadataId))
				.GroupBy(r => r.FeeHeadId)
				.Select(g => new { FeeHeadId = g.Key, Paid = g.Sum(r => r.Amount) })
				.ToDictionary(x => x.FeeHeadId, x => x.Paid);

			feeHeadSummaries = feeHeadDefs.Select(def =>
			{
				var paid = paidPerHead.GetValueOrDefault(def.FeeHeadId, 0d);
				var pending = Math.Max(0d, def.NetAmount - paid);
				return new FeeHeadSummaryDto
				{
					FeeHeadId = def.FeeHeadId,
					FeeHeadName = def.FeeHeadName,
					Amount = def.NetAmount,
					PaidAmount = paid,
					PendingAmount = pending,
					Description = def.Description,
					Status = pending <= 0d ? FeeCollectionStatus.Paid : (paid > 0d ? FeeCollectionStatus.Partial : FeeCollectionStatus.Pending),
				};
			}).ToList();
		}

		// ── Approved charges ──────────────────────────────────────────────
		var chargesQ = await chargeRepository.GetQueryableAsync();
		var studentCharges = chargesQ
			.Where(c => c.StudentId == studentId
					 && c.AcademicYearId == academicYearId
					 && c.Status == ChargeStatus.Approved)
			.ToList();

		List<FeeChargeDetailDto>? chargeDtos = null;
		if (studentCharges.Count > 0)
		{
			var chargeCollQ = await chargeCollectionRecordRepository.GetQueryableAsync();
			var chargeReversedTxIds = metaQ
				.Where(m => m.Status == FeeCollectionStatus.Reversed)
				.Select(m => m.Id);

			var paidPerCharge = chargeCollQ
				.Where(r => r.StudentId == studentId
						 && !chargeReversedTxIds.Contains(r.FeeRecordMetadataId))
				.GroupBy(r => r.ChargeId)
				.Select(g => new { ChargeId = g.Key, Paid = g.Sum(r => r.Amount) })
				.ToDictionary(x => x.ChargeId, x => x.Paid);

			chargeDtos = studentCharges.Select(c =>
			{
				var net = (double)c.Amount;
				var paid = paidPerCharge.GetValueOrDefault(c.Id, 0d);
				var pending = Math.Max(0d, net - paid);
				return new FeeChargeDetailDto
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

		// ── Payment history ───────────────────────────────────────────────
		var paymentMethodQ = await paymentMethodRecordRepository.GetQueryableAsync();

		var metadataForStudent = metaQ
			.Where(m => m.StudentId == studentId && m.AcademicYearId == academicYearId)
			.OrderByDescending(m => m.CreationTime)
			.ToList();

		List<FeePaymentHistoryDto>? payments = null;
		if (metadataForStudent.Count > 0)
		{
			var metadataIds = metadataForStudent.Select(m => m.Id).ToList();
			var methodsByMetadata = paymentMethodQ
				.Where(p => metadataIds.Contains(p.FeeRecordMetadataId))
				.GroupBy(p => p.FeeRecordMetadataId)
				.Select(g => new { MetadataId = g.Key, PaymentMethods = g.Select(p => p.PaymentMethod).ToList() })
				.ToDictionary(x => x.MetadataId, x => x.PaymentMethods);

			payments = metadataForStudent.Select(m => new FeePaymentHistoryDto
			{
				PaymentId = m.Id,
				ReceiptNumber = m.ReceiptNumber,
				Date = DateOnly.FromDateTime(m.CreationTime.Date),
				Amount = m.TotalPaidFee,
				PaymentMethods = methodsByMetadata.TryGetValue(m.Id, out var methods) ? methods : [],
				ReceiptUrl = null,
				Status = m.Status,
			}).ToList();
		}

		// ── Concessions ───────────────────────────────────────────────────
		var approvedConcessions = await concessionRequestRepository.GetListAsync(
			r => r.StudentId == studentId && r.Status == ConcessionRequestStatus.Approved);

		List<ConcessionDetailDto>? concessions = null;
		if (approvedConcessions.Count > 0)
		{
			var typeIds = approvedConcessions.Select(r => r.ConcessionTypeId).Distinct().ToList();
			var concessionTypes = await concessionTypeRepository.GetListAsync(ct => typeIds.Contains(ct.Id));
			var typeMap = concessionTypes.ToDictionary(ct => ct.Id, ct => ct.Name);

			concessions = approvedConcessions.Select(r =>
			{
				var total = r.ConcessionFeeHeadMappings.Sum(m => (double)m.FeeHeadConcessionAmount);
				return new ConcessionDetailDto
				{
					ConcessionRequestConcessionTypeId = r.Id,
					ConcessionTypeName = typeMap.GetValueOrDefault(r.ConcessionTypeId, string.Empty),
					Amount = total > 0d ? total : null,
				};
			}).ToList();
		}

		// ── Fee overview ──────────────────────────────────────────────────
		var totalFee = feeHeadSummaries.Sum(fh => fh.Amount);
		var totalPaid = feeHeadSummaries.Sum(fh => fh.PaidAmount);
		var totalPending = feeHeadSummaries.Sum(fh => fh.PendingAmount ?? 0d);
		var totalConcession = concessions?.Sum(c => c.Amount ?? 0d) ?? 0d;

		return new StudentFeeDetailsDto
		{
			Student = studentSummary,
			FeeOverview = new FeeOverviewDto
			{
				TotalFee = totalFee,
				PaidFee = totalPaid,
				PendingFee = totalPending,
				ConcessionAmount = totalConcession > 0d ? totalConcession : null,
			},
			FeeHeads = feeHeadSummaries,
			Charges = chargeDtos,
			Payments = payments,
			Concessions = concessions,
		};
	}

	// ── Shared private helpers ────────────────────────────────────────────

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
	}

	private async Task<StudentSnapshot> GetStudentSnapshotAsync(Guid studentId, CancellationToken cancellationToken)
	{
		var studentQ = await studentRepository.GetQueryableAsync();
		var boardQ = await educationBoardRepository.GetQueryableAsync();
		var mediumQ = await educationMediumRepository.GetQueryableAsync();
		var standardQ = await standardRepository.GetQueryableAsync();
		var divisionQ = await divisionRepository.GetQueryableAsync();
		var contactQ = await studentContactDetailRepository.GetQueryableAsync();

		var query =
			from s in studentQ
			join b in boardQ on s.AcademicBoardId equals (Guid?)b.Id into bj
			from b in bj.DefaultIfEmpty()
			join m in mediumQ on s.AcademicMediumId equals m.Id into mj
			from m in mj.DefaultIfEmpty()
			join st in standardQ on s.StandardId equals (Guid?)st.Id into stj
			from st in stj.DefaultIfEmpty()
			join d in divisionQ on s.DivisionId equals (Guid?)d.Id into dj
			from d in dj.DefaultIfEmpty()
			join c in contactQ on s.Id equals c.Id into cj
			from c in cj.DefaultIfEmpty()
			where s.Id == studentId
			select new StudentSnapshot
			{
				Id = s.Id,
				FirstName = s.FirstName,
				MiddleName = s.MiddleName,
				LastName = s.LastName,
				AcademicBoardId = s.AcademicBoardId,
				BoardType = b != null ? (EducationBoardType?)b.Type : null,
				AcademicMediumId = s.AcademicMediumId,
				EducationMediumName = m != null ? m.Name : string.Empty,
				StandardId = s.StandardId,
				StandardName = st != null ? st.Name : string.Empty,
				DivisionId = s.DivisionId,
				DivisionName = d != null ? d.Name : string.Empty,
				GrNo = s.GrNo,
				RollNo = s.RollNo,
				ContactNo = c != null ? c.PrimaryContactNo : string.Empty,
				Email = c != null ? (c.Email ?? string.Empty) : string.Empty,
			};

		var snapshot = await asyncExecuter.FirstOrDefaultAsync(query, cancellationToken);

		return snapshot ?? throw new EntityNotFoundException(typeof(Student), studentId);
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
		IsFeeStructureAssigned = false,
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

	// ── Internal raw-row type (DB-translatable, no computed strings) ──────

	private sealed class FeeCollectionRawRow
	{
		public Guid    StudentId       { get; init; }
		public string? FirstName       { get; init; }
		public string? MiddleName      { get; init; }
		public string? LastName        { get; init; }
		public string  GrNo            { get; init; } = string.Empty;
		public string  RollNo          { get; init; } = string.Empty;
		public string  StandardName    { get; init; } = string.Empty;
		public string  DivisionName    { get; init; } = string.Empty;
		public double?  TotalFees       { get; init; }
		public double?  PaidFees        { get; init; }
		public double?  OutstandingFees { get; init; }
		public double?  OverdueFees     { get; init; }
		public double?  TotalCharges    { get; init; }
		public double?  PendingCharges  { get; init; }
	}
}
