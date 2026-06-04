using System;
using System.ComponentModel.DataAnnotations;
using ShikshaMitra.Finance.FeeCollection;

namespace ShikshaMitra.Finance.Fees.Collections.Dtos;

/// <summary>Supported export file formats for the fee-collection report.</summary>
public enum FeeCollectionExportFormat
{
	/// <summary>Comma-separated values (.csv).</summary>
	Csv = 0,

	/// <summary>Excel Open XML workbook (.xlsx).</summary>
	Excel = 1,
}

/// <summary>
/// Query parameters for the fee-collection export endpoint.
/// Mirrors the filters of <see cref="GetFeeCollectionSummaryInput"/> without
/// pagination — the export always returns all matching rows.
/// </summary>
public class FeeCollectionExportRequestDto
{
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
	/// Optional free-text search matching student name or roll number.
	/// Minimum 2 characters when provided.
	/// </summary>
	[MinLength(2)]
	[MaxLength(100)]
	public string? Search { get; set; }

	/// <summary>Optional status filter: 0 = Pending, 1 = Paid, 2 = Overdue.</summary>
	public FeeCollectionStatus? Status { get; set; }

	/// <summary>
	/// Desired export format. Defaults to <see cref="FeeCollectionExportFormat.Csv"/>.
	/// </summary>
	public FeeCollectionExportFormat Format { get; set; } = FeeCollectionExportFormat.Csv;
}
