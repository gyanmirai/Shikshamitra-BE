using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Academic.Curriculum.Settings;
using ShikshaMitra.Academic.Examination.Exams;
using ShikshaMitra.Academic.Examination.SeatAllocation.Dtos;
using ShikshaMitra.Academic.Examination.Settings;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Students;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using ShikshaMitra.TenantSettings.RoomsManagement.Rooms;
using ShikshaMitra.TenantSettings.RoomsManagement.Buildings;
using ShikshaMitra.TenantSettings.RoomsManagement.RoomTypes;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace ShikshaMitra.Academic.Examination.SeatAllocation;

public partial class ExamSeatAllocationAppService_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	private static readonly Guid TestTenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
	private static readonly Guid FakeAdminUserId = Guid.Parse("2e701e62-0953-4dd3-910b-dc6cc93ccb0d");

	private readonly IExamSeatAllocationAppService _seatAllocationAppService;
	private readonly IRepository<EducationMediumStage, Guid> _stageRepository;
	private readonly IRepository<Standard, Guid> _standardRepository;
	private readonly IRepository<Division, Guid> _divisionRepository;
	private readonly IRepository<EducationMedium, Guid> _educationMediumRepository;
	private readonly IRepository<EducationBoard, Guid> _educationBoardRepository;
	private readonly IRepository<Subject, Guid> _subjectRepository;
	private readonly IRepository<Exam, Guid> _examRepository;
	private readonly IRepository<Student, Guid> _studentRepository;
	private readonly IRepository<ExamSeatingPlan, Guid> _seatingPlanRepository;
	private readonly IRepository<ExamSeatingPlanSubject, Guid> _seatingPlanSubjectRepository;
	private readonly IRepository<ExamSeatAllocation, Guid> _seatAllocationRepository;
	private readonly IRepository<ExamSeatingPublication, Guid> _seatingPublicationRepository;
	private readonly IRepository<SeatingPlanAuditLog, Guid> _auditLogRepository;
	private readonly IRepository<IdentityUser, Guid> _identityUserRepository;
	private readonly IRepository<IdentityRole, Guid> _identityRoleRepository;
	private readonly IRepository<Building, Guid> _buildingRepository;
	private readonly IRepository<Floor, Guid> _floorRepository;
	private readonly IRepository<RoomType, Guid> _roomTypeRepository;
	private readonly IRepository<Room, Guid> _roomRepository;
	private readonly StandardManager _standardManager;
	private readonly DivisionManager _divisionManager;
	private readonly ExamTypeManager _examTypeManager;
	private readonly ExamManager _examManager;
	private readonly ICurrentTenant _currentTenant;

	public ExamSeatAllocationAppService_Tests()
	{
		_seatAllocationAppService = GetRequiredService<IExamSeatAllocationAppService>();
		_stageRepository = GetRequiredService<IRepository<EducationMediumStage, Guid>>();
		_standardRepository = GetRequiredService<IRepository<Standard, Guid>>();
		_divisionRepository = GetRequiredService<IRepository<Division, Guid>>();
		_educationMediumRepository = GetRequiredService<IRepository<EducationMedium, Guid>>();
		_educationBoardRepository = GetRequiredService<IRepository<EducationBoard, Guid>>();
		_subjectRepository = GetRequiredService<IRepository<Subject, Guid>>();
		_examRepository = GetRequiredService<IRepository<Exam, Guid>>();
		_studentRepository = GetRequiredService<IRepository<Student, Guid>>();
		_seatingPlanRepository = GetRequiredService<IRepository<ExamSeatingPlan, Guid>>();
		_seatingPlanSubjectRepository = GetRequiredService<IRepository<ExamSeatingPlanSubject, Guid>>();
		_seatAllocationRepository = GetRequiredService<IRepository<ExamSeatAllocation, Guid>>();
		_seatingPublicationRepository = GetRequiredService<IRepository<ExamSeatingPublication, Guid>>();
		_auditLogRepository = GetRequiredService<IRepository<SeatingPlanAuditLog, Guid>>();
		_identityUserRepository = GetRequiredService<IRepository<IdentityUser, Guid>>();
		_identityRoleRepository = GetRequiredService<IRepository<IdentityRole, Guid>>();
		_buildingRepository = GetRequiredService<IRepository<Building, Guid>>();
		_floorRepository = GetRequiredService<IRepository<Floor, Guid>>();
		_roomTypeRepository = GetRequiredService<IRepository<RoomType, Guid>>();
		_roomRepository = GetRequiredService<IRepository<Room, Guid>>();
		_standardManager = GetRequiredService<StandardManager>();
		_divisionManager = GetRequiredService<DivisionManager>();
		_examTypeManager = GetRequiredService<ExamTypeManager>();
		_examManager = GetRequiredService<ExamManager>();
		_currentTenant = GetRequiredService<ICurrentTenant>();
	}

	[Fact]
	public async Task GetSeatingPlanWorkspaceAsync_Should_Group_Subjects_In_The_Same_Session()
	{
		await WithUnitOfWorkAsync(async () =>
		{
			using (_currentTenant.Change(TestTenantId))
			{
				await EnsureAdminUserExistsAsync();
				var (exam, examSubjectIds) = await CreateSessionExamAsync(scheduleSecondSubject: true);

				var result = await _seatAllocationAppService.GetSeatingPlanWorkspaceAsync(exam.Id);

				result.Sessions.Count.ShouldBe(1);
				result.Sessions.Single().Subjects
					.Select(subject => subject.ExamSubjectId)
					.ToHashSet()
					.SetEquals(examSubjectIds)
					.ShouldBeTrue();
				result.Sessions.Single().HasCompleteSchedule.ShouldBeTrue();
			}
		});
	}

	[Fact]
	public async Task GetSeatingPlanWorkspaceAsync_Should_Block_An_Unscheduled_Subject()
	{
		await WithUnitOfWorkAsync(async () =>
		{
			using (_currentTenant.Change(TestTenantId))
			{
				await EnsureAdminUserExistsAsync();
				var (exam, _) = await CreateSessionExamAsync(scheduleSecondSubject: false);

				var result = await _seatAllocationAppService.GetSeatingPlanWorkspaceAsync(exam.Id);

				var unscheduledSession = result.Sessions.Single(session => !session.HasCompleteSchedule);
				unscheduledSession.CanPrepare.ShouldBeFalse();
				unscheduledSession.Issues.ShouldContain(issue => issue.Code == "INCOMPLETE_SCHEDULE");
			}
		});
	}

	[Fact]
	public async Task PrepareSessionSeatingPlanAsync_Should_Create_One_Plan_And_Return_It_On_Repeat()
	{
		await WithUnitOfWorkAsync(async () =>
		{
			using (_currentTenant.Change(TestTenantId))
			{
				await EnsureAdminUserExistsAsync();
				var (exam, examSubjectIds) = await CreateSessionExamAsync(scheduleSecondSubject: true);
				var input = new PrepareSessionSeatingPlanInputDto
				{
					Name = "Morning session",
					ExamSubjectIds = examSubjectIds
				};

				var first = await _seatAllocationAppService.PrepareSessionSeatingPlanAsync(exam.Id, input);
				var second = await _seatAllocationAppService.PrepareSessionSeatingPlanAsync(exam.Id, input);

				first.SelectedSeatingPlanId.ShouldNotBeNull();
				second.SelectedSeatingPlanId.ShouldBe(first.SelectedSeatingPlanId);
				(await _seatingPlanRepository.CountAsync(plan => plan.ExamId == exam.Id)).ShouldBe(1);
				var plan = await _seatingPlanRepository.GetAsync(first.SelectedSeatingPlanId!.Value);
				plan.SessionKey.ShouldBe(first.Sessions.Single().SessionKey);

				var planSubjects = await _seatingPlanSubjectRepository.GetListAsync(subject =>
					subject.SeatingPlanId == first.SelectedSeatingPlanId!.Value);
				planSubjects.Select(subject => subject.ExamSubjectId).ToHashSet()
					.SetEquals(examSubjectIds)
					.ShouldBeTrue();
			}
		});
	}

	[Fact]
	public async Task PrepareSeatingPlanAsync_Should_Create_An_Individual_Subject_Plan()
	{
		await WithUnitOfWorkAsync(async () =>
		{
			using (_currentTenant.Change(TestTenantId))
			{
				await EnsureAdminUserExistsAsync();
				var (exam, examSubjectIds) = await CreateSessionExamAsync(scheduleSecondSubject: true);

				var result = await _seatAllocationAppService.PrepareSeatingPlanAsync(
					exam.Id,
					new PrepareSeatingPlanInputDto { ExamSubjectIds = [examSubjectIds[0]] });

				result.SelectedSeatingPlanId.ShouldNotBeNull();
				var plan = result.Plans.Single(item => item.SeatingPlanId == result.SelectedSeatingPlanId);
				plan.ScopeType.ShouldBe(SeatingPlanScopeType.SingleSubject);
				plan.Subjects.Select(subject => subject.ExamSubjectId).ShouldBe([examSubjectIds[0]]);
				plan.ScheduleCount.ShouldBe(1);
			}
		});
	}

	[Fact]
	public async Task PrepareSeatingPlanAsync_Should_Share_One_Plan_Across_Multiple_Subject_Schedules()
	{
		await WithUnitOfWorkAsync(async () =>
		{
			using (_currentTenant.Change(TestTenantId))
			{
				await EnsureAdminUserExistsAsync();
				var (exam, examSubjectIds) = await CreateSessionExamAsync(
					scheduleSecondSubject: true,
					secondSubjectOnDifferentDate: true);

				var result = await _seatAllocationAppService.PrepareSeatingPlanAsync(
					exam.Id,
					new PrepareSeatingPlanInputDto
					{
						Name = "Shared science plan",
						ExamSubjectIds = examSubjectIds
					});

				result.Plans.Count.ShouldBe(1);
				var plan = result.Plans.Single();
				plan.Name.ShouldBe("Shared science plan");
				plan.ScopeType.ShouldBe(SeatingPlanScopeType.SelectedSubjects);
				plan.Subjects.Count.ShouldBe(2);
				plan.ScheduleCount.ShouldBe(2);
			}
		});
	}

	[Fact]
	public async Task PrepareSeatingPlanAsync_Should_Reject_A_Subject_Already_Owned_By_Another_Plan()
	{
		await WithUnitOfWorkAsync(async () =>
		{
			using (_currentTenant.Change(TestTenantId))
			{
				await EnsureAdminUserExistsAsync();
				var (exam, examSubjectIds) = await CreateSessionExamAsync(scheduleSecondSubject: true);
				await _seatAllocationAppService.PrepareSeatingPlanAsync(
					exam.Id,
					new PrepareSeatingPlanInputDto { ExamSubjectIds = [examSubjectIds[0]] });

				var exception = await Should.ThrowAsync<UserFriendlyException>(() =>
					_seatAllocationAppService.PrepareSeatingPlanAsync(
						exam.Id,
						new PrepareSeatingPlanInputDto { ExamSubjectIds = examSubjectIds }));

				exception.Message.ShouldContain("already belong to another seating plan");
			}
		});
	}

	[Fact]
	public async Task ApplyAutoAllocatePreviewAsync_Should_Replace_Existing_Seats_And_Be_Idempotent()
	{
		await WithUnitOfWorkAsync(async () =>
		{
			using (_currentTenant.Change(TestTenantId))
			{
				await EnsureAdminUserExistsAsync();
				var (exam, examSubjectIds) = await CreateSessionExamAsync(scheduleSecondSubject: true);
				var divisionId = exam.ExamDivisions.Single().DivisionId;
				var room = await CreateRoomAsync();
				foreach (var subject in exam.ExamSubjects)
				{
					subject.AddRoomAssignment(new ExamSubjectDivision(
						Guid.NewGuid(), subject.Id, divisionId, room.Capacity, [room.Id]));
				}
				await _examRepository.UpdateAsync(exam, autoSave: true);

				var standard = await _standardRepository.GetAsync(exam.StandardId);
				var stage = await _stageRepository.GetAsync(standard.EducationMediumStageId);
				var medium = await _educationMediumRepository.GetAsync(stage.EducationMediumId);
				var board = await _educationBoardRepository.GetAsync(medium.EducationBoardId);
				var unique = Guid.NewGuid().ToString("N");
				var students = new[]
				{
					CreateStudent(board.AcademicYearId, board.Id, medium.Id, stage.Id, standard.Id, divisionId, 1, unique),
					CreateStudent(board.AcademicYearId, board.Id, medium.Id, stage.Id, standard.Id, divisionId, 2, unique)
				};
				await _studentRepository.InsertManyAsync(students, autoSave: true);

				var prepared = await _seatAllocationAppService.PrepareSessionSeatingPlanAsync(
					exam.Id,
					new PrepareSessionSeatingPlanInputDto { ExamSubjectIds = examSubjectIds });
				var planId = prepared.SelectedSeatingPlanId!.Value;
				await _seatAllocationRepository.InsertManyAsync(
					students.Select((student, index) => new ExamSeatAllocation(
						Guid.NewGuid(), TestTenantId, planId, exam.Id, student.Id, room.Id, index + 1, null, true)),
					autoSave: true);

				var input = new ApplyAutoAllocatePreviewInputDto
				{
					SeatingPlanId = planId,
					RoomIds = [room.Id],
					AllocationRulesJson = "{}",
					Assignments =
					[
						new AutoAllocateProposedAssignmentDto
						{
							StudentId = students[0].Id,
							RoomId = room.Id,
							SeatNumber = 2
						},
						new AutoAllocateProposedAssignmentDto
						{
							StudentId = students[1].Id,
							RoomId = room.Id,
							SeatNumber = 1
						}
					]
				};

				await _seatAllocationAppService.ApplyAutoAllocatePreviewAsync(exam.Id, input);
				await _seatAllocationAppService.ApplyAutoAllocatePreviewAsync(exam.Id, input);

				var allocations = await _seatAllocationRepository.GetListAsync(row =>
					row.ExamId == exam.Id && row.SeatingPlanId == planId);
				allocations.Count.ShouldBe(2);
				allocations.ShouldContain(row => row.StudentId == students[0].Id && row.SeatNumber == 2);
				allocations.ShouldContain(row => row.StudentId == students[1].Id && row.SeatNumber == 1);
			}
		});
	}

	[Fact]
	public async Task AutoAllocatePreviewAsync_Should_Repropose_Students_Already_Seated_In_The_Current_Plan()
	{
		await WithUnitOfWorkAsync(async () =>
		{
			using (_currentTenant.Change(TestTenantId))
			{
				await EnsureAdminUserExistsAsync();
				var (exam, examSubjectIds) = await CreateSessionExamAsync(scheduleSecondSubject: true);
				var divisionId = exam.ExamDivisions.Single().DivisionId;
				var room = await CreateRoomAsync();
				foreach (var subject in exam.ExamSubjects)
				{
					subject.AddRoomAssignment(new ExamSubjectDivision(
						Guid.NewGuid(), subject.Id, divisionId, room.Capacity, [room.Id]));
				}
				await _examRepository.UpdateAsync(exam, autoSave: true);

				var standard = await _standardRepository.GetAsync(exam.StandardId);
				var stage = await _stageRepository.GetAsync(standard.EducationMediumStageId);
				var medium = await _educationMediumRepository.GetAsync(stage.EducationMediumId);
				var board = await _educationBoardRepository.GetAsync(medium.EducationBoardId);
				var unique = Guid.NewGuid().ToString("N");
				var students = new[]
				{
					CreateStudent(board.AcademicYearId, board.Id, medium.Id, stage.Id, standard.Id, divisionId, 1, unique),
					CreateStudent(board.AcademicYearId, board.Id, medium.Id, stage.Id, standard.Id, divisionId, 2, unique),
					CreateStudent(board.AcademicYearId, board.Id, medium.Id, stage.Id, standard.Id, divisionId, 3, unique)
				};
				await _studentRepository.InsertManyAsync(students, autoSave: true);

				var prepared = await _seatAllocationAppService.PrepareSessionSeatingPlanAsync(
					exam.Id,
					new PrepareSessionSeatingPlanInputDto { ExamSubjectIds = examSubjectIds });
				var planId = prepared.SelectedSeatingPlanId!.Value;
				await _seatAllocationRepository.InsertManyAsync(
					students.Select((student, index) => new ExamSeatAllocation(
						Guid.NewGuid(),
						TestTenantId,
						planId,
						exam.Id,
						student.Id,
						room.Id,
						index + 4,
						benchId: null,
						isRoomAssign: true)),
					autoSave: true);

				var preview = await _seatAllocationAppService.AutoAllocatePreviewAsync(
					exam.Id,
					new AutoAllocateRequestDto
					{
						SeatingPlanId = planId,
						RoomIds = [room.Id]
					});

				preview.TotalStudents.ShouldBe(3);
				preview.Assignments.Count.ShouldBe(3);
				preview.Assignments.Select(row => row.StudentId).ToHashSet()
					.ShouldBe(students.Select(student => student.Id).ToHashSet(), ignoreOrder: true);
				preview.Assignments.Select(row => row.SeatNumber).ShouldBe([1, 2, 3], ignoreOrder: true);
			}
		});
	}

	[Fact]
	public async Task GetSeatLayoutAsync_Should_Block_Seats_From_An_Overlapping_Sibling_Plan()
	{
		await WithUnitOfWorkAsync(async () =>
		{
			using (_currentTenant.Change(TestTenantId))
			{
				await EnsureAdminUserExistsAsync();
				var fixture = await CreateSameRoomSingleSubjectPlansAsync(secondSubjectOnDifferentDate: false);

				var layout = await _seatAllocationAppService.GetSeatLayoutAsync(
					fixture.Exam.Id,
					new GetSeatLayoutInput
					{
						SeatingPlanId = fixture.SecondPlanId,
						DivisionId = fixture.DivisionId,
						RoomId = fixture.Room.Id,
						PageSize = 100
					});

				var occupiedSeat = layout.Items.Single(item => item.SeatNumber == 1);
				occupiedSeat.StudentId.ShouldBe(fixture.Students[0].Id);
				occupiedSeat.IsOtherPlanOccupied.ShouldBeTrue();
				occupiedSeat.IsExternalExamOccupied.ShouldBeFalse();
				occupiedSeat.SeatingPlanId.ShouldBe(fixture.FirstPlanId);
				occupiedSeat.SeatingPlanName.ShouldBe("English seating plan");
				occupiedSeat.SubjectNames.ShouldContain(fixture.FirstSubjectName);
			}
		});
	}

	[Fact]
	public async Task GetSeatLayoutAsync_Should_Not_Block_Seats_From_A_NonOverlapping_Sibling_Plan()
	{
		await WithUnitOfWorkAsync(async () =>
		{
			using (_currentTenant.Change(TestTenantId))
			{
				await EnsureAdminUserExistsAsync();
				var fixture = await CreateSameRoomSingleSubjectPlansAsync(secondSubjectOnDifferentDate: true);

				var layout = await _seatAllocationAppService.GetSeatLayoutAsync(
					fixture.Exam.Id,
					new GetSeatLayoutInput
					{
						SeatingPlanId = fixture.SecondPlanId,
						DivisionId = fixture.DivisionId,
						RoomId = fixture.Room.Id,
						PageSize = 100
					});

				var reusableSeat = layout.Items.Single(item => item.SeatNumber == 1);
				reusableSeat.StudentId.ShouldBeNull();
				reusableSeat.IsOtherPlanOccupied.ShouldBeFalse();
			}
		});
	}

	[Fact]
	public async Task AutoAllocatePreviewAsync_Should_Skip_Seats_From_An_Overlapping_Sibling_Plan()
	{
		await WithUnitOfWorkAsync(async () =>
		{
			using (_currentTenant.Change(TestTenantId))
			{
				await EnsureAdminUserExistsAsync();
				var fixture = await CreateSameRoomSingleSubjectPlansAsync(secondSubjectOnDifferentDate: false);

				var preview = await _seatAllocationAppService.AutoAllocatePreviewAsync(
					fixture.Exam.Id,
					new AutoAllocateRequestDto
					{
						SeatingPlanId = fixture.SecondPlanId,
						RoomIds = [fixture.Room.Id]
					});

				preview.Assignments.ShouldNotContain(assignment =>
					assignment.RoomId == fixture.Room.Id && assignment.SeatNumber == 1);
			}
		});
	}

	[Fact]
	public async Task SaveSeatAllocationAsync_Should_Reject_A_Seat_From_An_Overlapping_Sibling_Plan()
	{
		await WithUnitOfWorkAsync(async () =>
		{
			using (_currentTenant.Change(TestTenantId))
			{
				await EnsureAdminUserExistsAsync();
				var fixture = await CreateSameRoomSingleSubjectPlansAsync(secondSubjectOnDifferentDate: false);

				var exception = await Should.ThrowAsync<UserFriendlyException>(() =>
					_seatAllocationAppService.SaveSeatAllocationAsync(
						fixture.Exam.Id,
						new SaveSeatAllocationRequestDto
						{
							SeatingPlanId = fixture.SecondPlanId,
							RoomId = fixture.Room.Id,
							Students =
							[
								new SaveSeatStudentDto
								{
									StudentId = fixture.Students[1].Id,
									SeatNumber = 1,
									IsRoomAssign = true
								}
							]
						}));

				exception.Code.ShouldBe("ROOM_SEAT_TIME_CONFLICT");
				exception.Message.ShouldContain("English seating plan");
			}
		});
	}

	[Fact]
	public async Task SaveSeatingPlanBulkAsync_Should_Apply_Manual_Gapped_Seats_From_Review_Plan()
	{
		var (examId, planId, roomId, studentIds, version) = await WithUnitOfWorkAsync(async () =>
		{
			using (_currentTenant.Change(TestTenantId))
			{
				await EnsureAdminUserExistsAsync();
				var (exam, examSubjectIds) = await CreateSessionExamAsync(scheduleSecondSubject: true);
				var divisionId = exam.ExamDivisions.Single().DivisionId;
				var room = await CreateRoomAsync();
				foreach (var subject in exam.ExamSubjects)
				{
					subject.AddRoomAssignment(new ExamSubjectDivision(
						Guid.NewGuid(), subject.Id, divisionId, room.Capacity, [room.Id]));
				}
				await _examRepository.UpdateAsync(exam, autoSave: true);

				var standard = await _standardRepository.GetAsync(exam.StandardId);
				var stage = await _stageRepository.GetAsync(standard.EducationMediumStageId);
				var medium = await _educationMediumRepository.GetAsync(stage.EducationMediumId);
				var board = await _educationBoardRepository.GetAsync(medium.EducationBoardId);
				var unique = Guid.NewGuid().ToString("N");
				var students = Enumerable.Range(1, 3)
					.Select(rollNo => CreateStudent(board.AcademicYearId, board.Id, medium.Id,
						stage.Id, standard.Id, divisionId, rollNo, unique))
					.ToList();
				await _studentRepository.InsertManyAsync(students, autoSave: true);

				var prepared = await _seatAllocationAppService.PrepareSessionSeatingPlanAsync(
					exam.Id, new PrepareSessionSeatingPlanInputDto { ExamSubjectIds = examSubjectIds });
				var selectedPlanId = prepared.SelectedSeatingPlanId!.Value;
				var draft = await _seatAllocationAppService.SaveSeatingPlanDraftAsync(exam.Id,
					new SaveSeatingPlanDraftInputDto
					{
						SeatingPlanId = selectedPlanId,
						RoomIds = [room.Id],
						AllocationRulesJson = "{\"strategy\":\"alphabetical\",\"leaveGap\":true}"
					});

				return (exam.Id, selectedPlanId, room.Id, students.Select(student => student.Id).ToList(), draft.Version);
			}
		});

		var savedVersion = await WithUnitOfWorkAsync(async () =>
		{
			using (_currentTenant.Change(TestTenantId))
			{
				var saved = await _seatAllocationAppService.SaveSeatingPlanBulkAsync(examId,
					new SaveSeatingPlanBulkInputDto
					{
						SeatingPlanId = planId,
						RoomIds = [roomId],
						AllocationRulesJson = "{\"strategy\":\"alphabetical\",\"leaveGap\":true}",
						Version = version,
						Rooms =
						[
							new SaveSeatingPlanBulkRoomDto
							{
								RoomId = roomId,
								Students = studentIds.Select((studentId, index) => new SaveSeatStudentDto
								{
									StudentId = studentId,
									SeatNumber = index * 2 + 1,
									IsRoomAssign = true
								}).ToList()
							}
						]
					});

				saved.AllocatedStudents.ShouldBe(3);
				var allocations = await _seatAllocationRepository.GetListAsync(row =>
					row.ExamId == examId && row.SeatingPlanId == planId);
				allocations.Select(row => row.SeatNumber).OrderBy(seat => seat).ShouldBe([1, 3, 5]);
				return saved.Version;
			}
		});

		await WithUnitOfWorkAsync(async () =>
		{
			using (_currentTenant.Change(TestTenantId))
			{
				await _seatAllocationAppService.SaveSeatingPlanBulkAsync(examId,
					new SaveSeatingPlanBulkInputDto
					{
						SeatingPlanId = planId,
						RoomIds = [roomId],
						AllocationRulesJson = "{\"strategy\":\"alphabetical\",\"leaveGap\":true}",
						Version = savedVersion,
						Rooms =
						[
							new SaveSeatingPlanBulkRoomDto
							{
								RoomId = roomId,
								Students =
								[
									new SaveSeatStudentDto { StudentId = studentIds[0], SeatNumber = 3, IsRoomAssign = true },
									new SaveSeatStudentDto { StudentId = studentIds[1], SeatNumber = 1, IsRoomAssign = true },
									new SaveSeatStudentDto { StudentId = studentIds[2], SeatNumber = 5, IsRoomAssign = true }
								]
							}
						]
					});
			}
		});

		await WithUnitOfWorkAsync(async () =>
		{
			using (_currentTenant.Change(TestTenantId))
			{
				var swapped = await _seatAllocationRepository.GetListAsync(row =>
					row.ExamId == examId && row.SeatingPlanId == planId);
				swapped.Single(row => row.StudentId == studentIds[0]).SeatNumber.ShouldBe(3);
				swapped.Single(row => row.StudentId == studentIds[1]).SeatNumber.ShouldBe(1);

				var conflict = await Should.ThrowAsync<UserFriendlyException>(() =>
					_seatAllocationAppService.SaveSeatingPlanBulkAsync(examId,
						new SaveSeatingPlanBulkInputDto
						{
							SeatingPlanId = planId,
							RoomIds = [roomId],
							AllocationRulesJson = "{}",
							Version = version,
							Rooms = []
						}));
				conflict.Code.ShouldBe("SEATING_PLAN_CONCURRENCY_CONFLICT");
			}
		});
	}

	[Fact]
	public async Task SaveSeatingPlanDraftAsync_Should_Reject_An_Old_Opaque_Version()
	{
		await WithUnitOfWorkAsync(async () =>
		{
			using (_currentTenant.Change(TestTenantId))
			{
				await EnsureAdminUserExistsAsync();
				var (exam, examSubjectIds) = await CreateSessionExamAsync(scheduleSecondSubject: true);
				var prepared = await _seatAllocationAppService.PrepareSessionSeatingPlanAsync(
					exam.Id,
					new PrepareSessionSeatingPlanInputDto { ExamSubjectIds = examSubjectIds });
				var planId = prepared.SelectedSeatingPlanId!.Value;
				var original = await _seatAllocationAppService.GetSeatingPlanDraftAsync(exam.Id, planId);

				var updated = await _seatAllocationAppService.SaveSeatingPlanDraftAsync(
					exam.Id,
					new SaveSeatingPlanDraftInputDto
					{
						SeatingPlanId = planId,
						AllocationRulesJson = "{}",
						Version = original.Version
					});

				updated.Version.ShouldNotBe(original.Version);
				var exception = await Should.ThrowAsync<UserFriendlyException>(() =>
					_seatAllocationAppService.SaveSeatingPlanDraftAsync(
						exam.Id,
						new SaveSeatingPlanDraftInputDto
						{
							SeatingPlanId = planId,
							AllocationRulesJson = "{}",
							Version = original.Version
						}));
				exception.Code.ShouldBe("SEATING_PLAN_CONCURRENCY_CONFLICT");
			}
		});
	}

	[Fact]
	public async Task GetDivisionsForExamAsync_Should_Default_To_Exam_Standard_When_StandardId_Is_Not_Provided()
	{
		await WithUnitOfWorkAsync(async () =>
		{
			using (_currentTenant.Change(TestTenantId))
			{
				await EnsureAdminUserExistsAsync();
				var (exam, division) = await CreateScheduledExamAsync();

				var result = await _seatAllocationAppService.GetDivisionsForExamAsync(
					exam.Id,
					new GetExamDivisionsInput
					{
						StandardId = Guid.Empty
					});

				result.TotalCount.ShouldBe(1);
				result.Items.Select(item => item.Id).ShouldContain(division.Id);
				result.Items.Single().Name.ShouldBe(division.Name);
			}
		});
	}

	[Fact]
	public async Task GetDivisionsForExamAsync_Should_Return_Divisions_Alphabetically()
	{
		await WithUnitOfWorkAsync(async () =>
		{
			using (_currentTenant.Change(TestTenantId))
			{
				await EnsureAdminUserExistsAsync();
				var stageId = await EmployeeOrganizationTestHelper.EnsureOrganizationExistsAsync(ServiceProvider);
				var stage = await _stageRepository.GetAsync(stageId);
				var unique = Guid.NewGuid().ToString("N");

				var standard = await _standardManager.CreateAsync(
					stageId,
					$"Seat Std {unique}"[..30],
					11,
					shiftId: null);
				await _standardRepository.InsertAsync(standard, autoSave: true);

				var divisionB = await _divisionManager.CreateAsync(standard.Id, $"B-{unique}"[..30], 30);
				var divisionA = await _divisionManager.CreateAsync(standard.Id, $"A-{unique}"[..30], 30);
				await _divisionRepository.InsertManyAsync([divisionB, divisionA], autoSave: true);

				var subject = new Subject(
					Guid.NewGuid(),
					TestTenantId,
					$"Math {unique}"[..30],
					"#00A76F",
					stage.EducationMediumId);
				await _subjectRepository.InsertAsync(subject, autoSave: true);

				var examType = await _examTypeManager.CreateAsync(
					$"Seat Exam {unique}"[..30],
					description: null,
					stage.EducationMediumId);

				var exam = _examManager.Create(
					examType.Id,
					$"Seating Plan Exam {unique}"[..40],
					standard.Id,
					new List<Guid> { divisionB.Id, divisionA.Id },
					new List<Guid> { subject.Id });
				exam.ChangeStatus(ExamStatus.Published);
				await _examRepository.InsertAsync(exam, autoSave: true);

				var result = await _seatAllocationAppService.GetDivisionsForExamAsync(
					exam.Id,
					new GetExamDivisionsInput
					{
						StandardId = Guid.Empty
					});

				result.Items.Select(item => item.Name).ToList().ShouldBe([divisionA.Name, divisionB.Name]);
			}
		});
	}

	[Fact]
	public async Task GetSeatAllocationTableAsync_Should_Return_Division_Wise_Progress()
	{
		await WithUnitOfWorkAsync(async () =>
		{
			using (_currentTenant.Change(TestTenantId))
			{
				await EnsureAdminUserExistsAsync();
				var stageId = await EmployeeOrganizationTestHelper.EnsureOrganizationExistsAsync(ServiceProvider);
				var stage = await _stageRepository.GetAsync(stageId);
				var medium = await _educationMediumRepository.GetAsync(stage.EducationMediumId);
				var board = await _educationBoardRepository.GetAsync(medium.EducationBoardId);
				var unique = Guid.NewGuid().ToString("N");

				var standard = await _standardManager.CreateAsync(
					stageId,
					$"Seat Std {unique}"[..30],
					11,
					shiftId: null);
				await _standardRepository.InsertAsync(standard, autoSave: true);

				var divisionA = await _divisionManager.CreateAsync(standard.Id, $"A-{unique}"[..30], 30);
				var divisionB = await _divisionManager.CreateAsync(standard.Id, $"B-{unique}"[..30], 30);
				await _divisionRepository.InsertManyAsync([divisionB, divisionA], autoSave: true);

				var subject = new Subject(
					Guid.NewGuid(),
					TestTenantId,
					$"Math {unique}"[..30],
					"#00A76F",
					stage.EducationMediumId);
				await _subjectRepository.InsertAsync(subject, autoSave: true);

				var examType = await _examTypeManager.CreateAsync(
					$"Seat Exam {unique}"[..30],
					description: null,
					stage.EducationMediumId);

				var exam = _examManager.Create(
					examType.Id,
					$"Seating Plan Exam {unique}"[..40],
					standard.Id,
					new List<Guid> { divisionB.Id, divisionA.Id },
					new List<Guid> { subject.Id });
				exam.ChangeStatus(ExamStatus.Published);
				await _examRepository.InsertAsync(exam, autoSave: true);

				var students = Enumerable.Range(1, 3)
					.Select(index => CreateStudent(board.AcademicYearId, board.Id, medium.Id, stage.Id, standard.Id, divisionA.Id, index, unique))
					.Concat(Enumerable.Range(4, 3)
						.Select(index => CreateStudent(board.AcademicYearId, board.Id, medium.Id, stage.Id, standard.Id, divisionB.Id, index, unique)))
					.ToList();
				await _studentRepository.InsertManyAsync(students, autoSave: true);

				var plan = new ExamSeatingPlan(Guid.NewGuid(), TestTenantId, exam.Id, "All Subjects", SeatingPlanScopeType.AllSubjects);
				await _seatingPlanRepository.InsertAsync(plan, autoSave: true);
				var roomId = Guid.NewGuid();

				var allocations = students
					.Where(student => student.DivisionId == divisionA.Id || student.RollNo == 4)
					.Select((student, index) => new ExamSeatAllocation(
						Guid.NewGuid(),
						TestTenantId,
						plan.Id,
						exam.Id,
						student.Id,
						roomId,
						index + 1,
						benchId: null,
						isRoomAssign: true))
					.ToList();
				await _seatAllocationRepository.InsertManyAsync(allocations, autoSave: true);

				var result = await _seatAllocationAppService.GetSeatAllocationTableAsync(
					new GetSeatAllocationTableInput { StandardId = standard.Id });

				var row = result.Items.Single(item => item.ExamId == exam.Id);
				row.TotalStudents.ShouldBe(6);
				row.AllocatedStudents.ShouldBe(4);
				row.DivisionProgress.Count.ShouldBe(2);
				row.Divisions.ShouldBe([divisionA.Name, divisionB.Name]);
				row.DivisionProgress.Select(item => item.DivisionName).ToList().ShouldBe([divisionA.Name, divisionB.Name]);
				row.DivisionProgress.Single(item => item.DivisionId == divisionA.Id).AllocatedStudents.ShouldBe(3);
				row.DivisionProgress.Single(item => item.DivisionId == divisionA.Id).Progress.ShouldBe(100);
				row.DivisionProgress.Single(item => item.DivisionId == divisionB.Id).AllocatedStudents.ShouldBe(1);
				row.DivisionProgress.Single(item => item.DivisionId == divisionB.Id).Progress.ShouldBe(33);
			}
		});
	}

	[Fact]
	public async Task GetSeatAllocationTableAsync_Should_Report_Published_Selected_Subject_Session()
	{
		await WithUnitOfWorkAsync(async () =>
		{
			using (_currentTenant.Change(TestTenantId))
			{
				await EnsureAdminUserExistsAsync();
				var (exam, planId, room, students) = await CreateAllocatedSessionPlanAsync();

				await _seatAllocationAppService.FinalizeSeatingPlanAsync(exam.Id, planId);
				await _seatAllocationAppService.PublishSeatingPlanAsync(
					exam.Id,
					new PublishSeatingPlanInputDto(),
					planId);

				var result = await _seatAllocationAppService.GetSeatAllocationTableAsync(
					new GetSeatAllocationTableInput { StandardId = exam.StandardId });
				var row = result.Items.Single(item => item.ExamId == exam.Id);
				var session = row.Sessions.Single();

				session.SeatingPlanId.ShouldBe(planId);
				session.Status.ShouldBe((int)SeatingPlanStatus.Published);
				session.TotalStudents.ShouldBe(students.Count);
				session.AllocatedStudents.ShouldBe(students.Count);
				session.PendingStudents.ShouldBe(0);
				session.RoomsUsed.ShouldBe(1);
				session.UsableCapacity.ShouldBe(room.Capacity);
				session.EmptySeats.ShouldBe(room.Capacity - students.Count);
				row.Status.ShouldBe((int)SeatingPlanStatus.Published);
			}
		});
	}

	[Fact]
	public async Task Finalize_And_Publish_Should_Be_Idempotent_On_Retry()
	{
		await WithUnitOfWorkAsync(async () =>
		{
			using (_currentTenant.Change(TestTenantId))
			{
				await EnsureAdminUserExistsAsync();
				var (exam, planId, _, _) = await CreateAllocatedSessionPlanAsync();

				await _seatAllocationAppService.FinalizeSeatingPlanAsync(exam.Id, planId);
				await _seatAllocationAppService.FinalizeSeatingPlanAsync(exam.Id, planId);
				await _seatAllocationAppService.PublishSeatingPlanAsync(
					exam.Id,
					new PublishSeatingPlanInputDto(),
					planId);
				await _seatAllocationAppService.PublishSeatingPlanAsync(
					exam.Id,
					new PublishSeatingPlanInputDto(),
					planId);

				var auditLogs = await _auditLogRepository.GetListAsync(log => log.SeatingPlanId == planId);
				auditLogs.Count(log => log.Action == SeatingPlanAuditAction.Finalized).ShouldBe(1);
				auditLogs.Count(log => log.Action == SeatingPlanAuditAction.ExaminationPublished).ShouldBe(1);
			}
		});
	}

	[Fact]
	public async Task PublishExaminationSeatingAsync_Should_Block_When_Any_Subject_Is_Unplanned()
	{
		await WithUnitOfWorkAsync(async () =>
		{
			using (_currentTenant.Change(TestTenantId))
			{
				await EnsureAdminUserExistsAsync();
				var (exam, examSubjectIds) = await CreateSessionExamAsync(
					scheduleSecondSubject: true,
					secondSubjectOnDifferentDate: true);
				await _seatAllocationAppService.PrepareSessionSeatingPlanAsync(
					exam.Id,
					new PrepareSessionSeatingPlanInputDto { ExamSubjectIds = [examSubjectIds[0]] });

				var readiness = await _seatAllocationAppService.ValidateExaminationSeatingAsync(exam.Id);
				readiness.IsReadyToPublish.ShouldBeFalse();
				readiness.Blockers.ShouldContain(issue => issue.Code == "UNPLANNED_SUBJECTS");

				var exception = await Should.ThrowAsync<UserFriendlyException>(() =>
					_seatAllocationAppService.PublishExaminationSeatingAsync(
						exam.Id,
						new PublishSeatingPlanInputDto()));
				exception.Code.ShouldBe("EXAMINATION_SEATING_NOT_READY");
				(await _seatingPublicationRepository.GetListAsync(publication => publication.ExamId == exam.Id))
					.Count.ShouldBe(0);
			}
		});
	}

	[Fact]
	public async Task PublishExaminationSeatingAsync_Should_Publish_All_Plans_Atomically_And_Only_Once()
	{
		await WithUnitOfWorkAsync(async () =>
		{
			using (_currentTenant.Change(TestTenantId))
			{
				await EnsureAdminUserExistsAsync();
				var (exam, planIds, _, _) = await CreateAllocatedSeparateSessionPlansAsync();

				var readiness = await _seatAllocationAppService.ValidateExaminationSeatingAsync(exam.Id);
				readiness.IsReadyToPublish.ShouldBeTrue();
				readiness.ReadyPlanCount.ShouldBe(2);

				var first = await _seatAllocationAppService.PublishExaminationSeatingAsync(
					exam.Id,
					new PublishSeatingPlanInputDto());
				var retry = await _seatAllocationAppService.PublishExaminationSeatingAsync(
					exam.Id,
					new PublishSeatingPlanInputDto());

				first.Readiness.IsPublished.ShouldBeTrue();
				retry.Readiness.IsPublished.ShouldBeTrue();
				(await _seatingPublicationRepository.GetListAsync(publication => publication.ExamId == exam.Id))
					.Count.ShouldBe(1);
				var plans = await _seatingPlanRepository.GetListAsync(plan => planIds.Contains(plan.Id));
				plans.Count.ShouldBe(2);
				plans.ShouldAllBe(plan => plan.Status == SeatingPlanStatus.Published && plan.IsLocked);
				var publicationAudits = await _auditLogRepository.GetListAsync(log =>
					log.ExamId == exam.Id && log.Action == SeatingPlanAuditAction.ExaminationPublished);
				publicationAudits.Count.ShouldBe(2);
			}
		});
	}

	[Fact]
	public async Task ResetExaminationSeatingDraftAsync_Should_Remove_All_Draft_Plans_And_Allocations()
	{
		await WithUnitOfWorkAsync(async () =>
		{
			using (_currentTenant.Change(TestTenantId))
			{
				await EnsureAdminUserExistsAsync();
				var (exam, _, _, _) = await CreateAllocatedSeparateSessionPlansAsync();

				var workspace = await _seatAllocationAppService.ResetExaminationSeatingDraftAsync(
					exam.Id,
					new ResetExaminationSeatingDraftInputDto { Reason = "Regroup all examination subjects" });

				workspace.Plans.ShouldBeEmpty();
				workspace.Readiness.CanResetDraft.ShouldBeFalse();
				workspace.Readiness.PlannedSubjectCount.ShouldBe(0);
				(await _seatingPlanRepository.GetListAsync(plan => plan.ExamId == exam.Id)).Count.ShouldBe(0);
				(await _seatingPlanSubjectRepository.GetListAsync(subject => subject.ExamId == exam.Id)).Count.ShouldBe(0);
				(await _seatAllocationRepository.GetListAsync(allocation => allocation.ExamId == exam.Id)).Count.ShouldBe(0);
				var resetAudits = await _auditLogRepository.GetListAsync(log =>
					log.ExamId == exam.Id && log.Action == SeatingPlanAuditAction.DraftReset);
				resetAudits.Count.ShouldBe(2);
			}
		});
	}

	[Fact]
	public async Task Published_Examination_Seating_Should_Reject_Reset_Unlock_And_Replanning()
	{
		await WithUnitOfWorkAsync(async () =>
		{
			using (_currentTenant.Change(TestTenantId))
			{
				await EnsureAdminUserExistsAsync();
				var (exam, planId, _, _) = await CreateAllocatedSessionPlanAsync();
				await _seatAllocationAppService.PublishExaminationSeatingAsync(
					exam.Id,
					new PublishSeatingPlanInputDto());

				var resetException = await Should.ThrowAsync<UserFriendlyException>(() =>
					_seatAllocationAppService.ResetExaminationSeatingDraftAsync(
						exam.Id,
						new ResetExaminationSeatingDraftInputDto { Reason = "Try to reopen publication" }));
				resetException.Code.ShouldBe("EXAMINATION_SEATING_PUBLISHED");

				var unlockException = await Should.ThrowAsync<UserFriendlyException>(() =>
					_seatAllocationAppService.UnlockSeatingPlanAsync(
						exam.Id,
						new UnlockSeatingPlanInputDto { Reason = "Try to unlock publication" },
						planId));
				unlockException.Code.ShouldBe("EXAMINATION_SEATING_PUBLISHED");

				var prepareException = await Should.ThrowAsync<UserFriendlyException>(() =>
					_seatAllocationAppService.PrepareSessionSeatingPlanAsync(
						exam.Id,
						new PrepareSessionSeatingPlanInputDto
						{
							ExamSubjectIds = exam.ExamSubjects.Select(subject => subject.Id).ToList()
						}));
				prepareException.Code.ShouldBe("EXAMINATION_SEATING_PUBLISHED");
			}
		});
	}

	[Fact]
	public async Task Editing_A_Populated_Draft_Group_Should_Require_And_Honor_Explicit_Seat_Reset()
	{
		await WithUnitOfWorkAsync(async () =>
		{
			using (_currentTenant.Change(TestTenantId))
			{
				await EnsureAdminUserExistsAsync();
				var (exam, planId, _, students) = await CreateAllocatedSessionPlanAsync();
				var firstSubjectId = exam.ExamSubjects.First().Id;
				var input = new SaveSeatingPlanScopeInputDto
				{
					SeatingPlanId = planId,
					Name = "Regrouped subject plan",
					ScopeType = SeatingPlanScopeType.SingleSubject,
					ExamSubjectIds = [firstSubjectId]
				};

				var confirmationException = await Should.ThrowAsync<UserFriendlyException>(() =>
					_seatAllocationAppService.SaveSeatingPlanScopeAsync(exam.Id, input));
				confirmationException.Message.ShouldContain("Confirm the reset");
				(await _seatAllocationRepository.GetListAsync(allocation => allocation.SeatingPlanId == planId))
					.Count.ShouldBe(students.Count);

				input.ResetAllocationsWhenSubjectsChange = true;
				var updated = await _seatAllocationAppService.SaveSeatingPlanScopeAsync(exam.Id, input);

				updated.Subjects.Select(subject => subject.ExamSubjectId).ShouldBe([firstSubjectId]);
				updated.AllocatedStudents.ShouldBe(0);
				(await _seatAllocationRepository.GetListAsync(allocation => allocation.SeatingPlanId == planId))
					.ShouldBeEmpty();
				var readiness = await _seatAllocationAppService.ValidateExaminationSeatingAsync(exam.Id);
				readiness.Blockers.ShouldContain(issue => issue.Code == "UNPLANNED_SUBJECTS");
			}
		});
	}

	[Fact]
	public async Task Legacy_PerPlan_Publication_Without_Global_Publication_Should_Remain_Editable()
	{
		await WithUnitOfWorkAsync(async () =>
		{
			using (_currentTenant.Change(TestTenantId))
			{
				await EnsureAdminUserExistsAsync();
				var (exam, planId, _, _) = await CreateAllocatedSessionPlanAsync();
				var legacyPlan = await _seatingPlanRepository.GetAsync(planId);
				var now = DateTime.UtcNow;
				legacyPlan.Finalize(FakeAdminUserId, now);
				legacyPlan.Publish(FakeAdminUserId, now);
				await _seatingPlanRepository.UpdateAsync(legacyPlan, autoSave: true);

				var draftWorkspace = await _seatAllocationAppService.GetSeatingPlanWorkspaceAsync(exam.Id, planId);
				draftWorkspace.Readiness.IsPublished.ShouldBeFalse();
				draftWorkspace.Plans.Single().Status.ShouldBe(SeatingPlanStatus.Ready);
				draftWorkspace.Plans.Single().IsLocked.ShouldBeFalse();

				var retainedSubjectId = exam.ExamSubjects.First().Id;
				var updated = await _seatAllocationAppService.SaveSeatingPlanScopeAsync(
					exam.Id,
					new SaveSeatingPlanScopeInputDto
					{
						SeatingPlanId = planId,
						Name = "Reopened legacy draft",
						ScopeType = SeatingPlanScopeType.SingleSubject,
						ExamSubjectIds = [retainedSubjectId],
						ResetAllocationsWhenSubjectsChange = true
					});

				updated.Subjects.Select(subject => subject.ExamSubjectId).ShouldBe([retainedSubjectId]);
				updated.AllocatedStudents.ShouldBe(0);
				var reopenedPlan = await _seatingPlanRepository.GetAsync(planId);
				reopenedPlan.IsLocked.ShouldBeFalse();
				reopenedPlan.Status.ShouldNotBe(SeatingPlanStatus.Published);
				reopenedPlan.PublishedAt.ShouldBeNull();
				(await _seatingPublicationRepository.GetListAsync(publication => publication.ExamId == exam.Id))
					.ShouldBeEmpty();
			}
		});
	}

	[Fact]
	public async Task GetStudentsForDivisionAsync_Should_Page_Students_And_Keep_Count_And_Seat_Details()
	{
		await WithUnitOfWorkAsync(async () =>
		{
			using (_currentTenant.Change(TestTenantId))
			{
				await EnsureAdminUserExistsAsync();
				var (exam, planId, _, students) = await CreateAllocatedSessionPlanAsync();
				var divisionId = exam.ExamDivisions.Single().DivisionId;
				var input = new GetSeatStudentsInput { SeatingPlanId = planId, Page = 1, PageSize = 1 };
				var firstPage = await _seatAllocationAppService.GetStudentsForDivisionAsync(exam.Id, divisionId, input);
				input.Page = 2;
				var secondPage = await _seatAllocationAppService.GetStudentsForDivisionAsync(exam.Id, divisionId, input);

				firstPage.TotalCount.ShouldBe(2);
				secondPage.TotalCount.ShouldBe(2);
				firstPage.TotalPages.ShouldBe(2);
				firstPage.Items.Count.ShouldBe(1);
				secondPage.Items.Count.ShouldBe(1);
				firstPage.Items.Concat(secondPage.Items).Select(item => item.StudentId).ToHashSet()
					.SetEquals(students.Select(student => student.Id)).ShouldBeTrue();
				firstPage.Items.Concat(secondPage.Items).ShouldAllBe(item => item.IsSeatAllocate && item.SeatNumber > 0);
				input.Page = 1;
				input.Search = "Seat2";
				var searched = await _seatAllocationAppService.GetStudentsForDivisionAsync(exam.Id, divisionId, input);
				searched.TotalCount.ShouldBe(1);
				searched.Items.Single().StudentId.ShouldBe(students[1].Id);
				searched.Items.Single().SeatNumber.ShouldBe(2);
				input.Page = 2;
				var emptyPage = await _seatAllocationAppService.GetStudentsForDivisionAsync(exam.Id, divisionId, input);
				emptyPage.TotalCount.ShouldBe(1);
				emptyPage.Items.ShouldBeEmpty();
			}
		});
	}

	[Fact]
	public async Task GetStudentsForDivisionAsync_Should_Preserve_Single_Subject_Selection_And_Core_Fallback()
	{
		await WithUnitOfWorkAsync(async () =>
		{
			using (_currentTenant.Change(TestTenantId))
			{
				await EnsureAdminUserExistsAsync();
				var fixture = await CreateSameRoomSingleSubjectPlansAsync(secondSubjectOnDifferentDate: true);
				var input = new GetSeatStudentsInput { SeatingPlanId = fixture.FirstPlanId, PageSize = 10 };
				var coreFallback = await _seatAllocationAppService.GetStudentsForDivisionAsync(fixture.Exam.Id, fixture.DivisionId, input);
				coreFallback.TotalCount.ShouldBe(2);

				var planSubject = await _seatingPlanSubjectRepository.GetAsync(subject => subject.SeatingPlanId == fixture.FirstPlanId);
				var standard = await _standardRepository.GetAsync(fixture.Exam.StandardId);
				var stage = await _stageRepository.GetAsync(standard.EducationMediumStageId);
				var group = new ElectiveGroup(Guid.NewGuid(), TestTenantId, "Picker elective", "#123456", stage.EducationMediumId);
				group.AddSubject(planSubject.SubjectId);
				await GetRequiredService<IRepository<ElectiveGroup, Guid>>().InsertAsync(group, autoSave: true);
				// Seating's existing picker includes selections before they are locked.
				await GetRequiredService<IRepository<StudentElectiveSelection, Guid>>().InsertAsync(
					new StudentElectiveSelection(Guid.NewGuid(), TestTenantId, fixture.Students[1].Id,
						group.Id, planSubject.SubjectId, stage.EducationMediumId), autoSave: true);

				var selected = await _seatAllocationAppService.GetStudentsForDivisionAsync(fixture.Exam.Id, fixture.DivisionId, input);
				selected.TotalCount.ShouldBe(1);
				selected.Items.Single().StudentId.ShouldBe(fixture.Students[1].Id);
			}
		});
	}

	private async Task<(Exam exam, Guid planId, Room room, List<Student> students)>
		CreateAllocatedSessionPlanAsync()
	{
		var (exam, planIds, room, students) = await CreateAllocatedPlansAsync(separateSessions: false);
		return (exam, planIds.Single(), room, students);
	}

	private async Task<(
		Exam Exam,
		Guid DivisionId,
		Guid FirstPlanId,
		Guid SecondPlanId,
		string FirstSubjectName,
		Room Room,
		List<Student> Students)> CreateSameRoomSingleSubjectPlansAsync(bool secondSubjectOnDifferentDate)
	{
		var (exam, examSubjectIds) = await CreateSessionExamAsync(
			scheduleSecondSubject: true,
			secondSubjectOnDifferentDate: secondSubjectOnDifferentDate);
		var divisionId = exam.ExamDivisions.Single().DivisionId;
		var room = await CreateRoomAsync();
		foreach (var subject in exam.ExamSubjects)
		{
			subject.AddRoomAssignment(new ExamSubjectDivision(
				Guid.NewGuid(), subject.Id, divisionId, room.Capacity, [room.Id]));
		}
		await _examRepository.UpdateAsync(exam, autoSave: true);

		var standard = await _standardRepository.GetAsync(exam.StandardId);
		var stage = await _stageRepository.GetAsync(standard.EducationMediumStageId);
		var medium = await _educationMediumRepository.GetAsync(stage.EducationMediumId);
		var board = await _educationBoardRepository.GetAsync(medium.EducationBoardId);
		var unique = Guid.NewGuid().ToString("N");
		var students = new List<Student>
		{
			CreateStudent(board.AcademicYearId, board.Id, medium.Id, stage.Id, standard.Id, divisionId, 1, unique),
			CreateStudent(board.AcademicYearId, board.Id, medium.Id, stage.Id, standard.Id, divisionId, 2, unique)
		};
		await _studentRepository.InsertManyAsync(students, autoSave: true);

		var firstWorkspace = await _seatAllocationAppService.PrepareSeatingPlanAsync(
			exam.Id,
			new PrepareSeatingPlanInputDto
			{
				Name = "English seating plan",
				ExamSubjectIds = [examSubjectIds[0]]
			});
		var secondWorkspace = await _seatAllocationAppService.PrepareSeatingPlanAsync(
			exam.Id,
			new PrepareSeatingPlanInputDto
			{
				Name = "Computer Science seating plan",
				ExamSubjectIds = [examSubjectIds[1]]
			});
		var firstPlanId = firstWorkspace.SelectedSeatingPlanId!.Value;
		var secondPlanId = secondWorkspace.SelectedSeatingPlanId!.Value;
		await _seatAllocationRepository.InsertAsync(
			new ExamSeatAllocation(
				Guid.NewGuid(),
				TestTenantId,
				firstPlanId,
				exam.Id,
				students[0].Id,
				room.Id,
				seatNumber: 1,
				benchId: null,
				isRoomAssign: true),
			autoSave: true);

		var firstExamSubject = exam.ExamSubjects.Single(subject => subject.Id == examSubjectIds[0]);
		var firstSubject = await _subjectRepository.GetAsync(firstExamSubject.SubjectId);
		return (
			exam,
			divisionId,
			firstPlanId,
			secondPlanId,
			firstSubject.Name,
			room,
			students);
	}

	private Task<(Exam exam, List<Guid> planIds, Room room, List<Student> students)>
		CreateAllocatedSeparateSessionPlansAsync()
	{
		return CreateAllocatedPlansAsync(separateSessions: true);
	}

	private async Task<(Exam exam, List<Guid> planIds, Room room, List<Student> students)>
		CreateAllocatedPlansAsync(bool separateSessions)
	{
		var (exam, examSubjectIds) = await CreateSessionExamAsync(
			scheduleSecondSubject: true,
			secondSubjectOnDifferentDate: separateSessions);
		var divisionId = exam.ExamDivisions.Single().DivisionId;
		var room = await CreateRoomAsync();
		foreach (var subject in exam.ExamSubjects)
		{
			subject.AddRoomAssignment(new ExamSubjectDivision(
				Guid.NewGuid(), subject.Id, divisionId, room.Capacity, [room.Id]));
		}
		await _examRepository.UpdateAsync(exam, autoSave: true);

		var standard = await _standardRepository.GetAsync(exam.StandardId);
		var stage = await _stageRepository.GetAsync(standard.EducationMediumStageId);
		var medium = await _educationMediumRepository.GetAsync(stage.EducationMediumId);
		var board = await _educationBoardRepository.GetAsync(medium.EducationBoardId);
		var unique = Guid.NewGuid().ToString("N");
		var students = new List<Student>
		{
			CreateStudent(board.AcademicYearId, board.Id, medium.Id, stage.Id, standard.Id, divisionId, 1, unique),
			CreateStudent(board.AcademicYearId, board.Id, medium.Id, stage.Id, standard.Id, divisionId, 2, unique)
		};
		await _studentRepository.InsertManyAsync(students, autoSave: true);

		var planSubjectGroups = separateSessions
			? examSubjectIds.Select(examSubjectId => new List<Guid> { examSubjectId }).ToList()
			: [examSubjectIds];
		var planIds = new List<Guid>();
		foreach (var planSubjectIds in planSubjectGroups)
		{
			var prepared = await _seatAllocationAppService.PrepareSessionSeatingPlanAsync(
				exam.Id,
				new PrepareSessionSeatingPlanInputDto { ExamSubjectIds = planSubjectIds });
			var planId = prepared.SelectedSeatingPlanId!.Value;
			planIds.Add(planId);
			await _seatAllocationAppService.ApplyAutoAllocatePreviewAsync(
				exam.Id,
				new ApplyAutoAllocatePreviewInputDto
				{
					SeatingPlanId = planId,
					RoomIds = [room.Id],
					AllocationRulesJson = "{}",
					Assignments = students.Select((student, index) =>
						new AutoAllocateProposedAssignmentDto
						{
							StudentId = student.Id,
							RoomId = room.Id,
							SeatNumber = index + 1
						}).ToList()
				});
		}

		return (exam, planIds, room, students);
	}

	private async Task EnsureAdminUserExistsAsync()
	{
		if (await _identityUserRepository.FindAsync(FakeAdminUserId) is not null)
		{
			return;
		}

		var adminRole = (await _identityRoleRepository.GetListAsync(role => role.Name == "admin"))
			.FirstOrDefault();
		if (adminRole is null)
		{
			adminRole = new IdentityRole(Guid.NewGuid(), "admin", TestTenantId);
			await _identityRoleRepository.InsertAsync(adminRole, autoSave: true);
		}

		var user = new IdentityUser(FakeAdminUserId, "admin", "admin@test.local", TestTenantId);
		user.AddRole(adminRole.Id);
		await _identityUserRepository.InsertAsync(user, autoSave: true);
	}

	private async Task<Room> CreateRoomAsync()
	{
		var suffix = Guid.NewGuid().ToString("N")[..8];
		var building = new Building
		{
			TenantId = TestTenantId,
			BuildingName = $"Exam Building {suffix}",
			TotalFloors = 1
		};
		SetEntityId(building, Guid.NewGuid());
		await _buildingRepository.InsertAsync(building, autoSave: true);

		var floor = new Floor { BuildingId = building.Id, FloorName = "First", FloorNo = 1 };
		SetEntityId(floor, Guid.NewGuid());
		await _floorRepository.InsertAsync(floor, autoSave: true);

		var roomType = new RoomType
		{
			TenantId = TestTenantId,
			RoomTypeName = $"Exam {suffix}",
			ShortName = $"E{suffix[..3]}"
		};
		SetEntityId(roomType, Guid.NewGuid());
		await _roomTypeRepository.InsertAsync(roomType, autoSave: true);

		var room = new Room
		{
			TenantId = TestTenantId,
			RoomName = $"R-{suffix}",
			BuildingId = building.Id,
			FloorId = floor.Id,
			RoomTypeId = roomType.Id,
			Capacity = 5,
			TotalBenches = 5,
			BenchCapacity = 1,
			BenchesPerColumn = 5,
			BenchesPerRow = 1
		};
		SetEntityId(room, Guid.NewGuid());
		return await _roomRepository.InsertAsync(room, autoSave: true);
	}

	private static void SetEntityId<TEntity>(TEntity entity, Guid id)
		where TEntity : Entity<Guid>
	{
		typeof(TEntity).GetProperty(nameof(Entity<Guid>.Id))?.SetValue(entity, id);
	}

	private async Task<(Exam exam, Division division)> CreateScheduledExamAsync()
	{
		using (_currentTenant.Change(TestTenantId))
		{
			var stageId = await EmployeeOrganizationTestHelper.EnsureOrganizationExistsAsync(ServiceProvider);
			var stage = await _stageRepository.GetAsync(stageId);
			var unique = Guid.NewGuid().ToString("N");

			var standard = await _standardManager.CreateAsync(
				stageId,
				$"Seat Std {unique}"[..30],
				6,
				shiftId: null);
			await _standardRepository.InsertAsync(standard, autoSave: true);

			var division = await _divisionManager.CreateAsync(
				standard.Id,
				$"A-{unique}"[..30],
				30);
			await _divisionRepository.InsertAsync(division, autoSave: true);

			var subject = new Subject(
				Guid.NewGuid(),
				TestTenantId,
				$"Math {unique}"[..30],
				"#00A76F",
				stage.EducationMediumId);
			await _subjectRepository.InsertAsync(subject, autoSave: true);

			var examType = await _examTypeManager.CreateAsync(
				$"Seat Exam {unique}"[..30],
				description: null,
				stage.EducationMediumId);

			var exam = _examManager.Create(
				examType.Id,
				$"Seating Plan Exam {unique}"[..40],
				standard.Id,
				new List<Guid> { division.Id },
				new List<Guid> { subject.Id });
			exam.ChangeStatus(ExamStatus.Published);
			await _examRepository.InsertAsync(exam, autoSave: true);

			return (exam, division);
		}
	}

	private async Task<(Exam exam, List<Guid> examSubjectIds)> CreateSessionExamAsync(
		bool scheduleSecondSubject,
		bool secondSubjectOnDifferentDate = false)
	{
		var stageId = await EmployeeOrganizationTestHelper.EnsureOrganizationExistsAsync(ServiceProvider);
		var stage = await _stageRepository.GetAsync(stageId);
		var unique = Guid.NewGuid().ToString("N");

		var standard = await _standardManager.CreateAsync(
			stageId,
			$"Session Std {unique}"[..30],
			8,
			shiftId: null);
		await _standardRepository.InsertAsync(standard, autoSave: true);

		var division = await _divisionManager.CreateAsync(standard.Id, $"A-{unique}"[..30], 30);
		await _divisionRepository.InsertAsync(division, autoSave: true);

		var subjects = new[]
		{
			new Subject(Guid.NewGuid(), TestTenantId, $"Math {unique}"[..30], "#00A76F", stage.EducationMediumId),
			new Subject(Guid.NewGuid(), TestTenantId, $"Science {unique}"[..30], "#6750A4", stage.EducationMediumId)
		};
		await _subjectRepository.InsertManyAsync(subjects, autoSave: true);

		var examType = await _examTypeManager.CreateAsync(
			$"Session Exam {unique}"[..30],
			description: null,
			stage.EducationMediumId);
		var exam = _examManager.Create(
			examType.Id,
			$"Session Seating {unique}"[..40],
			standard.Id,
			new List<Guid> { division.Id },
			subjects.Select(subject => subject.Id).ToList());

		var examDate = DateTime.Now.Date.AddDays(10);
		var sessionSubjects = exam.ExamSubjects.ToList();
		sessionSubjects[0].UpdateSchedule(
			examDate,
			examDate.AddHours(9),
			examDate.AddHours(12),
			100,
			33,
			notes: null);
		if (scheduleSecondSubject)
		{
			var secondExamDate = secondSubjectOnDifferentDate ? examDate.AddDays(1) : examDate;
			sessionSubjects[1].UpdateSchedule(
				secondExamDate,
				secondExamDate.AddHours(9),
				secondExamDate.AddHours(12),
				100,
				33,
				notes: null);
		}

		exam.ChangeStatus(ExamStatus.Published);
		await _examRepository.InsertAsync(exam, autoSave: true);

		return (exam, sessionSubjects.Select(subject => subject.Id).ToList());
	}

	private static Student CreateStudent(
		Guid academicYearId,
		Guid educationBoardId,
		Guid educationMediumId,
		Guid educationMediumStageId,
		Guid standardId,
		Guid divisionId,
		int rollNo,
		string unique)
	{
		return new Student(
			Guid.NewGuid(),
			TestTenantId,
			$"Seat{rollNo}",
			null,
			$"Student{unique}"[..30],
			academicYearId,
			educationBoardId,
			educationMediumId,
			educationMediumStageId,
			standardId,
			divisionId,
			grNo: rollNo,
			rollNo: rollNo,
			status: StudentLifeCycleStatusEnum.Enrolled);
	}
}
