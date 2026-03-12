using System;

namespace ShikshaMitra.Library.IssueReturn;

public class ReturnBookSearchDto
{
    public ReturnBookSearchBookDto BookDetails { get; set; }
    public ReturnBookSearchMemberDto MemberDetails { get; set; }
}

public class ReturnBookSearchBookDto
{
    public Guid Id { get; set; }
    public string AccessionNo { get; set; }
    public string BookTitle { get; set; }
    public string Author { get; set; }
    public string Isbn { get; set; }
    public string Publisher { get; set; }
    public DateTime AddedOn { get; set; }
    public int Language { get; set; }
    public string Edition { get; set; }
    public string Category { get; set; }
    public int Pages { get; set; }
    public string? CoverImage { get; set; }
}

public class ReturnBookSearchMemberDto
{
    public Guid Id { get; set; }
    public string MemberName { get; set; }
    public string? Avatar { get; set; }
    public string? Email { get; set; }
    public string Role { get; set; }
    public string? ClassDivision { get; set; }
    public string? RollNo { get; set; }
    public string? GrNo { get; set; }
    public string? Phone { get; set; }
    public DateTime? JoinDate { get; set; }
}
