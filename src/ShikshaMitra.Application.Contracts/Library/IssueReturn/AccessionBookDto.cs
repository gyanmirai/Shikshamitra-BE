using System;

namespace ShikshaMitra.Library.IssueReturn;

public class AccessionBookDto
{
    public Guid BookId { get; set; }
    public string Title { get; set; }
    public string AccessionNo { get; set; }
    public string Author { get; set; }
    public string Isbn { get; set; }
    public string Publisher { get; set; }
    public string Edition { get; set; }
    public DateTime AddedOn { get; set; }
    public string Language { get; set; }
    public string Category { get; set; }
    public int Pages { get; set; }
}
