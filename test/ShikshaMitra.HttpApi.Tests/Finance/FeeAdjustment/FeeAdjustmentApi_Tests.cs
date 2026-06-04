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
    public async Task Get_Eligibility_Should_Return_Ok()
    {
        var response = await Client.GetAsync(
            $"{BaseUrl}/eligibility?studentId={Guid.Empty}&academicYearId={Guid.Empty}");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
