using System;
using ShikshaMitra.Finance.FeeCollection;

namespace ShikshaMitra.Finance.Fees.Collections.Dtos;

/// <summary>
/// Single student row in the V2 fee-collection summary list.
/// All monetary values use <c>double</c> consistent with the V1 convention.
/// </summary>
public class FeeCollectionSummaryItemDtoV2
{
	/// <summary>Unique identifier of the student.</summary>
	public Guid StudentId { get; set; }

	/// <summary>Full name of the student (First [Middle] Last).</summary>
	public string StudentName { get; set; } = string.Empty;

	/// <summary>General Register number assigned to the student.</summary>
	public string GrNumber { get; set; } = string.Empty;

	/// <summary>Roll number within the class/division.</summary>
	public string RollNumber { get; set; } = string.Empty;

	/// <summary>
	/// Combined standard and division label, e.g. <c>"10 - A"</c>.
	/// Empty when either component is unavailable.
	/// </summary>
	public string StandardDivision { get; set; } = string.Empty;

	/// <summary>
	/// Sum of all fee-structure detail amounts assigned to the student
	/// for the requested academic year.
	/// </summary>
	public double TotalFees { get; set; }

	/// <summary>Sum of all fee-collection record amounts for the student.</summary>
	public double PaidFees { get; set; }

	/// <summary>TotalFees − PaidFees (clamped to zero).</summary>
	public double PendingFees { get; set; }

	/// <summary>
	/// Sum of unpaid charge amounts whose <c>DueDate</c> has already passed.
	/// </summary>
	public double OverdueFees { get; set; }

	/// <summary>
	/// Sum of approved charge amounts for the student in the requested academic year.
	/// </summary>
	public double TotalCharges { get; set; }

	/// <summary>
	/// Sum of unpaid approved charge balances (amount minus collected, excluding reversed payments).
	/// </summary>
	public double PendingCharges { get; set; }

	/// <summary>
	/// Computed fee status.
	/// <list type="bullet">
	///   <item><c>Paid</c> — PendingFees = 0 and OverdueFees = 0</item>
	///   <item><c>Overdue</c> — OverdueFees &gt; 0</item>
	///   <item><c>Pending</c> — PendingFees &gt; 0 and OverdueFees = 0</item>
	/// </list>
	/// </summary>
	public FeeCollectionStatus Status { get; set; }
}
