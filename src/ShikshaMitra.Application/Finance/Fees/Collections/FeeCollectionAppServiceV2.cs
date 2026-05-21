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
using ShikshaMitra.Finance.Collections;
using ShikshaMitra.Finance.ConcessionRequests;
using ShikshaMitra.Finance.FeeCollection;
using ShikshaMitra.Finance.Fees.Collections.Dtos;
using ShikshaMitra.Finance.Fees.ConcessionRequests;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.Settings.ConcessionTypes;
using ShikshaMitra.Finance.Fees.Settings.FeeStructures;
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
public class FeeCollectionAppServiceV2(
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
	IAsyncQueryableExecuter asyncExecuter,
	IClock clock
) : ApplicationService, IFeeCollectionAppServiceV2
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

	private static void EnsureSummaryContextIsValid(GetFeeCollectionSummaryInputV2 input)
	{
		EnsureNotEmpty(input.AcademicYearId, nameof(input.AcademicYearId));
		EnsureNotEmpty(input.BoardId, nameof(input.BoardId));
		EnsureNotEmpty(input.EducationMediumId, nameof(input.EducationMediumId));
	}

	public async Task<PagedResultDto<FeeCollectionSummaryItemDtoV2>> GetSummaryAsync(
		GetFeeCollectionSummaryInputV2 input,
		CancellationToken cancellationToken = default)
	{
		var query = await BuildRawQueryAsync(input);
		query = ApplyStatusFilter(query, input.Status);

		var totalCount = await asyncExecuter.CountAsync(query, cancellationToken);
		if (totalCount == 0)
			return new PagedResultDto<FeeCollectionSummaryItemDtoV2>(0, []);

			query = ApplySorting(query, input.Sorting);

		var rawItems = await asyncExecuter.ToListAsync(
			query.Skip(input.SkipCount).Take(input.MaxResultCount),
			cancellationToken);

		return new PagedResultDto<FeeCollectionSummaryItemDtoV2>(totalCount, MapToDto(rawItems));
	}

	public async Task<byte[]> ExportAsync(
		FeeCollectionExportRequestDto input,
		CancellationToken cancellationToken = default)
	{
		var filterInput = new GetFeeCollectionSummaryInputV2
		{
			AcademicYearId    = input.AcademicYearId,
			BoardId           = input.BoardId,
			EducationMediumId = input.EducationMediumId,
			StandardId        = input.StandardId,
			DivisionId        = input.DivisionId,
			Search            = input.Search,
			Status            = input.Status,
		};

		var query = await BuildRawQueryAsync(filterInput);
		query = ApplyStatusFilter(query, input.Status);
		query = ApplySorting(query, sorting: null);

		var rawRows = await asyncExecuter.ToListAsync(query, cancellationToken);
		var rows = MapToDto(rawRows);

		return input.Format == FeeCollectionExportFormat.Excel
			? BuildExcel(rows)
			: BuildCsv(rows);
	}

	// ── Raw query (returns anonymous-projected queryable for SQL translation) ─

	private async Task<IQueryable<FeeCollectionRawRow>> BuildRawQueryAsync(
		GetFeeCollectionSummaryInputV2 input)
	{
		EnsureSummaryContextIsValid(input);

		// MonthEnum is 0-indexed (January = 0, December = 11).
		// An installment is "overdue" when its Month/Year is strictly before
		// the current calendar month — i.e. the student should have paid by now.
		var now          = clock.Now;
		var currentMonth = now.Month - 1; // convert to 0-based MonthEnum
		var currentYear  = now.Year;

		// ── Base student query ─────────────────────────────────────────────
		var studentQ = await studentRepository.GetQueryableAsync();

		studentQ = studentQ
			.Where(s => s.AcademicYearId   == input.AcademicYearId
					 && s.AcademicBoardId  == input.BoardId
					 && s.AcademicMediumId == input.EducationMediumId
					 && s.Status           == StudentLifeCycleStatusEnum.Enrolled);

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

		// ── DueFee queryable ───────────────────────────────────────────────
		var dueFeeQ = await dueFeeRepository.GetQueryableAsync();

		// ── Subquery A: total / paid / outstanding per student ─────────────
		var feeAggPerStudent = dueFeeQ
			.GroupBy(d => d.StudentId)
			.Select(g => new
			{
				StudentId   = g.Key,
				Total       = g.Sum(d => (double?)d.OriginalAmount) ?? 0d,
				Paid        = g.Sum(d => (double?)d.PaidAmount) ?? 0d,
				Outstanding = g.Sum(d => (double?)d.OutstandingAmount) ?? 0d,
			});

		// ── Subquery B: overdue outstanding per student ────────────────────
		var overduePerStudent = dueFeeQ
			.Where(d => d.OutstandingAmount > 0d
					 && (d.Year < currentYear
					 || (d.Year == currentYear && (int)d.Month < currentMonth)))
			.GroupBy(d => d.StudentId)
			.Select(g => new
			{
				StudentId = g.Key,
				Overdue   = (double?)g.Sum(d => d.OutstandingAmount),
			});

		// ── Raw projection (only scalar/DB-translatable fields) ────────────
		var query = studentQ
			.GroupJoin(feeAggPerStudent, s => s.Id, fa => fa.StudentId, (s, faj) => new { s, faj })
			.SelectMany(x => x.faj.DefaultIfEmpty(), (x, fa) => new { x.s, fa })
			.GroupJoin(overduePerStudent, x => x.s.Id, ov => ov.StudentId, (x, ovj) => new { x.s, x.fa, ovj })
			.SelectMany(x => x.ovj.DefaultIfEmpty(), (x, ov) => new FeeCollectionRawRow
			{
				StudentId = x.s.Id,
				FirstName = x.s.FirstName,
				MiddleName = x.s.MiddleName,
				LastName = x.s.LastName,
				GrNo = x.s.GrNo != null ? x.s.GrNo.ToString() : string.Empty,
				RollNo = x.s.RollNo != null ? x.s.RollNo.ToString() : string.Empty,
				StandardName = x.s.Standard != null ? x.s.Standard.Name : string.Empty,
				DivisionName = x.s.Division != null ? x.s.Division.Name : string.Empty,
				TotalFees = x.fa == null ? 0d : x.fa.Total,
				OutstandingFees = x.fa == null ? 0d : x.fa.Outstanding,
				OverdueFees = ov == null ? 0d : ov.Overdue,
				PaidFees = x.fa == null ? 0d : x.fa.Paid,
			});

		return query;
	}

	// ── In-memory mapping (string concatenation, status derivation) ───────

	private static List<FeeCollectionSummaryItemDtoV2> MapToDto(List<FeeCollectionRawRow> rows)
		=> rows.ConvertAll(r =>
		{
			var outstanding = r.OutstandingFees;
			var overdue     = r.OverdueFees;

			return new FeeCollectionSummaryItemDtoV2
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

	private static IQueryable<FeeCollectionRawRow> ApplyStatusFilter(
		IQueryable<FeeCollectionRawRow> query,
		FeeCollectionStatus? status)
	{
		if (!status.HasValue)
			return query;

		return status.Value switch
		{
			FeeCollectionStatus.Paid    => query.Where(x => x.OutstandingFees <= 0d),
			FeeCollectionStatus.Overdue => query.Where(x => x.OutstandingFees > 0d && x.OverdueFees > 0d),
			FeeCollectionStatus.Pending => query.Where(x => x.OutstandingFees > 0d && x.OverdueFees <= 0d),
			_                           => query,
		};
	}

	// ── Sorting ───────────────────────────────────────────────────────────

	private static IQueryable<FeeCollectionRawRow> ApplySorting(
		IQueryable<FeeCollectionRawRow> query,
		string? sorting)
	{
		var sort = (sorting ?? string.Empty).Trim().ToLower();

		return sort switch
		{
			"studentname asc"      or "studentname"       => query.OrderBy(x => x.LastName).ThenBy(x => x.FirstName),
			"studentname desc"                            => query.OrderByDescending(x => x.LastName).ThenByDescending(x => x.FirstName),
			"rollnumber asc"       or "rollnumber"        => query.OrderBy(x => x.RollNo),
			"rollnumber desc"                             => query.OrderByDescending(x => x.RollNo),
			"grnumber asc"         or "grnumber"          => query.OrderBy(x => x.GrNo),
			"grnumber desc"                               => query.OrderByDescending(x => x.GrNo),
			"totalfees asc"        or "totalfees"         => query.OrderBy(x => x.TotalFees),
			"totalfees desc"                              => query.OrderByDescending(x => x.TotalFees),
			"paidfees asc"         or "paidfees"          => query.OrderBy(x => x.PaidFees),
			"paidfees desc"                               => query.OrderByDescending(x => x.PaidFees),
			"pendingfees asc"      or "pendingfees"       => query.OrderBy(x => x.OutstandingFees),
			"pendingfees desc"                            => query.OrderByDescending(x => x.OutstandingFees),
			"overduefees asc"      or "overduefees"       => query.OrderBy(x => x.OverdueFees),
			"overduefees desc"                            => query.OrderByDescending(x => x.OverdueFees),
			"standarddivision asc" or "standarddivision"  => query.OrderBy(x => x.StandardName).ThenBy(x => x.DivisionName),
			"standarddivision desc"                       => query.OrderByDescending(x => x.StandardName).ThenByDescending(x => x.DivisionName),
			_                                             => query.OrderBy(x => x.LastName).ThenBy(x => x.FirstName),
		};
	}

	// ── Export helpers ────────────────────────────────────────────────────

	private static byte[] BuildCsv(List<FeeCollectionSummaryItemDtoV2> rows)
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
			csv.WriteField(r.Status.ToString());
			csv.NextRecord();
		}

		writer.Flush();
		return ms.ToArray();
	}

	private static byte[] BuildExcel(List<FeeCollectionSummaryItemDtoV2> rows)
	{
		using var workbook = new XLWorkbook();
		var sheet = workbook.Worksheets.Add("Fee Collection");

		var headers = new[]
		{
			"Student Name", "GR Number", "Roll Number", "Class / Division",
			"Total Fees", "Paid Fees", "Pending Fees", "Overdue Fees", "Status",
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
			sheet.Cell(row, 9).Value = r.Status.ToString();

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
		// 1. Validate referenced academic year and fee heads
		await academicYearRepository.GetAsync(input.AcademicYearId);

		var feeHeadIds = input.SelectedFeeHeads.Select(f => f.FeeHeadId).Distinct().ToList();
		var feeHeads = await feeHeadRepository.GetListAsync(fh => feeHeadIds.Contains(fh.Id));
		if (feeHeads.Count != feeHeadIds.Count)
			throw new BusinessException(FeeCollectionErrorCodes.InvalidFeeHead);

		var chargeMap = new Dictionary<Guid, Charge>();
		if (input.SelectedCharges is { Count: > 0 })
		{
			var chargeIds = input.SelectedCharges.Select(c => c.ChargeId).Distinct().ToList();
			var charges = await chargeRepository.GetListAsync(c =>
				chargeIds.Contains(c.Id) && c.Status == ChargeStatus.Approved);
			if (charges.Count != chargeIds.Count)
				throw new BusinessException(FeeCollectionErrorCodes.InvalidCharge);
			chargeMap = charges.ToDictionary(c => c.Id);
		}

		// 2. Determine collection status
		var received = input.ReceivedAmount ?? input.TotalAmount;
		var status = received >= input.TotalAmount
			? FeeCollectionStatus.Paid
			: FeeCollectionStatus.Pending;

		// 3. Generate receipt number
		var receiptNumber = await feeCollectionManager.GenerateReceiptNumberAsync();

		// 4. Create FeeRecordMetadata root
		var metadata = feeCollectionManager.CreateMetadata(
			input.StudentId, input.AcademicYearId, received, status, receiptNumber, input.Notes);
		await feeRecordMetadataRepository.InsertAsync(metadata, autoSave: false);

		// 5. Fee-head collection records
		var feeHeadMap = feeHeads.ToDictionary(fh => fh.Id);
		var feeCollectionRecords = feeCollectionManager.CreateFeeCollectionRecords(
			input.StudentId,
			metadata.Id,
			input.SelectedFeeHeads.Select(sfh => (sfh.FeeHeadId, sfh.Amount)));
		await feeCollectionRecordRepository.InsertManyAsync(feeCollectionRecords, autoSave: false);

		// 6. Charge collection records
		var chargeCollectionRecords = new List<ChargeCollectionRecord>();
		if (input.SelectedCharges is { Count: > 0 })
		{
			chargeCollectionRecords = feeCollectionManager.CreateChargeCollectionRecords(
				input.StudentId,
				metadata.Id,
				input.SelectedCharges.Select(sc => (sc.ChargeId, sc.Amount)));
			await chargeCollectionRecordRepository.InsertManyAsync(chargeCollectionRecords, autoSave: false);
		}

		// 7. Payment-method records
		var paymentRecords = feeCollectionManager.CreatePaymentRecords(
			input.StudentId,
			metadata.Id,
			input.Payments.Select(p => (
				p.PaymentMethod,
				p.Amount,
				FeeCollectionManager.MapPaymentDetails(p.PaymentMethod, p.Details))),
			input.Notes);
		await paymentMethodRecordRepository.InsertManyAsync(paymentRecords, autoSave: false);

		// 8. Apply DueFee allocations (optional — backward-compatible)
		var allAllocations = input.SelectedFeeHeads
			.Where(sfh => sfh.Allocations is { Count: > 0 })
			.SelectMany(sfh => sfh.Allocations!)
			.Select(a => (a.DueFeeId, a.Amount))
			.ToList();

		if (allAllocations.Count > 0)
		{
			var dueFees = await feeCollectionManager.ApplyDueFeeAllocationsAsync(allAllocations);
			await dueFeeRepository.UpdateManyAsync(dueFees, autoSave: true);
		}
		else
		{
			await CurrentUnitOfWork!.SaveChangesAsync();
		}

		Logger.LogInformation(
			"Fee collected for student {StudentId}: receipt {ReceiptNumber}, amount {Amount}",
			input.StudentId, receiptNumber, received);

		return new CollectFeeOutput
		{
			Id = metadata.Id,
			ReceiptNumber = receiptNumber,
			StudentId = input.StudentId,
			AcademicYearId = input.AcademicYearId,
			TotalAmount = input.TotalAmount,
			Status = status,
			CollectedBy = CurrentUser.Id ?? Guid.Empty,
			CollectedAt = clock.Now,
			CreatedAt = clock.Now,
			ReceiptUrl = null,
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
						ChargeName = chargeMap[r.ChargeId].PurposeDescription,
						AmountPaid = r.Amount,
					})
					.ToList()
				: null,
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
						: FeeCollectionStatus.Pending,
				}).ToList();

				ledger.Add(new FeeLedgerMonthDto
				{
					Month = ((int)group.Key.Month + 1).ToString("00"),
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
			var chargeCollQ = await chargeCollectionRecordRepository.GetQueryableAsync();
			var paidPerCharge = chargeCollQ
				.Where(r => r.StudentId == studentId)
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
					Name = c.PurposeDescription,
					Amount = net,
					PaidAmount = paid,
					PendingAmount = pending,
					Status = pending <= 0d ? FeeCollectionStatus.Paid : FeeCollectionStatus.Pending,
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

			var feeCollQ = await feeCollectionRecordRepository.GetQueryableAsync();
			var paidPerHead = feeCollQ
				.Where(r => r.StudentId == studentId)
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
					Status = pending <= 0d ? FeeCollectionStatus.Paid : FeeCollectionStatus.Pending,
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
			var paidPerCharge = chargeCollQ
				.Where(r => r.StudentId == studentId)
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
					Name = c.PurposeDescription,
					Amount = net,
					PaidAmount = paid,
					PendingAmount = pending,
					Status = pending <= 0d ? FeeCollectionStatus.Paid : FeeCollectionStatus.Pending,
				};
			}).ToList();
		}

		// ── Payment history ───────────────────────────────────────────────
		var metadataQ = await feeRecordMetadataRepository.GetQueryableAsync();
		var paymentMethodQ = await paymentMethodRecordRepository.GetQueryableAsync();

		var metadataForStudent = metadataQ
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
			}).ToList();
		}

		// ── Concessions ───────────────────────────────────────────────────
		var concessionRequests = await concessionRequestRepository.GetListAsync(
			skipCount: 0,
			maxResultCount: int.MaxValue,
			studentId: studentId,
			status: ConcessionRequestStatus.Approved);

		List<ConcessionDetailDto>? concessions = null;
		if (concessionRequests.Count > 0)
		{
			var typeIds = concessionRequests.Select(r => r.ConcessionTypeId).Distinct().ToList();
			var concessionTypes = await concessionTypeRepository.GetListAsync(ct => typeIds.Contains(ct.Id));
			var typeMap = concessionTypes.ToDictionary(ct => ct.Id, ct => ct.Name);

			concessions = concessionRequests.Select(r =>
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
		var totalConcession = concessions?.Sum(c => c.Amount ?? 0d);

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
	}
}
