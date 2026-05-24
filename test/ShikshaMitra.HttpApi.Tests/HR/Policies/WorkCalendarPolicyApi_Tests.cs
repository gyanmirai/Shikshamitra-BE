using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using ShikshaMitra.HR.Policies;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Infrastructure.Traits;
using ShikshaMitra.TenantSettings.AcademicYears;
using Shouldly;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace ShikshaMitra.HR.Policies;

[Collection(ApiTestCollection.Name)]
[TestModule(TestTraits.Modules.HR)]
public class WorkCalendarPolicyApi_Tests : ShikshaMitraApiTestBase
{
	private const string BaseUrl = "/api/hr/work-calendar-policies";

	public WorkCalendarPolicyApi_Tests(ShikshaMitraApiTestFactory factory) : base(factory) { }

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Holiday_Import_Preview_Should_Not_Return_404()
	{
		var response = await Client.GetAsync($"{BaseUrl}/holiday-import-preview?academicYearId={Factory.SeededAcademicYearId}");

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var body = await response.Content.ReadFromJsonAsync<List<WorkCalendarHolidayDto>>(JsonOptions);
		body.ShouldNotBeNull();
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Work_Calendar_Should_Persist_Manual_Holiday_Range()
	{
		var academicYear = await GetAcademicYearDateRangeAsync(Factory.SeededAcademicYearId);
		var fromDate = academicYear.StartDate.AddMonths(1).Date;
		var toDate = fromDate.AddDays(4);
		var request = new CreateUpdateWorkCalendarPolicyDto
		{
			AcademicYearId = Factory.SeededAcademicYearId,
			Name = $"Range Calendar {Guid.NewGuid():N}",
			Status = "draft",
			EffectiveFrom = academicYear.StartDate,
			EffectiveTo = academicYear.EndDate,
			WeeklyOffPattern = "sunday",
			CustomWeeklyOffs = [0],
			HolidaySyncMode = "snapshot",
			Holidays =
			[
				new WorkCalendarHolidayDto
				{
					AcademicYearId = Factory.SeededAcademicYearId,
					Date = fromDate,
					FromDate = fromDate,
					ToDate = toDate,
					HolidayName = "Summer Vacation",
					Source = "manual",
					IsIncluded = true,
					Notes = "Range holiday API test"
				}
			]
		};

		var createResponse = await Client.PostAsJsonAsync(BaseUrl, request, JsonOptions);
		createResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
		var created = await createResponse.Content.ReadFromJsonAsync<WorkCalendarPolicyDto>(JsonOptions);
		created.ShouldNotBeNull();

		var holidaysResponse = await Client.GetAsync($"{BaseUrl}/{created!.Id}/holidays");
		holidaysResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
		var holidays = await holidaysResponse.Content.ReadFromJsonAsync<List<WorkCalendarHolidayDto>>(JsonOptions);
		var range = holidays.ShouldNotBeNull().ShouldHaveSingleItem();
		range.FromDate?.Date.ShouldBe(fromDate);
		range.ToDate?.Date.ShouldBe(toDate);
		range.DayCount.ShouldBe(5);
	}

	private async Task<(DateTime StartDate, DateTime EndDate)> GetAcademicYearDateRangeAsync(Guid academicYearId)
	{
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var academicYearRepository = scope.ServiceProvider.GetRequiredService<IRepository<AcademicYear, Guid>>();

		using (currentTenant.Change(Guid.Parse(TestAuthHandler.TestTenantId)))
		{
			var academicYear = await academicYearRepository.GetAsync(academicYearId);
			return (academicYear.StartDate.Date, academicYear.EndDate.Date);
		}
	}
}
