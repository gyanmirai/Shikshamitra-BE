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

namespace ShikshaMitra.Finance.Fees.Collections;

/// <summary>
/// V2 implementation of the fee-collection summary screen.
/// </summary>
/// <remarks>
/// <para><b>Data source:</b> All fee figures are derived exclusively from the
/// <c>DueFees</c> table. <see cref="DueFee"/> is the single source of truth for
/// fee obligations and payment state — fee structure or legacy collection-record
/// tables are NOT queried here.</para>
///
/// <para><b>Calculation rules (all from DueFee):</b></para>
/// <list type="table">
///   <item><term>TotalFees</term><description>SUM(OriginalAmount) per student</description></item>
///   <item><term>PaidFees</term><description>SUM(PaidAmount) per student</description></item>
///   <item><term>PendingFees</term><description>SUM(OutstandingAmount) per student</description></item>
///   <item><term>OverdueFees</term><description>SUM(OutstandingAmount) for installments whose Month/Year is before the current calendar month</description></item>
/// </list>
///
/// <para><b>Performance strategy:</b></para>
/// <list type="bullet">
///   <item>All filtering, sorting, and pagination are pushed to the database.</item>
///   <item>Two EF Core subqueries (<c>feeAggPerStudent</c>, <c>overduePerStudent</c>) compose into a single SQL JOIN.</item>
///   <item>Status is computed inside the SQL projection — status filter is server-side.</item>
///   <item><see cref="IReadOnlyRepository{T,TKey}"/> provides implicit AsNoTracking.</item>
///   <item>Only two DB round-trips per request: one COUNT and one SELECT.</item>
/// </list>
/// </remarks>
[Authorize]
[RemoteService(false)]
public class FeeCollectionAppServiceV2(
	IReadOnlyRepository<Student, Guid> studentRepository,
	IReadOnlyRepository<DueFee, Guid> dueFeeRepository,
	IAsyncQueryableExecuter asyncExecuter
) : ApplicationService, IFeeCollectionAppServiceV2
{
	// ── Public API ────────────────────────────────────────────────────────

	[Authorize(ShikshaMitraSubModulePermissions.FeeCollection)]
	public async Task<PagedResultDto<FeeCollectionSummaryItemDtoV2>> GetSummaryAsync(
		GetFeeCollectionSummaryInputV2 input,
		CancellationToken cancellationToken = default)
	{
		var query = await BuildProjectedQueryAsync(input);

		// ── Server-side status filter ─────────────────────────────────────
		if (input.Status.HasValue)
			query = query.Where(x => x.Status == input.Status.Value);

		// ── Count (separate round-trip, no ORDER BY overhead) ─────────────
		var totalCount = await asyncExecuter.CountAsync(query, cancellationToken);
		if (totalCount == 0)
			return new PagedResultDto<FeeCollectionSummaryItemDtoV2>(0, []);

		// ── Sorting ───────────────────────────────────────────────────────
		query = ApplySorting(query, input.Sorting);

		// ── Pagination ────────────────────────────────────────────────────
		var items = await asyncExecuter.ToListAsync(
			query.Skip(input.SkipCount).Take(input.MaxResultCount),
			cancellationToken);

		return new PagedResultDto<FeeCollectionSummaryItemDtoV2>(totalCount, items);
	}

	[Authorize(ShikshaMitraSubModulePermissions.FeeCollection)]
	public async Task<byte[]> ExportAsync(
		FeeCollectionExportRequestDto input,
		CancellationToken cancellationToken = default)
	{
		// Re-use the same projection query without pagination.
		var listInput = new GetFeeCollectionSummaryInputV2
		{
			AcademicYearId    = input.AcademicYearId,
			BoardId           = input.BoardId,
			EducationMediumId = input.EducationMediumId,
			StandardId        = input.StandardId,
			DivisionId        = input.DivisionId,
			Search            = input.Search,
			Status            = input.Status,
			SkipCount         = 0,
			MaxResultCount    = int.MaxValue,
		};

		var query = await BuildProjectedQueryAsync(listInput);

		if (input.Status.HasValue)
			query = query.Where(x => x.Status == input.Status.Value);

		query = ApplySorting(query, sorting: null);

		var rows = await asyncExecuter.ToListAsync(query, cancellationToken);

		return input.Format == FeeCollectionExportFormat.Excel
			? BuildExcel(rows)
			: BuildCsv(rows);
	}

	// ── Core query builder ────────────────────────────────────────────────

	/// <summary>
	/// Builds the single server-side IQueryable projection.
	/// All monetary figures come exclusively from <see cref="DueFee"/> rows.
	/// </summary>
	private async Task<IQueryable<FeeCollectionSummaryItemDtoV2>> BuildProjectedQueryAsync(
		GetFeeCollectionSummaryInputV2 input)
	{
		// MonthEnum is 0-indexed (January = 0, December = 11).
		// An installment is "overdue" when its Month/Year is strictly before
		// the current calendar month — i.e. the student should have paid by now.
		var currentMonth = DateTime.UtcNow.Month - 1; // convert to 0-based MonthEnum
		var currentYear  = DateTime.UtcNow.Year;

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
		// Reads directly from DueFee — the authoritative fee ledger.
		var feeAggPerStudent =
			from d in dueFeeQ
			group d by d.StudentId into g
			select new
			{
				StudentId   = g.Key,
				Total       = g.Sum(d => d.OriginalAmount),
				Paid        = g.Sum(d => d.PaidAmount),
				Outstanding = g.Sum(d => d.OutstandingAmount),
			};

		// ── Subquery B: overdue outstanding per student ────────────────────
		// An installment is overdue when it still has an outstanding balance
		// AND its Year/Month is strictly before the current calendar month.
		var overduePerStudent =
			from d in dueFeeQ
			where d.OutstandingAmount > 0d
				  && (d.Year < currentYear
					  || (d.Year == currentYear && (int)d.Month < currentMonth))
			group d by d.StudentId into g
			select new
			{
				StudentId = g.Key,
				Overdue   = g.Sum(d => d.OutstandingAmount),
			};

		// ── Final projection ───────────────────────────────────────────────
		var query =
			from s in studentQ

			// Left-join fee aggregates (student may have no DueFee rows yet)
			join fa in feeAggPerStudent on s.Id equals fa.StudentId into faj
			from fa in faj.DefaultIfEmpty()

			// Left-join overdue aggregates
			join ov in overduePerStudent on s.Id equals ov.StudentId into ovj
			from ov in ovj.DefaultIfEmpty()

			let total       = fa == null ? 0d : fa.Total
			let paid        = fa == null ? 0d : fa.Paid
			let outstanding = fa == null ? 0d : fa.Outstanding
			let overdue     = ov == null ? 0d : ov.Overdue

			select new FeeCollectionSummaryItemDtoV2
			{
				StudentId        = s.Id,
				StudentName      = (s.FirstName ?? string.Empty)
								 + (s.MiddleName != null ? " " + s.MiddleName : string.Empty)
								 + (s.LastName   != null ? " " + s.LastName   : string.Empty),
				GrNumber         = s.GrNo   != null ? s.GrNo.ToString()  : string.Empty,
				RollNumber       = s.RollNo != null ? s.RollNo.ToString() : string.Empty,
				StandardDivision = (s.Standard != null ? s.Standard.Name : string.Empty)
								 + (s.Division  != null ? " - " + s.Division.Name : string.Empty),
				TotalFees   = total,
				PaidFees    = paid,
				PendingFees = outstanding,
				OverdueFees = overdue,
				// Status:
				//   Paid    → nothing outstanding
				//   Overdue → has overdue balance
				//   Pending → has balance but none overdue yet
				Status = outstanding <= 0d
					? FeeCollectionStatus.Paid
					: overdue > 0d
						? FeeCollectionStatus.Overdue
						: FeeCollectionStatus.Pending,
			};

		return query;
	}

	// ── Sorting ───────────────────────────────────────────────────────────

	private static IQueryable<FeeCollectionSummaryItemDtoV2> ApplySorting(
		IQueryable<FeeCollectionSummaryItemDtoV2> query,
		string? sorting)
	{
		var sort = (sorting ?? string.Empty).Trim().ToLower();

		return sort switch
		{
			"studentname asc"      or "studentname"       => query.OrderBy(x => x.StudentName),
			"studentname desc"                            => query.OrderByDescending(x => x.StudentName),
			"rollnumber asc"       or "rollnumber"        => query.OrderBy(x => x.RollNumber),
			"rollnumber desc"                             => query.OrderByDescending(x => x.RollNumber),
			"grnumber asc"         or "grnumber"          => query.OrderBy(x => x.GrNumber),
			"grnumber desc"                               => query.OrderByDescending(x => x.GrNumber),
			"totalfees asc"        or "totalfees"         => query.OrderBy(x => x.TotalFees),
			"totalfees desc"                              => query.OrderByDescending(x => x.TotalFees),
			"paidfees asc"         or "paidfees"          => query.OrderBy(x => x.PaidFees),
			"paidfees desc"                               => query.OrderByDescending(x => x.PaidFees),
			"pendingfees asc"      or "pendingfees"       => query.OrderBy(x => x.PendingFees),
			"pendingfees desc"                            => query.OrderByDescending(x => x.PendingFees),
			"overduefees asc"      or "overduefees"       => query.OrderBy(x => x.OverdueFees),
			"overduefees desc"                            => query.OrderByDescending(x => x.OverdueFees),
			"status asc"           or "status"            => query.OrderBy(x => x.Status),
			"status desc"                                 => query.OrderByDescending(x => x.Status),
			"standarddivision asc" or "standarddivision"  => query.OrderBy(x => x.StandardDivision),
			"standarddivision desc"                       => query.OrderByDescending(x => x.StandardDivision),
			_                                             => query.OrderBy(x => x.StudentName),
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
}
