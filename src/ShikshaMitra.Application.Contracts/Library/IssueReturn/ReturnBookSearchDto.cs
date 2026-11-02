using System;

namespace ShikshaMitra.Library.IssueReturn;

public class ReturnBookSearchDto
{
    public ReturnBookSearchBookDto BookDetails { get; set; } = null!; // The response mapper populates these members after construction.
    public ReturnBookSearchMemberDto MemberDetails { get; set; } = null!;
}

public class ReturnBookSearchBookDto
{
    public Guid Id { get; set; }
    public string AccessionNo { get; set; } = null!; // The response mapper populates these members after construction.
    public string BookTitle { get; set; } = null!;
    public string Author { get; set; } = null!;
    public string Isbn { get; set; } = null!;
    public string Publisher { get; set; } = null!;
    public DateTime AddedOn { get; set; }
    public int Language { get; set; }
    public string Edition { get; set; } = null!;
    public string Category { get; set; } = null!;
    public int Pages { get; set; }
    public string? CoverImage { get; set; }
}

public class ReturnBookSearchMemberDto
{
    public Guid Id { get; set; }
    public string? MemberName { get; set; }
    public string? Avatar { get; set; }
    public string? Email { get; set; }
    public string? Role { get; set; }
    public string? ClassDivision { get; set; }
    public string? RollNo { get; set; }
    public string? GrNo { get; set; }
    public string? Phone { get; set; }
    public DateTime? JoinDate { get; set; }
}
