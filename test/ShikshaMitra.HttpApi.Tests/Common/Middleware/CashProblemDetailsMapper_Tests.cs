using Microsoft.AspNetCore.Http;
using ShikshaMitra.Common.Middleware;
using ShikshaMitra.Finance.Accounts.CashManagement;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Common.Tests;

public class CashProblemDetailsMapper_Tests
{
    [Theory]
    [InlineData(CashManagementErrorCodes.Stale)]
    [InlineData(CashManagementErrorCodes.RetryConflict)]
    [InlineData(CashManagementErrorCodes.InvalidState)]
    [InlineData(CashManagementErrorCodes.InsufficientCash)]
    [InlineData(CashManagementErrorCodes.Immutable)]
    public void Cash_conflicts_are_recoverable_HTTP_409(string code) =>
        ApiProblemDetailsMapper.ResolveBusinessStatusCode(code).ShouldBe(StatusCodes.Status409Conflict);
}
