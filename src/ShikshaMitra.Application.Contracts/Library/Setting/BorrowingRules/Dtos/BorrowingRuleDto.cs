using System;

namespace ShikshaMitra.Library.Setting.BorrowingRules.Dtos;

public class BorrowingRuleDto
{
	public Guid Id { get; set; }
	public string? Role { get; set; }
	public int MaxBooksAtTime { get; set; }
	public int ReservationLimit { get; set; }
}
