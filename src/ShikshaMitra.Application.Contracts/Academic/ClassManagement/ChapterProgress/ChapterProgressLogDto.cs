using System;

namespace ShikshaMitra.Academic.ClassManagement.ChapterProgress;

public class ChapterProgressLogDto
{
    public string ChangedByName { get; set; }
    public int Progress { get; set; }
    public DateTime ChangedAt { get; set; }
}
