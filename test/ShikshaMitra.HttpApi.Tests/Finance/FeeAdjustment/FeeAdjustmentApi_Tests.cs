using System;
using System.Net;
using System.Threading.Tasks;
using ShikshaMitra.Finance.FeeRefundTests;
using ShikshaMitra.Infrastructure.Traits;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Finance.FeeAdjustment;

[Collection(ApiTestCollection.Name)]
[TestModule(TestTraits.Modules.Finance)]
public class FeeAdjustmentApi_Tests : ShikshaMitraApiTestBase
{
    private const string BaseUrl = "/api/fee-adjustments";

    public FeeAdjustmentApi_Tests(ShikshaMitraApiTestFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task Get_Eligibility_With_Empty_Student_And_Year_Should_Be_Forbidden()
    {
        var response = await Client.GetAsync(
            $"{BaseUrl}/eligibility?studentId={Guid.Empty}&academicYearId={Guid.Empty}");
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}
