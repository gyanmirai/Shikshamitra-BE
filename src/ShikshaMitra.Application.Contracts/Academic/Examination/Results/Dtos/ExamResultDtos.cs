using System;
using System.Collections.Generic;

namespace ShikshaMitra.Academic.Examination.Results.Dtos;

// ─── Request DTOs ─────────────────────────────────────────────────────────────

public class GenerateResultRequestDto
{
    public Guid ExamId { get; set; }
    public Guid DivisionId { get; set; }

    /// <summary>
    /// When true, allows regeneration even if a result is already Frozen.
    /// </summary>
    public bool ForceRegenerate { get; set; } = false;
}

public class PublishResultRequestDto
{
    public Guid ResultHeaderId { get; set; }
}

public class FreezeResultRequestDto
{
    public Guid ResultHeaderId { get; set; }
    public string? Remarks { get; set; }
}

// ─── Response DTOs ────────────────────────────────────────────────────────────

public class ResultHeaderDto
{
    public Guid Id { get; set; }
    public Guid ExamId { get; set; }
    public Guid DivisionId { get; set; }
    public string Status { get; set; } = string.Empty;   // "Generated" | "Published" | "Frozen"
    public int StatusCode { get; set; }

    public DateTime? GeneratedOn { get; set; }
    public string? GeneratedByName { get; set; }

    public DateTime? PublishedOn { get; set; }
    public DateTime? FrozenOn { get; set; }

    public string? Remarks { get; set; }

    // Summary counts (populated on generate/refresh)
    public int TotalStudents { get; set; }
    public int Appeared { get; set; }
    public int Passed { get; set; }
    public int Failed { get; set; }
    public int Compartment { get; set; }
    public int Absent { get; set; }
    public double PassPercentage { get; set; }
    public double HighestPercentage { get; set; }
    public double AveragePercentage { get; set; }
}

public class ResultAnalyticsDto
{
    public Guid ResultHeaderId { get; set; }
    public int TotalStudents { get; set; }
    public int Appeared { get; set; }
    public int Passed { get; set; }
    public int Failed { get; set; }
    public int Compartment { get; set; }
    public int Absent { get; set; }
    public double PassPercentage { get; set; }
    public double HighestPercentage { get; set; }
    public double LowestPercentage { get; set; }
    public double AveragePercentage { get; set; }
    public Dictionary<string, int> GradeDistribution { get; set; } = new();
}

// ─── Readiness Check DTOs ─────────────────────────────────────────────────────

public class ResultReadinessDto
{
    public Guid ExamId { get; set; }
    public Guid DivisionId { get; set; }
    public bool IsReady { get; set; }
    public List<ReadinessCheckDto> Checks { get; set; } = new();
}

public class ReadinessCheckDto
{
    public string Label { get; set; } = string.Empty;
    public bool Passed { get; set; }
    public string? Hint { get; set; }
}
