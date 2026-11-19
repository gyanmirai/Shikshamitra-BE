using System;

namespace ShikshaMitra.Academic.Syllabus;

public class ChapterLookupDto
{
    public Guid Id { get; set; }
    public int ChapterNumber { get; set; }
    public string ChapterName { get; set; } = string.Empty;
}
