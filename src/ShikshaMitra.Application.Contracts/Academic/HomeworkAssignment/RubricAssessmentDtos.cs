using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ShikshaMitra.Academic.HomeworkAssignment;

public class CreateUpdateRubricDto
{
	public Guid StandardId { get; set; }
	public Guid SubjectId { get; set; }
	public Guid? HomeworkId { get; set; }
	public Guid? AssignmentId { get; set; }
	public string Title { get; set; } = string.Empty;
	public string? Description { get; set; }
	public List<RubricCriterionInputDto> Criteria { get; set; } = [];
}

public class RubricCriterionInputDto
{
	public string Title { get; set; } = string.Empty;
	public string? Description { get; set; }
	public decimal MaxMarks { get; set; }
	public int DisplayOrder { get; set; }
}

public class AssessmentRubricDto
{
	public Guid Id { get; set; }
	public Guid StandardId { get; set; }
	public Guid SubjectId { get; set; }
	public Guid? HomeworkId { get; set; }
	public Guid? AssignmentId { get; set; }
	public string Title { get; set; } = string.Empty;
	public string? Description { get; set; }
	public decimal TotalMarks { get; set; }
	public int Version { get; set; }
	public RubricStatus Status { get; set; }
	public List<AssessmentRubricCriterionDto> Criteria { get; set; } = [];
}

public class AssessmentRubricCriterionDto
{
	public Guid Id { get; set; }
	public Guid RubricId { get; set; }
	public string Title { get; set; } = string.Empty;
	public string? Description { get; set; }
	public decimal MaxMarks { get; set; }
	public int DisplayOrder { get; set; }
}

public class GetRubricsInput
{
	public Guid? StandardId { get; set; }
	public Guid? SubjectId { get; set; }
	public Guid? HomeworkId { get; set; }
	public Guid? AssignmentId { get; set; }
	public RubricStatus? Status { get; set; }
	public string? Search { get; set; }
}

public class UpsertRubricEvaluationDto
{
	public Guid StudentSubmissionId { get; set; }
	public Guid StudentId { get; set; }
	public string? Remarks { get; set; }
	public List<RubricEvaluationScoreInputDto> Scores { get; set; } = [];
}

public class RubricEvaluationScoreInputDto
{
	public Guid CriterionId { get; set; }
	public decimal Marks { get; set; }
	public string? Comments { get; set; }
}

public class RubricEvaluationDto
{
	public Guid Id { get; set; }
	public Guid RubricId { get; set; }
	public Guid StudentSubmissionId { get; set; }
	public Guid StudentId { get; set; }
	public Guid EvaluatorId { get; set; }
	public decimal TotalMarks { get; set; }
	public string? Remarks { get; set; }
	public RubricEvaluationStatus Status { get; set; }
	public List<RubricCriterionScoreDto> Scores { get; set; } = [];
}

public class RubricCriterionScoreDto
{
	public Guid Id { get; set; }
	public Guid EvaluationId { get; set; }
	public Guid CriterionId { get; set; }
	public decimal Marks { get; set; }
	public string? Comments { get; set; }
}

public interface IRubricAssessmentAppService
{
	Task<AssessmentRubricDto> CreateAsync(CreateUpdateRubricDto input);
	Task<List<AssessmentRubricDto>> GetListAsync(GetRubricsInput input);
	Task<AssessmentRubricDto> GetAsync(Guid id);
	Task<AssessmentRubricDto> UpdateAsync(Guid id, CreateUpdateRubricDto input);
	Task PublishAsync(Guid id);
	Task ArchiveAsync(Guid id);
	Task<RubricEvaluationDto> UpsertEvaluationAsync(Guid rubricId, UpsertRubricEvaluationDto input);
	Task<List<RubricEvaluationDto>> GetEvaluationsAsync(Guid rubricId);
	Task SubmitEvaluationAsync(Guid evaluationId);
	Task ApproveEvaluationAsync(Guid evaluationId);
}
