using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
    [Fact]
    public Task Queue_column_filters_use_full_server_population_before_count_and_paging() =>
        RunCertificationScenarioAsync(async () =>
        {
            var first = await CreateAcademicFilterFixtureAsync();
            await CreateAcademicFilterFixtureAsync();
            var application = await _applicationAppService.GetAsync(first.ApplicationId);
            var filters = System.Text.Json.JsonSerializer.Serialize(new[] { new { column = "standardName", type = "equals", value = application.StandardName, valueType = "string" } });
            var applications = await _applicationAppService.GetListAsync(new() { ColumnFilters = filters, MaxResultCount = 1 });
            applications.TotalCount.ShouldBe(1);
            applications.Items.Single().Id.ShouldBe(first.ApplicationId);
            var pipeline = await _applicationAppService.GetPipelineAsync(new() { ColumnFilters = filters, MyCases = false, MaxResultCount = 1 });
            pipeline.TotalCount.ShouldBe(1);
            pipeline.Items.Single().Id.ShouldBe(first.ApplicationId);
            var inquiries = await GetRequiredService<IAdmissionInquiryAppService>().GetListAsync(new() { StandardName = application.StandardName, StandardNameFilterType = "equals", MaxResultCount = 1 });
            inquiries.TotalCount.ShouldBe(1);
            inquiries.Items.Single().StandardName.ShouldBe(application.StandardName);
        });
}
