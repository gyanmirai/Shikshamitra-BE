using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.AcademicSetups.AcademicYears;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace ShikshaMitra.Finance.Accounts;

internal static class CashTestDates
{
    public static async Task EnsureCurrentYearAsync(HttpClient client, Guid yearId, JsonSerializerOptions json)
    {
        var year = (await client.GetFromJsonAsync<AcademicYearDto>($"/api/app/academic-year/{yearId}", json))!;
        var today = DateTime.UtcNow.Date;
        if (today >= year.StartDate.Date && today <= year.EndDate.Date) return;
        // Collection timestamps come from the server clock. Keep the synthetic
        // hierarchy IDs, but make its test period contain that actual clock.
        var response = await client.PutAsJsonAsync($"/api/app/academic-year/{yearId}", new CreateUpdateAcademicYearDto
        {
            Name = year.Name,
            StartYear = new DateTime(today.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            EndYear = new DateTime(today.Year, 12, 31, 0, 0, 0, DateTimeKind.Utc)
        }, json);
        response.EnsureSuccessStatusCode();
    }

    public static async Task<DateTimeOffset> InYearAsync(IServiceProvider services, Guid yearId)
    {
        using var scope = services.CreateScope();
        using var tenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(Guid.Parse(TestAuthHandler.TestTenantId));
        using var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true);
        var year = await scope.ServiceProvider.GetRequiredService<IRepository<AcademicYear, Guid>>().GetAsync(yearId);
        var period = AccountsRegisterPeriod.Resolve(year.StartDate, year.EndDate, null, null);
        var recent = DateTimeOffset.UtcNow.AddMinutes(-10);
        var actual = recent.UtcDateTime >= period.StartUtc && recent.UtcDateTime < period.EndUtc
            ? recent : new DateTimeOffset(DateTime.SpecifyKind(period.StartUtc.AddHours(12), DateTimeKind.Utc));
        await uow.CompleteAsync();
        return actual;
    }
}
