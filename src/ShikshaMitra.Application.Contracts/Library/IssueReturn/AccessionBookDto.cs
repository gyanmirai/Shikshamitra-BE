using System;

namespace ShikshaMitra.Library.IssueReturn;

public class AccessionBookDto
{
    public Guid BookId { get; set; }
    public string Title { get; set; } = null!; // The response mapper populates these members after construction.
    public string AccessionNo { get; set; } = null!;
    public string Author { get; set; } = null!;
    public string Isbn { get; set; } = null!;
    public string Publisher { get; set; } = null!;
    public string Edition { get; set; } = null!;
    public DateTime AddedOn { get; set; }
    public string Language { get; set; } = null!;
	public string? LanguageCode { get; set; }
	public string? MaterialCode { get; set; }
	public string? MaterialLabel { get; set; }
    public string Category { get; set; } = null!;
    public int Pages { get; set; }
}
