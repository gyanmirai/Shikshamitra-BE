using System;
using System.ComponentModel.DataAnnotations;
using ShikshaMitra.Finance.FeeCollection;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Finance.Fees.Collections.Dtos;

/// <summary>
/// Query parameters for the V2 fee-collection summary list endpoint.
/// Uses ABP standard <see cref="PagedAndSortedResultRequestDto"/> for
/// skip-count pagination and server-side sorting.
/// </summary>
public class GetFeeCollectionSummaryInput : PagedAndSortedResultRequestDto
{
	/// <summary>Maximum page size cap.</summary>
	public const int MaxPageSize = 100;

	/// <inheritdoc />
	/// <remarks>Capped at <see cref="MaxPageSize"/>.</remarks>
	public override int MaxResultCount
	{
		get => base.MaxResultCount;
		set => base.MaxResultCount = Math.Min(value, MaxPageSize);
	}

	/// <summary>Academic year to scope results to (required).</summary>
	[Required]
	public Guid AcademicYearId { get; set; }

	/// <summary>Education board to filter by (required).</summary>
	[Required]
	public Guid BoardId { get; set; }

	/// <summary>Education medium to filter by (required).</summary>
	[Required]
	public Guid EducationMediumId { get; set; }

	/// <summary>Optional standard (class) filter.</summary>
	public Guid? StandardId { get; set; }

	/// <summary>Optional division filter.</summary>
	public Guid? DivisionId { get; set; }

	/// <summary>
	/// Optional free-text search. Matches student first/middle/last name or
	/// roll number. Minimum 2 characters when provided.
	/// </summary>
	[MinLength(2)]
	[MaxLength(100)]
	public string? Search { get; set; }

	/// <summary>
	/// Optional status filter applied server-side before pagination.
	/// <c>0 = Pending</c>, <c>1 = Paid</c>, <c>2 = Overdue</c>.
	/// </summary>
	public FeeCollectionStatus? Status { get; set; }
}
