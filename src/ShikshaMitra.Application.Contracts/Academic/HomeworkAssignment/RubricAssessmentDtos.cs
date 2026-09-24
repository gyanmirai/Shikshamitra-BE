using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ShikshaMitra.Academic.HomeworkAssignment;

public class CreateUpdateRubricDto
{
	public Guid? StandardId { get; set; }
	public Guid? SubjectId { get; set; }
	public Guid? AcademicMediumId { get; set; }
	public Guid? HomeworkId { get; set; }
	public Guid? AssignmentId { get; set; }
	public Guid? HomeworkTypeId { get; set; }
	public Guid? AssignmentTypeId { get; set; }
	public bool IsTemplate { get; set; }
	public WorkType? AppliesToWorkType { get; set; }
	public RubricScoringModel ScoringModel { get; set; }
	public RubricVisibility Visibility { get; set; }
	public string Title { get; set; } = string.Empty;
	public string? Description { get; set; }
	public string? EvaluationInstructions { get; set; }
	public string? StudentInstructions { get; set; }
	public List<string> Tags { get; set; } = [];
	public List<RubricPerformanceLevelDto> PerformanceLevels { get; set; } = [];
	public List<RubricCriterionInputDto> Criteria { get; set; } = [];
}

public class CreateRubricFromTemplateDto
{
	public Guid StandardId { get; set; }
	public Guid SubjectId { get; set; }
	public Guid? HomeworkId { get; set; }
	public Guid? AssignmentId { get; set; }
	public string? Title { get; set; }
	public string? Description { get; set; }
}

public class RubricCriterionInputDto
{
	public string Title { get; set; } = string.Empty;
	public string? Description { get; set; }
	public string? StudentDescription { get; set; }
	public string? TeacherGuidance { get; set; }
	public bool IsRequired { get; set; } = true;
	public decimal MaxMarks { get; set; }
	public int DisplayOrder { get; set; }
}

public class RubricPerformanceLevelDto
{
	public int Level { get; set; }
	public string Label { get; set; } = string.Empty;
	public decimal Score { get; set; }
	public string? Description { get; set; }
	public string? StudentLabel { get; set; }
}

public class AssessmentRubricDto
{
	public Guid Id { get; set; }
	public Guid? StandardId { get; set; }
	public Guid? SubjectId { get; set; }
	public Guid? AcademicMediumId { get; set; }
	public Guid? HomeworkId { get; set; }
	public Guid? AssignmentId { get; set; }
	public Guid? HomeworkTypeId { get; set; }
	public Guid? AssignmentTypeId { get; set; }
	public Guid? SourceTemplateId { get; set; }
	public bool IsTemplate { get; set; }
	public WorkType? AppliesToWorkType { get; set; }
	public RubricScoringModel ScoringModel { get; set; }
	public RubricVisibility Visibility { get; set; }
	public string Title { get; set; } = string.Empty;
	public string? Description { get; set; }
	public string? EvaluationInstructions { get; set; }
	public string? StudentInstructions { get; set; }
	public List<string> Tags { get; set; } = [];
	public List<RubricPerformanceLevelDto> PerformanceLevels { get; set; } = [];
	public decimal TotalMarks { get; set; }
	public int Version { get; set; }
	public RubricStatus Status { get; set; }
	public DateTime CreationTime { get; set; }
	public DateTime? LastModificationTime { get; set; }
	public List<AssessmentRubricCriterionDto> Criteria { get; set; } = [];
}

public class AssessmentRubricCriterionDto
{
	public Guid Id { get; set; }
	public Guid RubricId { get; set; }
	public string Title { get; set; } = string.Empty;
	public string? Description { get; set; }
	public string? StudentDescription { get; set; }
	public string? TeacherGuidance { get; set; }
	public bool IsRequired { get; set; }
	public decimal MaxMarks { get; set; }
	public int DisplayOrder { get; set; }
}

public class GetRubricsInput
{
	public Guid? StandardId { get; set; }
	public Guid? SubjectId { get; set; }
	public Guid? AcademicMediumId { get; set; }
	public Guid? HomeworkId { get; set; }
	public Guid? AssignmentId { get; set; }
	public Guid? HomeworkTypeId { get; set; }
	public Guid? AssignmentTypeId { get; set; }
	public bool? IsTemplate { get; set; }
	public WorkType? AppliesToWorkType { get; set; }
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
	Task<AssessmentRubricDto> CreateFromTemplateAsync(Guid templateId, CreateRubricFromTemplateDto input);
	Task PublishAsync(Guid id);
	Task ArchiveAsync(Guid id);
	Task<RubricEvaluationDto> UpsertEvaluationAsync(Guid rubricId, UpsertRubricEvaluationDto input);
	Task<List<RubricEvaluationDto>> GetEvaluationsAsync(Guid rubricId);
	Task<RubricEvaluationDto?> GetStudentAssignmentEvaluationAsync(Guid studentId, Guid assignmentId);
	Task SubmitEvaluationAsync(Guid evaluationId);
	Task ApproveEvaluationAsync(Guid evaluationId);
}
