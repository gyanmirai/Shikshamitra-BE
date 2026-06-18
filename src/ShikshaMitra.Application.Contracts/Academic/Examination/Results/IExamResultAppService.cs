using System;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Examination.Results.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Academic.Examination.Results;

public interface IExamResultAppService : IApplicationService
{
    /// <summary>
    /// Checks whether all prerequisites for result generation are met
    /// (marks verified, grading scale set, etc.).
    /// </summary>
    Task<ResultReadinessDto> GetReadinessAsync(Guid examId, Guid divisionId);

    /// <summary>
    /// Generates (or regenerates) the result for a given Exam + Division.
    /// Creates ExamResultHeader + ExamResultDetail rows, computes ranks.
    /// </summary>
    Task<ResultHeaderDto> GenerateAsync(GenerateResultRequestDto input);

    /// <summary>
    /// Publishes the result (Generated → Published).
    /// Makes results visible to students/parents.
    /// </summary>
    Task<ResultHeaderDto> PublishAsync(PublishResultRequestDto input);

    /// <summary>
    /// Freezes the result (Published → Frozen).
    /// Prevents further changes.
    /// </summary>
    Task<ResultHeaderDto> FreezeAsync(FreezeResultRequestDto input);

    /// <summary>
    /// Returns aggregate analytics for a result header.
    /// </summary>
    Task<ResultAnalyticsDto> GetAnalyticsAsync(Guid resultHeaderId);

    /// <summary>
    /// Fetches the current result header for an Exam + Division, or null if not yet generated.
    /// </summary>
    Task<ResultHeaderDto?> GetHeaderAsync(Guid examId, Guid divisionId);

    /// <summary>
    /// Fetches all values for viewing a student result.
    /// </summary>
    Task<StudentResultViewDto> GetStudentResultDetailsAsync(Guid examId, Guid studentId);
}
