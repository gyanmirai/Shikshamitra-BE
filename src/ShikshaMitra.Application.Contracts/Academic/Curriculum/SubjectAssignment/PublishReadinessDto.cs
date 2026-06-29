using System;
using System.Collections.Generic;

namespace ShikshaMitra.Academic.Curriculum;

public class PublishReadinessDto
{
    public int OverallHealth { get; set; }
    public int ReadyToPublish { get; set; }
    public int NeedsAttention { get; set; }
    public List<PublishReadinessIssueDto> Issues { get; set; } = [];
}

public class PublishReadinessIssueDto
{
    public Guid SubjectId { get; set; }
    public string? SubjectName { get; set; }

    /// <summary>"Warning" or "Info"</summary>
    public string Severity { get; set; } = "Warning";
    public string? Message { get; set; }
}
