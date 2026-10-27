using System;
using System.Linq;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Students.Enrollment;

public class AdmissionQueueColumnFilters_Tests
{
    [Fact]
    public void Pipeline_text_predicates_apply_before_total_and_page_and_preserve_scope()
    {
        var rows = new[] {
            new AdmissionPipelineItemDto { Id = Guid.NewGuid(), ApplicantName = "Riya", StandardName = "Class 6", ContactEmail = "parent@example.test" },
            new AdmissionPipelineItemDto { Id = Guid.NewGuid(), ApplicantName = "Riya B", StandardName = "Class 6", ContactEmail = "parent@example.test" },
            new AdmissionPipelineItemDto { Id = Guid.NewGuid(), ApplicantName = "Other", StandardName = "Class 7" },
        }.AsQueryable();
        var scoped = rows.Where(row => row.ApplicantName != "Riya B");
        const string filters = """[{"column":"standardName","type":"equals","value":"CLASS 6","valueType":"string"},{"column":"contactEmail","type":"endsWith","value":"EXAMPLE.TEST","valueType":"string"}]""";
        var query = AdmissionQueueColumnFilters.Pipeline(scoped, filters);
        query.Count().ShouldBe(1);
        query.Take(1).Single().ApplicantName.ShouldBe("Riya");
        AdmissionQueueColumnFilters.Pipeline(rows, filters).Count().ShouldBe(2);
        AdmissionQueueColumnFilters.Pipeline(rows, filters).Skip(1).Take(1).Single().ApplicantName.ShouldBe("Riya B");
    }

    [Fact]
    public void Local_day_filter_includes_prior_utc_evening_and_excludes_next_local_day_and_null()
    {
        var rows = new[] {
            new AdmissionApplicationListRow { ApplicantName = "start", DecisionEnteredAt = new DateTime(2026, 10, 25, 18, 30, 0, DateTimeKind.Utc) },
            new AdmissionApplicationListRow { ApplicantName = "end", DecisionEnteredAt = new DateTime(2026, 10, 26, 18, 30, 0, DateTimeKind.Utc) },
            new AdmissionApplicationListRow { ApplicantName = "missing", DecisionEnteredAt = null },
        }.AsQueryable();
        const string filters = """[{"column":"decisionEnteredAt","type":"equals","value":"26-10-2026","valueType":"date","startOffsetMinutes":-330,"endOffsetMinutes":-330}]""";
        AdmissionQueueColumnFilters.Applications(rows, filters).Single().ApplicantName.ShouldBe("start");
        AdmissionQueueColumnFilters.Applications(rows, filters.Replace("equals", "after")).Single().ApplicantName.ShouldBe("end");
        AdmissionQueueColumnFilters.Applications(rows, filters.Replace("equals", "before")).Count().ShouldBe(0);
    }

    [Fact]
    public void Day_boundaries_support_dst_offset_changes()
    {
        var row = new AdmissionApplicationListRow { DecisionEnteredAt = new DateTime(2026, 3, 9, 3, 59, 0, DateTimeKind.Utc) };
        const string filters = """[{"column":"decisionEnteredAt","type":"equals","value":"08-03-2026","valueType":"date","startOffsetMinutes":300,"endOffsetMinutes":240}]""";
        AdmissionQueueColumnFilters.Applications(new[] { row }.AsQueryable(), filters).Count().ShouldBe(1);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("[{\"column\":\"currentStageName\",\"type\":\"contains\",\"value\":\"x\",\"valueType\":\"string\"}]")]
    [InlineData("[{\"column\":\"standardName\",\"type\":\"unsafe\",\"value\":\"x\",\"valueType\":\"string\"}]")]
    [InlineData("[{\"column\":\"decisionEnteredAt\",\"type\":\"equals\",\"value\":\"26-10-2026\",\"valueType\":\"date\"}]")]
    [InlineData("[{\"column\":\"decisionEnteredAt\",\"type\":\"equals\",\"value\":\"26-10-2026\",\"valueType\":\"date\",\"startOffsetMinutes\":9999,\"endOffsetMinutes\":0}]")]
    public void Rejects_unknown_composite_or_malformed_filters(string filters) =>
        Should.Throw<BusinessException>(() => AdmissionQueueColumnFilters.Applications(Array.Empty<AdmissionApplicationListRow>().AsQueryable(), filters));
}
