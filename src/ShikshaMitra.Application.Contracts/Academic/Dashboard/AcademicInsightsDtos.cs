using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Academic.Dashboard;

public class AcademicInsightsInput : IValidatableObject
{
	public Guid AcademicYearId { get; set; }
	public Guid BoardId { get; set; }
	public Guid EducationMediumId { get; set; }
	public Guid? StandardId { get; set; }
	public Guid? DivisionId { get; set; }
	public Guid? StudentId { get; set; }
	public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
	{
		if (AcademicYearId == Guid.Empty) yield return new("Academic year is required.", [nameof(AcademicYearId)]);
		if (BoardId == Guid.Empty) yield return new("Board is required.", [nameof(BoardId)]);
		if (EducationMediumId == Guid.Empty) yield return new("Education medium is required.", [nameof(EducationMediumId)]);
	}
}

public class AcademicInsightsDto
{
	public string Role { get; set; } = "common";
	public string ScopeLabel { get; set; } = "Assigned academic scope";
	public DateTime GeneratedAt { get; set; }
	public List<AcademicInsightSectionDto> Sections { get; set; } = [];
	public List<AcademicInsightStudentDto> Students { get; set; } = [];
}
public record AcademicInsightStudentDto(Guid Id, string Label);
public class AcademicInsightSectionDto
{
	public string Key { get; set; } = "";
	public string Status { get; set; } = "empty";
	public string Summary { get; set; } = "";
	public List<AcademicInsightMetricDto> Metrics { get; set; } = [];
	public List<AcademicInsightSeriesDto> Series { get; set; } = [];
	public List<AcademicInsightItemDto> Items { get; set; } = [];
}
public record AcademicInsightMetricDto(string Key, string Label, double? Value, string? Unit = null);
public record AcademicInsightSeriesDto(string Name, List<AcademicInsightPointDto> Points);
public record AcademicInsightPointDto(string Key, string Label, double Value, double? X = null, double? Y = null, string? Status = null);
public record AcademicInsightItemDto(string Id, string Label, string? Detail = null, DateTime? Date = null, string? Status = null, double? Value = null);
public interface IAcademicInsightsAppService : IApplicationService
{
	Task<AcademicInsightsDto> GetAsync(AcademicInsightsInput input);
}
