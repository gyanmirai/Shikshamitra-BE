using ShikshaMitra.Finance.Common;
using ShikshaMitra.Finance.Fees.Collections.Dtos;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Students;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Validation;

namespace ShikshaMitra.Finance.Fees.Collections;

public partial class FeeCollectionSummaryQueryService
{
	private static void EnsureNotEmpty(Guid value, string fieldName)
	{
		if (value != Guid.Empty) return;
		throw new AbpValidationException(
			"Validation failed",
			[new ValidationResult($"{fieldName} is required.", new[] { fieldName })]);
	}

	private static void EnsureSummaryContextIsValid(GetFeeCollectionSummaryInput input)
	{
		EnsureNotEmpty(input.AcademicYearId, nameof(input.AcademicYearId));
		EnsureNotEmpty(input.BoardId, nameof(input.BoardId));
		EnsureNotEmpty(input.EducationMediumId, nameof(input.EducationMediumId));
		FinanceClassDivisionFilterValidator.Validate(input);
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
				DivisionId       = r.DivisionId,
				StandardDivision = string.IsNullOrEmpty(r.DivisionName)
					? r.StandardName
					: $"{r.StandardName} - {r.DivisionName}",
				TotalFees              = r.TotalFees ?? 0d,
				PaidFees               = r.PaidFees ?? 0d,
				PendingFees            = outstanding ?? 0d,
				OverdueFees            = overdue ?? 0d,
				TotalCharges           = r.TotalCharges ?? 0d,
				PendingCharges         = r.PendingCharges ?? 0d,
				TotalConcessionAmount  = r.TotalConcessionAmount ?? 0d,
				IsFeeStructureAssigned = r.IsFeeStructureAssigned,
				Status = GetSummaryStatus(r),
			};
		});

	private static string BuildFullName(string? first, string? middle, string? last)
		=> string.Join(" ", new[] { first, middle, last }.Where(p => !string.IsNullOrWhiteSpace(p)));

	private static FeeCollectionStatus GetSummaryStatus(FeeCollectionRawRow row)
	{
		if (!row.IsFeeStructureAssigned
			&& (row.TotalFees ?? 0d) == 0d
			&& (row.PaidFees ?? 0d) == 0d
			&& (row.OutstandingFees ?? 0d) == 0d
			&& (row.OverdueFees ?? 0d) == 0d
			&& (row.TotalCharges ?? 0d) == 0d
			&& (row.PendingCharges ?? 0d) == 0d)
			return FeeCollectionStatus.NoFeeAssigned;

		return (row.OutstandingFees ?? 0d) <= 0d && (row.PendingCharges ?? 0d) <= 0d
			? FeeCollectionStatus.Paid
			: (row.OverdueFees ?? 0d) > 0d
				? FeeCollectionStatus.Overdue
				: FeeCollectionStatus.Pending;
	}

	private static List<FeeCollectionRawRow> ApplyStatusFilter(
		List<FeeCollectionRawRow> rows, FeeCollectionStatus? status)
	{
		if (!status.HasValue) return rows;
		return status.Value switch
		{
			FeeCollectionStatus.Paid or FeeCollectionStatus.Overdue
				or FeeCollectionStatus.Pending or FeeCollectionStatus.NoFeeAssigned
				=> rows.Where(x => GetSummaryStatus(x) == status.Value).ToList(),
			_                           => rows,
		};
	}

	private static IOrderedEnumerable<FeeCollectionRawRow> ApplySorting(
		List<FeeCollectionRawRow> rows, string? sorting)
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
		if (!string.IsNullOrWhiteSpace(input.Search))
		{
			var terms = input.Search.Split((char[]?)null,
				StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
			if (terms.Length == 1 && terms[0].Length >= 2)
			{
				var term = terms[0].ToLower();
				studentQ = studentQ.Where(s =>
					(s.FirstName  != null && s.FirstName.ToLower().Contains(term))  ||
					(s.MiddleName != null && s.MiddleName.ToLower().Contains(term)) ||
					(s.LastName   != null && s.LastName.ToLower().Contains(term))   ||
					(s.RollNo     != null && s.RollNo.ToString()!.Contains(term)));
			}
			else if (terms.Length > 1)
			{
				foreach (var word in terms)
				{
					var term = word.ToLower();
					studentQ = studentQ.Where(s =>
						(s.FirstName  != null && s.FirstName.ToLower().Contains(term))  ||
						(s.MiddleName != null && s.MiddleName.ToLower().Contains(term)) ||
						(s.LastName   != null && s.LastName.ToLower().Contains(term)));
				}
			}
		}
		var scopedStudentIds = await financeScopeAccessService.GetScopedStudentIdsQueryAsync();
		return studentQ
			.Where(s => scopedStudentIds.Contains(s.Id))
			.OrderBy(s => s.Standard.CreationTime)
			.ThenBy(s => s.Division.CreationTime)
			.ThenBy(s => s.RollNo);
	}

	private bool IsPrivilegedUser() =>
		CurrentUser.IsInRole("admin") || CurrentUser.IsInRole("Finance Officer");

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
		public Guid    StandardId      { get; set; }
		public string  StandardName    { get; init; } = string.Empty;
		public Guid    DivisionId      { get; set; }
		public string  DivisionName    { get; init; } = string.Empty;
		public double? TotalFees       { get; init; }
		public double? PaidFees        { get; init; }
		public double? OutstandingFees { get; init; }
		public double? OverdueFees     { get; init; }
		public double? TotalCharges    { get; init; }
		public double? PendingCharges  { get; init; }
		public double? TotalConcessionAmount   { get; set; }
		public bool    IsFeeStructureAssigned  { get; set; }
	}
}
