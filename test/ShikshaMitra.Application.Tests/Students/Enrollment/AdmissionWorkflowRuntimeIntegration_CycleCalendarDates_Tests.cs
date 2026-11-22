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
		var today = AdmissionCycleWindowEvaluator.GetInstitutionDate(DateTime.UtcNow);
		var start = today.AddDays(1).ToString("yyyy-MM-dd");
		var revisedStart = today.AddDays(2).ToString("yyyy-MM-dd");
		var end = today.AddDays(90).ToString("yyyy-MM-dd");
		var revisedEnd = today.AddDays(100).ToString("yyyy-MM-dd");
		var input = serializer.Deserialize<CreateUpdateAdmissionCycleDto>($$"""
			{"name":"Calendar dates","startDate":"{{start}}T00:00:00+05:30","endDate":"{{end}}T00:00:00+05:30"}
			""");
		input.AcademicYearId = board.AcademicYearId;
		var created = await service.CreateAsync(input);
		await AssertStoredCalendarDates(created.Id, start, end);

		input.ConcurrencyStamp = created.ConcurrencyStamp;
		input.EndDate = DateTime.SpecifyKind(today.AddDays(100), DateTimeKind.Utc);
		var edited = await service.UpdateAsync(created.Id, input);
		await AssertStoredCalendarDates(edited.Id, start, revisedEnd);
		var cloneInput = serializer.Deserialize<CloneAdmissionCycleDto>($$"""
			{"name":"Clone calendar dates","startDate":"{{start}}T23:30:00-07:00","endDate":"{{revisedEnd}}T00:00:00+05:30"}
			""");
		cloneInput.AcademicYearId = board.AcademicYearId;
		cloneInput.SourceCycleId = edited.Id;
		var clone = await service.CloneAsync(cloneInput);
		await AssertStoredCalendarDates(clone.Id, start, revisedEnd);

		var publishAt = DateTime.UtcNow.AddDays(1);
		var closeAt = publishAt.AddDays(1);
		var scheduled = await service.ScheduleAsync(edited.Id, serializer.Deserialize<ScheduleAdmissionCycleDto>(
			JsonSerializer.Serialize(new { concurrencyStamp = edited.ConcurrencyStamp,
				publishAt = new DateTimeOffset(publishAt).ToOffset(TimeSpan.FromHours(5.5)),
				closeAt = new DateTimeOffset(closeAt).ToOffset(TimeSpan.FromHours(5.5)) })));
		scheduled.ScheduledPublishAt!.Value.ToUniversalTime().ShouldBe(publishAt);
		scheduled.ScheduledCloseAt!.Value.ToUniversalTime().ShouldBe(closeAt);

		var published = await service.PublishAsync(edited.Id, scheduled.ConcurrencyStamp);
		var revision = serializer.Deserialize<UpdateAdmissionCycleWindowDto>($$"""
			{"startDate":"{{revisedStart}}T00:00:00+05:30","endDate":"{{end}}T00:00:00+05:30","reason":"Approved date revision"}
			""");
		revision.ConcurrencyStamp = published.ConcurrencyStamp;
		var revised = await service.UpdateWindowAsync(edited.Id, revision);
		await AssertStoredCalendarDates(revised.Id, revisedStart, end);
		var closed = await service.CloseAsync(revised.Id, new AdmissionCycleActionDto
		{
			ConcurrencyStamp = revised.ConcurrencyStamp, Reason = "Temporary closure"
		});
		var reopen = serializer.Deserialize<ReopenAdmissionCycleDto>($$"""
			{"newEndDate":"{{revisedEnd}}T00:00:00+05:30","reason":"Approved reopening"}
			""");
		reopen.ConcurrencyStamp = closed.ConcurrencyStamp;
		var reopened = await service.ReopenAsync(closed.Id, reopen);
		reopened.Status.ShouldBe(AdmissionCycleStatus.Published);
		await AssertStoredCalendarDates(reopened.Id, revisedStart, revisedEnd);

		var historicalStart = DateTime.SpecifyKind(today.AddDays(-30), DateTimeKind.Utc);
		var retainedCycle = new AdmissionCycle(Guid.NewGuid(), TestTenantId, "Retained historical intake",
			"HISTORICAL-INTAKE", board.AcademicYearId, historicalStart, DateTime.SpecifyKind(today.AddDays(30), DateTimeKind.Utc));
		retainedCycle.Publish(TestUserId, DateTime.UtcNow);
		await WithUnitOfWorkAsync(() => _cycleRepository.InsertAsync(retainedCycle, autoSave: true));
		var retainStart = new UpdateAdmissionCycleWindowDto
		{
			StartDate = historicalStart, EndDate = DateTime.SpecifyKind(today.AddDays(60), DateTimeKind.Utc),
			Reason = "Extend the current window", ConcurrencyStamp = retainedCycle.ConcurrencyStamp
		};
		var retained = await service.UpdateWindowAsync(retainedCycle.Id, retainStart);
		retained.StartDate.ShouldBe(historicalStart);
		retained.EndDate.ShouldBe(retainStart.EndDate);
		retainStart.StartDate = historicalStart.AddDays(1);
		retainStart.ConcurrencyStamp = retained.ConcurrencyStamp;
		(await Should.ThrowAsync<Volo.Abp.BusinessException>(() => service.UpdateWindowAsync(retainedCycle.Id, retainStart)))
			.Code.ShouldBe(AdmissionErrorCodes.CycleWindowDateTooEarly);

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
