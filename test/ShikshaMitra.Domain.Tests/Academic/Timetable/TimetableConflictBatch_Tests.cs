using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using ShikshaMitra.Academic.Curriculum.Settings;
using ShikshaMitra.Academic.Timetable.Timetables;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.RoomsManagement.Rooms;
using ShikshaMitra.TenantSettings.Standards;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace ShikshaMitra.Academic.Timetable;

public class TimetableConflictBatch_Tests
{
	private readonly ITimetableRepository _repository = Substitute.For<ITimetableRepository>();
	private readonly Guid _teacherId = Guid.NewGuid();
	private readonly Guid _roomId = Guid.NewGuid();
	private readonly TimetableManager _manager;

	public TimetableConflictBatch_Tests()
	{
		_manager = new TimetableManager(_repository, EmptyRepository<Standard>(), EmptyRepository<Division>(),
			EmptyRepository<Subject>(), EmptyRepository<Room>(), EmptyRepository<Employee>());
		_repository.GetConflictCandidatesAsync(Arg.Any<Guid>(), Arg.Any<Guid>(),
			Arg.Any<IReadOnlyCollection<TimetableDayOfWeek>>(), Arg.Any<TimeOnly>(), Arg.Any<TimeOnly>(),
			Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<IReadOnlyCollection<Guid>>())
			.Returns(new List<TimetableConflictCandidate>());
	}

	[Theory]
	[InlineData(1)]
	[InlineData(40)]
	[InlineData(80)]
	public async Task Validation_Should_Read_Candidates_Once_Regardless_Of_Period_Count(int periodCount)
	{
		var slots = Enumerable.Range(0, periodCount)
			.Select(index => CreateSlot(TimetableDayOfWeek.Monday, new TimeOnly(8, 0).AddMinutes(index * 5), 5))
			.ToList();

		await _manager.CheckConflictsAsync(Guid.NewGuid(), Guid.NewGuid(), slots);

		await _repository.Received(1).GetConflictCandidatesAsync(Arg.Any<Guid>(), Arg.Any<Guid>(),
			Arg.Is<IReadOnlyCollection<TimetableDayOfWeek>>(days => days.Count == 1), Arg.Any<TimeOnly>(), Arg.Any<TimeOnly>(),
			Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.Count == 1 && ids.Contains(_teacherId)),
			Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.Count == 1 && ids.Contains(_roomId)));
		await _repository.DidNotReceive().GetAllWithSlotsAsync();
	}

	[Theory]
	[InlineData("en-US", "09:15", "10:00")]
	[InlineData("fi-FI", "09.15", "10.00")]
	public async Task Validation_Should_Preserve_Teacher_First_Error_And_Conflict_Details(
		string culture, string expectedStartTime, string expectedEndTime)
	{
		var previousCulture = CultureInfo.CurrentCulture;
		try
		{
			CultureInfo.CurrentCulture = new CultureInfo(culture, useUserOverride: false);
			var candidate = Candidate(TimetableDayOfWeek.Monday, new TimeOnly(9, 15), new TimeOnly(10, 0));
			SetCandidates(candidate);

			var error = await Should.ThrowAsync<BusinessException>(() => _manager.CheckConflictsAsync(
				Guid.NewGuid(), Guid.NewGuid(), [CreateSlot(TimetableDayOfWeek.Monday, new TimeOnly(9, 0), 30)]));

			error.Code.ShouldBe(TimetableErrorCodes.TeacherConflict);
			error.Data["message"].ShouldBe(
				$"Teacher conflict: selected teacher is already assigned to another class Division another division for assigned subject on Monday {expectedStartTime}-{expectedEndTime}.");
			using var json = JsonDocument.Parse((string)error.Data["ConflictDetails"]!);
			var detail = json.RootElement[0];
			detail.GetProperty("Type").GetString().ShouldBe("teacher");
			detail.GetProperty("ResourceId").GetGuid().ShouldBe(_teacherId);
			detail.GetProperty("ConflictingStandardId").GetGuid().ShouldBe(candidate.StandardId);
			detail.GetProperty("ConflictingDivisionId").GetGuid().ShouldBe(candidate.DivisionId);
		}
		finally
		{
			CultureInfo.CurrentCulture = previousCulture;
		}
	}

	[Fact]
	public async Task Validation_Should_Ignore_Other_Days_And_Adjacent_Periods()
	{
		SetCandidates(
			Candidate(TimetableDayOfWeek.Tuesday, new TimeOnly(9, 0), new TimeOnly(10, 0)),
			Candidate(TimetableDayOfWeek.Monday, new TimeOnly(8, 0), new TimeOnly(9, 0)),
			Candidate(TimetableDayOfWeek.Monday, new TimeOnly(10, 0), new TimeOnly(11, 0)));

		await _manager.CheckConflictsAsync(Guid.NewGuid(), Guid.NewGuid(),
			[CreateSlot(TimetableDayOfWeek.Monday, new TimeOnly(9, 0), 60)]);
	}

	[Theory]
	[InlineData("en-US", "09:00")]
	[InlineData("fi-FI", "09.00")]
	public async Task Validation_Should_Report_First_Submitted_Period_Before_Later_Teacher_Conflict(
		string culture, string expectedStartTime)
	{
		var previousCulture = CultureInfo.CurrentCulture;
		try
		{
			CultureInfo.CurrentCulture = new CultureInfo(culture, useUserOverride: false);
			SetCandidates(
				Candidate(TimetableDayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(10, 0)) with { TeacherId = null },
				Candidate(TimetableDayOfWeek.Monday, new TimeOnly(10, 0), new TimeOnly(11, 0)));

			var error = await Should.ThrowAsync<BusinessException>(() => _manager.CheckConflictsAsync(
				Guid.NewGuid(), Guid.NewGuid(),
				[CreateSlot(TimetableDayOfWeek.Monday, new TimeOnly(9, 0), 60),
				 CreateSlot(TimetableDayOfWeek.Monday, new TimeOnly(10, 0), 60)]));

			error.Code.ShouldBe(TimetableErrorCodes.RoomConflict);
			error.Data["StartTime"].ShouldBe(expectedStartTime);
		}
		finally
		{
			CultureInfo.CurrentCulture = previousCulture;
		}
	}

	[Fact]
	public async Task Validation_Should_Not_Query_For_Periods_Without_Resources()
	{
		var slot = new TimetableSlot(Guid.NewGuid(), Guid.NewGuid(), TimetableDayOfWeek.Monday,
			1, new TimeOnly(9, 0), new TimeOnly(10, 0), SlotType.Period);
		slot.AddSubject(new TimetableSlotSubject(Guid.NewGuid(), slot.Id, Guid.NewGuid()));

		await _manager.CheckConflictsAsync(Guid.NewGuid(), Guid.NewGuid(), [slot]);

		await _repository.DidNotReceiveWithAnyArgs().GetConflictCandidatesAsync(
			default, default, [], default, default, [], []);
	}

	private TimetableSlot CreateSlot(TimetableDayOfWeek day, TimeOnly start, int duration)
	{
		var slot = new TimetableSlot(Guid.NewGuid(), Guid.NewGuid(), day, 1, start, start.AddMinutes(duration), SlotType.Period);
		slot.AddSubject(new TimetableSlotSubject(Guid.NewGuid(), slot.Id, Guid.NewGuid(), _teacherId, _roomId));
		return slot;
	}

	private TimetableConflictCandidate Candidate(TimetableDayOfWeek day, TimeOnly start, TimeOnly end) =>
		new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), day, start, end, _teacherId, _roomId);

	private void SetCandidates(params TimetableConflictCandidate[] candidates) =>
		_repository.GetConflictCandidatesAsync(Arg.Any<Guid>(), Arg.Any<Guid>(),
			Arg.Any<IReadOnlyCollection<TimetableDayOfWeek>>(), Arg.Any<TimeOnly>(), Arg.Any<TimeOnly>(),
			Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<IReadOnlyCollection<Guid>>()).Returns(candidates.ToList());

	private static IRepository<TEntity, Guid> EmptyRepository<TEntity>() where TEntity : class, Volo.Abp.Domain.Entities.IEntity<Guid>
	{
		var repository = Substitute.For<IRepository<TEntity, Guid>>();
		repository.GetListAsync(Arg.Any<Expression<Func<TEntity, bool>>>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
			.Returns(new List<TEntity>());
		return repository;
	}
}
