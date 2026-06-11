using ClosedXML.Excel;
using CsvHelper;
using CsvHelper.Configuration;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Finance.Common;
using ShikshaMitra.Finance.Fees.Charges;
using ShikshaMitra.Finance.Fees.Collections.Dtos;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Students;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.Standards;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Timing;
using Volo.Abp.Validation;

namespace ShikshaMitra.Finance.Fees.Collections;

[RemoteService(false)]
public class FeeCollectionSummaryQueryService(
	IReadOnlyRepository<Student, Guid> studentRepository,
	IReadOnlyRepository<Standard, Guid> standardRepository,
	IReadOnlyRepository<Division, Guid> divisionRepository,
	IDueFeeRepository dueFeeRepository,
	IRepository<FeeRecordMetadata, Guid> feeRecordMetadataRepository,
	IRepository<ChargeCollectionRecord, Guid> chargeCollectionRecordRepository,
	IRepository<Charge, Guid> chargeRepository,
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

	private static void EnsureSummaryContextIsValid(GetFeeCollectionSummaryInput input)
	{
		EnsureNotEmpty(input.AcademicYearId, nameof(input.AcademicYearId));
		EnsureNotEmpty(input.BoardId, nameof(input.BoardId));
		EnsureNotEmpty(input.EducationMediumId, nameof(input.EducationMediumId));
		FinanceClassDivisionFilterValidator.Validate(input);
	}

	public async Task<PagedResultDto<FeeCollectionSummaryItemDto>> GetSummaryAsync(
		GetFeeCollectionSummaryInput input,
		CancellationToken cancellationToken = default)
	{
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

	private async Task<List<FeeCollectionRawRow>> BuildRawRowsAsync(
		GetFeeCollectionSummaryInput input,
		CancellationToken cancellationToken = default)
	{
		EnsureSummaryContextIsValid(input);

		var now          = clock.Now;
		var currentMonth = now.Month - 1; // MonthEnum is 0-indexed
		var currentYear  = now.Year;

		// 1. Students
		var studentQ = await BuildFilteredStudentQueryAsync(input, cancellationToken);

		var students = await AsyncExecuter.ToListAsync(
			studentQ.Select(s => new
			{
				s.Id, s.FirstName, s.MiddleName, s.LastName,
				s.GrNo, s.RollNo, s.StandardId, s.DivisionId, s.Standard, s.Division, s.FeeStructureId
			}),
			cancellationToken);

		if (students.Count == 0)
			return [];

		var studentIds = students.Select(s => s.Id).ToList();

		// 2. Fee aggregates per student
		var dueFeeQ = await dueFeeRepository.GetQueryableAsync();

		var (feeAggs, overdueAggs) = await GetStudentFeeAggregatesAsync(
			dueFeeQ, studentIds, currentYear, currentMonth, cancellationToken);

		// 3. Charge aggregates per student
		var chargeAggDict = await BuildChargeAggregatesByStudentAsync(
			studentIds,
			input.AcademicYearId,
			cancellationToken);

		// 4. Join in memory
		var feeDict    = feeAggs.ToDictionary(fa => fa.StudentId);
		var overdueDict = overdueAggs.ToDictionary(ov => ov.StudentId);

		return students.Select(s =>
		{
			feeDict.TryGetValue(s.Id, out var fa);
			overdueDict.TryGetValue(s.Id, out var ov);
			chargeAggDict.TryGetValue(s.Id, out var ca);
			return new FeeCollectionRawRow
			{
				StudentId       = s.Id,
				FirstName       = s.FirstName,
				MiddleName      = s.MiddleName,
				LastName        = s.LastName,
				GrNo            = s.GrNo?.ToString() ?? string.Empty,
				RollNo          = s.RollNo?.ToString() ?? string.Empty,
				StandardName    = s.Standard?.Name ?? string.Empty,
				StandardId		= s.StandardId ?? Guid.Empty,
				DivisionName    = s.Division?.Name ?? string.Empty,
				DivisionId		= s.DivisionId ?? Guid.Empty,
				TotalFees       = fa.Total,
				PaidFees        = fa.Paid,
				OutstandingFees = fa.Outstanding,
				OverdueFees     = ov.Overdue,
				TotalCharges    = ca.TotalCharges,
				PendingCharges  = ca.PendingCharges,
				IsFeeStructureAssigned = s.FeeStructureId != null,
			};
		}).ToList();
	}

	private async Task<(
		List<(Guid StudentId, double Total, double Paid, double Outstanding)> FeeAggs,
		List<(Guid StudentId, double? Overdue)>                               OverdueAggs
	)> GetStudentFeeAggregatesAsync(
		IQueryable<DueFee>  dueFeeQ,
		ICollection<Guid>   studentIds,
		int                 currentYear,
		int                 currentMonth,
		CancellationToken   cancellationToken)
	{
		var feeAggs = await AsyncExecuter.ToListAsync(
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

		var overdueAggs = await AsyncExecuter.ToListAsync(
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

		return (
			feeAggs.Select(x    => (x.StudentId, x.Total, x.Paid, x.Outstanding)).ToList(),
			overdueAggs.Select(x => (x.StudentId, x.Overdue)).ToList()
		);
	}

	private async Task<Dictionary<Guid, StudentChargeAggregate>> BuildChargeAggregatesByStudentAsync(
		List<Guid> studentIds,
		Guid academicYearId,
		CancellationToken cancellationToken = default)
	{
		var result = studentIds.ToDictionary(id => id, _ => StudentChargeAggregate.Empty);

		if (studentIds.Count == 0)
			return result;

		var chargeQ = await chargeRepository.GetQueryableAsync();
		var approvedCharges = await AsyncExecuter.ToListAsync(
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
		var reversedTxIds = await AsyncExecuter.ToListAsync(
			metaQ
				.Where(m => m.Status == FeeCollectionStatus.Reversed)
				.Select(m => m.Id),
			cancellationToken);

		var chargeCollQ = await chargeCollectionRecordRepository.GetQueryableAsync();
		var paidPerCharge = await AsyncExecuter.ToListAsync(
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
				StandardId       = r.StandardId,
				DivisionId		 = r.DivisionId,
				StandardDivision = string.IsNullOrEmpty(r.DivisionName)
					? r.StandardName
					: $"{r.StandardName} - {r.DivisionName}",
				TotalFees   = r.TotalFees ?? 0d,
				PaidFees    = r.PaidFees ?? 0d,
				PendingFees = outstanding ?? 0d,
				OverdueFees = overdue ?? 0d,
				TotalCharges = r.TotalCharges ?? 0d,
				PendingCharges = r.PendingCharges ?? 0d,
				IsFeeStructureAssigned = r.IsFeeStructureAssigned,
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
			"TotalCharges", "Pending Charges", "Status",
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

	private bool IsPrivilegedUser() => CurrentUser.IsInRole("admin")
			|| CurrentUser.IsInRole("Finance Officer");

	private readonly record struct StudentChargeAggregate(double TotalCharges, double PendingCharges)
	{
		public static StudentChargeAggregate Empty => new(0d, 0d);
	}

	private sealed class FeeCollectionRawRow
	{
		public Guid    StudentId       { get; init; }
		public string? FirstName       { get; init; }
		public string? MiddleName      { get; init; }
		public string? LastName        { get; init; }
		public string  GrNo            { get; init; } = string.Empty;
		public string  RollNo          { get; init; } = string.Empty;
		public Guid StandardId { get; set; }
		public string  StandardName    { get; init; } = string.Empty;
		public Guid DivisionId { get; set; }
		public string  DivisionName    { get; init; } = string.Empty;
		public double?  TotalFees       { get; init; }
		public double?  PaidFees        { get; init; }
		public double?  OutstandingFees { get; init; }
		public double?  OverdueFees     { get; init; }
		public double?  TotalCharges    { get; init; }
		public double?  PendingCharges  { get; init; }
		public bool IsFeeStructureAssigned { get; set; } = false;
	}

	private async Task<IQueryable<Student>> BuildFilteredStudentQueryAsync(
		GetFeeCollectionSummaryInput input,
		CancellationToken cancellationToken = default)
	{
		var studentQ = await studentRepository.GetQueryableAsync();

		studentQ = studentQ.Where(s =>
			s.AcademicYearId   == input.AcademicYearId   &&
			s.AcademicBoardId  == input.BoardId           &&
			s.AcademicMediumId == input.EducationMediumId);

		studentQ = IsPrivilegedUser()
			? studentQ.Where(s => s.Status == StudentLifeCycleStatusEnum.Enrolled
								|| s.Status == StudentLifeCycleStatusEnum.Left
								|| s.Status == StudentLifeCycleStatusEnum.InActive)
			: studentQ.Where(s => s.Status == StudentLifeCycleStatusEnum.Enrolled);

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

		var scopedStudentIds = await financeScopeAccessService.GetScopedStudentIdsQueryAsync();
		return studentQ.Where(s => scopedStudentIds.Contains(s.Id)).OrderBy(s => s.Standard.CreationTime).ThenBy(s => s.Division.CreationTime).ThenBy(s => s.RollNo);
	}
}
