using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Library.Setting.BorrowingRules.Dtos;

public class BorrowingRulesUpdateRequest
{
	[Required]
	public List<BorrowingRuleItemRequest> Rules { get; set; } = new();
}

public class BorrowingRuleItemRequest
{
	/// <summary>
	/// 0 = Student, 1 = Teacher
	/// </summary>
	[Required]
	public int Role { get; set; }

	[Required]
	public int MaxBooksAtTime { get; set; }

	[Required]
	public int ReservationLimit { get; set; }
}
