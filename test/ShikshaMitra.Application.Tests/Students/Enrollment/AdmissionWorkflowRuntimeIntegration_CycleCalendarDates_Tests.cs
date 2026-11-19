using System;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Security.UserScope;
using Shouldly;
using Volo.Abp.Json;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Fact]
	public async Task Cycle_calendar_dates_round_trip_through_abp_serialization_persistence_and_governed_commands()
	{
		using var tenant = _currentTenant.Change(TestTenantId);
		using var principal = _principalAccessor.Change(BuildAdminPrincipal());
		await EnsureTestIdentityUserAsync(TestUserId, $"workflow-admin-{TestUserId:N}");
		await _scopeRepository.InsertAsync(new UserScopeAssignment(Guid.NewGuid(), TestTenantId,
			userId: TestUserId, description: "Calendar-date regression"), autoSave: true);
		var stageId = await EmployeeOrganizationTestHelper.EnsureOrganizationExistsAsync(ServiceProvider);
		var stage = await _stageRepository.GetAsync(stageId);
		var medium = await _mediumRepository.GetAsync(stage.EducationMediumId);
		var board = await _boardRepository.GetAsync(medium.EducationBoardId);
		var service = GetRequiredService<IAdmissionCycleAppService>();
		var serializer = GetRequiredService<IJsonSerializer>();
		var input = serializer.Deserialize<CreateUpdateAdmissionCycleDto>("""
			{"name":"Calendar dates","startDate":"2026-06-01T00:00:00+05:30","endDate":"2027-03-31T00:00:00+05:30"}
			""");
		input.AcademicYearId = board.AcademicYearId;
		var created = await service.CreateAsync(input);
		await AssertStoredCalendarDates(created.Id, "2026-06-01", "2027-03-31");

		input.ConcurrencyStamp = created.ConcurrencyStamp;
		input.EndDate = new DateTime(2037, 3, 31, 0, 0, 0, DateTimeKind.Utc);
		var edited = await service.UpdateAsync(created.Id, input);
		await AssertStoredCalendarDates(edited.Id, "2026-06-01", "2037-03-31");
		var cloneInput = serializer.Deserialize<CloneAdmissionCycleDto>("""
			{"name":"Clone calendar dates","startDate":"2026-06-01T23:30:00-07:00","endDate":"2037-03-31T00:00:00+05:30"}
			""");
		cloneInput.AcademicYearId = board.AcademicYearId;
		cloneInput.SourceCycleId = edited.Id;
		var clone = await service.CloneAsync(cloneInput);
		await AssertStoredCalendarDates(clone.Id, "2026-06-01", "2037-03-31");

		var publishAt = DateTime.UtcNow.AddDays(1);
		var closeAt = publishAt.AddDays(1);
		var scheduled = await service.ScheduleAsync(edited.Id, serializer.Deserialize<ScheduleAdmissionCycleDto>(
			JsonSerializer.Serialize(new { concurrencyStamp = edited.ConcurrencyStamp,
				publishAt = new DateTimeOffset(publishAt).ToOffset(TimeSpan.FromHours(5.5)),
				closeAt = new DateTimeOffset(closeAt).ToOffset(TimeSpan.FromHours(5.5)) })));
		scheduled.ScheduledPublishAt!.Value.ToUniversalTime().ShouldBe(publishAt);
		scheduled.ScheduledCloseAt!.Value.ToUniversalTime().ShouldBe(closeAt);

		var published = await service.PublishAsync(edited.Id, scheduled.ConcurrencyStamp);
		var revision = serializer.Deserialize<UpdateAdmissionCycleWindowDto>("""
			{"startDate":"2026-06-02T00:00:00+05:30","endDate":"2037-03-30T00:00:00+05:30","reason":"Approved date revision"}
			""");
		revision.ConcurrencyStamp = published.ConcurrencyStamp;
		var revised = await service.UpdateWindowAsync(edited.Id, revision);
		await AssertStoredCalendarDates(revised.Id, "2026-06-02", "2037-03-30");
		var closed = await service.CloseAsync(revised.Id, new AdmissionCycleActionDto
		{
			ConcurrencyStamp = revised.ConcurrencyStamp, Reason = "Temporary closure"
		});
		var reopen = serializer.Deserialize<ReopenAdmissionCycleDto>("""
			{"newEndDate":"2037-03-31T00:00:00+05:30","reason":"Approved reopening"}
			""");
		reopen.ConcurrencyStamp = closed.ConcurrencyStamp;
		var reopened = await service.ReopenAsync(closed.Id, reopen);
		reopened.Status.ShouldBe(AdmissionCycleStatus.Published);
		await AssertStoredCalendarDates(reopened.Id, "2026-06-02", "2037-03-31");

		async Task AssertStoredCalendarDates(Guid id, string expectedStart, string expectedEnd)
		{
			var saved = await WithUnitOfWorkAsync(() => service.GetAsync(id));
			saved.StartDate.ToString("yyyy-MM-dd").ShouldBe(expectedStart);
			saved.EndDate.ToString("yyyy-MM-dd").ShouldBe(expectedEnd);
			using var response = JsonDocument.Parse(serializer.Serialize(saved));
			response.RootElement.GetProperty("startDate").GetString().ShouldBe(expectedStart + "T00:00:00Z");
			response.RootElement.GetProperty("endDate").GetString().ShouldBe(expectedEnd + "T00:00:00Z");
		}
	}
}
