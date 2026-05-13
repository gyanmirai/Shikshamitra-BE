using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ClosedXML.Excel;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Finance.Collections;
using ShikshaMitra.Finance.FeeCollection;
using ShikshaMitra.Finance.Fees.Collections.Dtos;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Students;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.Timing;

namespace ShikshaMitra.Finance.Fees.Collections;

[Authorize(ShikshaMitraSubModulePermissions.FeeCollection)]
[RemoteService(false)]
public class FeeCollectionAppServiceV2(
	IReadOnlyRepository<Student, Guid> studentRepository,
	IReadOnlyRepository<DueFee, Guid> dueFeeRepository,
	IAsyncQueryableExecuter asyncExecuter,
	IClock clock
) : ApplicationService, IFeeCollectionAppServiceV2
{
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

		var temp = await AsyncExecuter.ToListAsync(studentQ.Take(10));

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
				Outstanding = g.Sum(d => (double?) d.OutstandingAmount) ?? 0d,
			});

		var temp1 = await AsyncExecuter.ToListAsync(feeAggPerStudent.Take(10));

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

		var temp2 = await AsyncExecuter.ToListAsync(overduePerStudent.Take(10));

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
				TotalFees = x.fa == null ? 0d : (x.fa.Total),
				OutstandingFees = x.fa == null ? 0d : (x.fa.Outstanding),
				OverdueFees = ov == null ? 0d : (ov.Overdue),
				PaidFees = x.fa == null ? 0d : (x.fa.Paid),
			});

		var temp3 = await AsyncExecuter.ToListAsync(query.Take(10));

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
