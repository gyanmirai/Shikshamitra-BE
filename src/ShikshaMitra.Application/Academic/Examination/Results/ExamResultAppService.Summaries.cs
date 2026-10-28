using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Examination.Results.Dtos;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Academic.Examination.Results;

public partial class ExamResultAppService
{
    private async Task<ResultSummary> ReadResultSummaryAsync(Guid headerId)
    {
        var query = (await resultDetailRepository.GetQueryableAsync()).Where(detail => detail.ResultHeaderId == headerId);
        return await AsyncExecuter.FirstOrDefaultAsync(query.GroupBy(_ => 1).Select(group => new ResultSummary
        {
            TotalStudents = group.Count(),
            Appeared = group.Count(detail => detail.ResultStatus != ExamStudentResultStatus.Absent),
            Passed = group.Count(detail => detail.ResultStatus == ExamStudentResultStatus.Pass),
            Failed = group.Count(detail => detail.ResultStatus == ExamStudentResultStatus.Fail),
            Compartment = group.Count(detail => detail.ResultStatus == ExamStudentResultStatus.Compartment),
            Absent = group.Count(detail => detail.ResultStatus == ExamStudentResultStatus.Absent),
            Highest = group.Max(detail => detail.ResultStatus != ExamStudentResultStatus.Absent ? (double?)detail.Percentage : null) ?? 0,
            Lowest = group.Min(detail => detail.ResultStatus != ExamStudentResultStatus.Absent ? (double?)detail.Percentage : null) ?? 0,
            Average = group.Average(detail => detail.ResultStatus != ExamStudentResultStatus.Absent ? (double?)detail.Percentage : null) ?? 0
        })) ?? new ResultSummary();
    }

    private static ResultHeaderDto MapToHeaderDto(ExamResultHeader header, ResultSummary summary) => new()
    {
        Id = header.Id, ExamId = header.ExamId, DivisionId = header.DivisionId,
        Status = header.Status.ToString(), StatusCode = (int)header.Status,
        GeneratedOn = header.GeneratedOn, PublishedOn = header.PublishedOn,
        FrozenOn = header.FrozenOn, Remarks = header.Remarks,
        TotalStudents = summary.TotalStudents, Appeared = summary.Appeared,
        Passed = summary.Passed, Failed = summary.Failed, Compartment = summary.Compartment,
        Absent = summary.Absent, PassPercentage = summary.PassPercentage,
        HighestPercentage = summary.Highest, AveragePercentage = summary.AveragePercentage
    };

    private sealed class ResultSummary
    {
        public int TotalStudents { get; init; }
        public int Appeared { get; init; }
        public int Passed { get; init; }
        public int Failed { get; init; }
        public int Compartment { get; init; }
        public int Absent { get; init; }
        public double Highest { get; init; }
        public double Lowest { get; init; }
        public double Average { get; init; }
        public double PassPercentage => Appeared > 0 ? Math.Round((double)(Passed + Compartment) / Appeared * 100, 1) : 0;
        public double AveragePercentage => Appeared > 0 ? Math.Round(Average, 1) : 0;
    }
}
