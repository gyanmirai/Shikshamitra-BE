using System;

namespace ShikshaMitra.Library.IssueReturn;

public class LibraryMemberDto
{
    public Guid Id { get; set; }
    public string MemberName { get; set; } = null!; // The response mapper populates these members after construction.
    public string? MemberIdentifier { get; set; }
    public string? Avatar { get; set; }
    public string? Email { get; set; }
    public string Role { get; set; } = null!;
    public string? ClassDivision { get; set; }
    public string? RollNo { get; set; }
    public string? GrNo { get; set; }
    public string? ContactNo { get; set; }
    public string? Phone { get; set; }
    public DateTime? JoinDate { get; set; }
    public LibraryMemberStatisticsDto Statistics { get; set; } = null!;
}

public class LibraryMemberStatisticsDto
{
    public int CurrentIssue { get; set; }
    public int ReservedBooks { get; set; }
    public int OverdueReturnBooks { get; set; }
}
