using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Calendar;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Academic.Curriculum.Settings;
using ShikshaMitra.Academic.Examination.Exams.Dtos;
using ShikshaMitra.Academic.Examination.Settings;
using ShikshaMitra.Common;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Students;
using ShikshaMitra.Students.StudentEnrollments;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;
using Xunit;

namespace ShikshaMitra.Academic.Examination.Exams;

public partial class ExamAppService_ScheduleStatus_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	private static readonly Guid TestTenantId = Guid.Parse("22222222-2222-2222-2222-222222222222");
	private static readonly Guid FakeAdminUserId = Guid.Parse("2e701e62-0953-4dd3-910b-dc6cc93ccb0d");

	private readonly IExamAppService _examAppService;
	private readonly IAcademicCalendarAppService _calendarAppService;
	private readonly IRepository<AcademicCalendar, Guid> _calendarRepository;
	private readonly IRepository<AcademicCalendarDay, Guid> _calendarDayRepository;
	private readonly IRepository<EducationMediumStage, Guid> _stageRepository;
	private readonly IRepository<EducationMedium, Guid> _mediumRepository;
	private readonly IRepository<EducationBoard, Guid> _boardRepository;
	private readonly IRepository<Standard, Guid> _standardRepository;
	private readonly IRepository<Division, Guid> _divisionRepository;
	private readonly IRepository<Subject, Guid> _subjectRepository;
	private readonly IRepository<Exam, Guid> _examRepository;
	private readonly IRepository<AssignedSubject, Guid> _assignedSubjectRepository;
	private readonly IRepository<ElectiveGroup, Guid> _electiveGroupRepository;
	private readonly IRepository<Student, Guid> _studentRepository;
	private readonly IRepository<StudentElectiveSelection, Guid> _selectionRepository;
	private readonly IRepository<UserScopeAssignment, Guid> _scopeRepository;
	private readonly IRepository<GradingScale, Guid> _gradingScaleRepository;
	private readonly IRepository<Employee, Guid> _employeeRepository;
	private readonly IRepository<IdentityUser, Guid> _identityUserRepository;
	private readonly IRepository<IdentityRole, Guid> _identityRoleRepository;
	private readonly StandardManager _standardManager;
	private readonly DivisionManager _divisionManager;
	private readonly ExamTypeManager _examTypeManager;
	private readonly ExamManager _examManager;
	private readonly EducationMediumManager _educationMediumManager;
	private readonly IUserScopeService _userScopeService;
	private readonly ICurrentTenant _currentTenant;
	private readonly ICurrentPrincipalAccessor _currentPrincipalAccessor;

	public ExamAppService_ScheduleStatus_Tests()
	{
		_examAppService = GetRequiredService<IExamAppService>();
		_calendarAppService = GetRequiredService<IAcademicCalendarAppService>();
		_calendarRepository = GetRequiredService<IRepository<AcademicCalendar, Guid>>();
		_calendarDayRepository = GetRequiredService<IRepository<AcademicCalendarDay, Guid>>();
		_stageRepository = GetRequiredService<IRepository<EducationMediumStage, Guid>>();
		_mediumRepository = GetRequiredService<IRepository<EducationMedium, Guid>>();
		_boardRepository = GetRequiredService<IRepository<EducationBoard, Guid>>();
		_standardRepository = GetRequiredService<IRepository<Standard, Guid>>();
		_divisionRepository = GetRequiredService<IRepository<Division, Guid>>();
		_subjectRepository = GetRequiredService<IRepository<Subject, Guid>>();
		_examRepository = GetRequiredService<IRepository<Exam, Guid>>();
		_assignedSubjectRepository = GetRequiredService<IRepository<AssignedSubject, Guid>>();
		_electiveGroupRepository = GetRequiredService<IRepository<ElectiveGroup, Guid>>();
		_studentRepository = GetRequiredService<IRepository<Student, Guid>>();
		_selectionRepository = GetRequiredService<IRepository<StudentElectiveSelection, Guid>>();
		_scopeRepository = GetRequiredService<IRepository<UserScopeAssignment, Guid>>();
		_gradingScaleRepository = GetRequiredService<IRepository<GradingScale, Guid>>();
		_employeeRepository = GetRequiredService<IRepository<Employee, Guid>>();
		_identityUserRepository = GetRequiredService<IRepository<IdentityUser, Guid>>();
		_identityRoleRepository = GetRequiredService<IRepository<IdentityRole, Guid>>();
		_standardManager = GetRequiredService<StandardManager>();
		_divisionManager = GetRequiredService<DivisionManager>();
		_examTypeManager = GetRequiredService<ExamTypeManager>();
		_examManager = GetRequiredService<ExamManager>();
		_educationMediumManager = GetRequiredService<EducationMediumManager>();
		_userScopeService = GetRequiredService<IUserScopeService>();
		_currentTenant = GetRequiredService<ICurrentTenant>();
		_currentPrincipalAccessor = GetRequiredService<ICurrentPrincipalAccessor>();
	}

	[Fact]
	public async Task CreateBasicDetailsAsync_Should_Allow_Primary_Teacher_For_Exact_Assigned_Subjects()
	{
		await WithUnitOfWorkAsync(async () =>
		{
			using (_currentTenant.Change(TestTenantId))
			{
				var stageId = await EmployeeOrganizationTestHelper.EnsureOrganizationExistsAsync(ServiceProvider);
				var stage = await _stageRepository.GetAsync(stageId);
				var unique = Guid.NewGuid().ToString("N");
				var teacherUserId = Guid.NewGuid();
				var teacher = await CreateTeacherAsync(teacherUserId, unique);

				var standard = await _standardManager.CreateAsync(
					stageId,
					$"Teacher Exam {unique}"[..30],
					12,
					shiftId: null);
				await _standardRepository.InsertAsync(standard, autoSave: true);

				var division = await _divisionManager.CreateAsync(standard.Id, $"A-{unique}"[..30], 35);
				await _divisionRepository.InsertAsync(division, autoSave: true);

				var assignedSubject = await CreatePrimaryTeacherSubjectAsync(
					standard.Id,
					stage.EducationMediumId,
					teacher.Id,
					$"Core Mathematics {unique}"[..30]);
				var otherSubject = await CreateSubjectAsync(
					stage.EducationMediumId,
					$"Unallocated Subject {unique}"[..30]);

				var examType = await _examTypeManager.CreateAsync(
					$"Practical {unique}"[..30],
					description: null,
					stage.EducationMediumId);
				var gradingScale = await _gradingScaleRepository.InsertAsync(
					new GradingScale(
						Guid.NewGuid(),
						TestTenantId,
						$"Main Grade {unique}"[..30],
						"100",
						stage.EducationMediumId),
					autoSave: true);

				using (_currentPrincipalAccessor.Change(BuildTeacherPrincipal(teacherUserId)))
				{
					var examId = await _examAppService.CreateBasicDetailsAsync(new ExamBasicDetailsRequestDto
					{
						ExamTypeId = examType.Id,
						ExamName = $"Allocated Exam {unique}"[..40],
						StandardId = standard.Id,
						GradingScaleId = gradingScale.Id,
						DivisionIds = [division.Id],
						SubjectIds = [assignedSubject.SubjectId]
					});

					var saved = await _examAppService.GetBasicDetailsAsync(examId);
					saved.SubjectIds.ShouldBe([assignedSubject.SubjectId]);
					saved.DivisionIds.ShouldBe([division.Id]);
					var visibleExams = await _examAppService.GetListAsync(new GetExamsInput
					{
						AcademicMediumId = stage.EducationMediumId,
						Status = ExamStatus.Pending,
						MaxResultCount = 10
					});
					visibleExams.Items.Select(item => item.ExamId).ShouldContain(examId);

					var denied = await Should.ThrowAsync<BusinessException>(() =>
						_examAppService.CreateBasicDetailsAsync(new ExamBasicDetailsRequestDto
						{
							ExamTypeId = examType.Id,
							ExamName = $"Mixed Scope Exam {unique}"[..40],
							StandardId = standard.Id,
							GradingScaleId = gradingScale.Id,
							DivisionIds = [division.Id],
							SubjectIds = [assignedSubject.SubjectId, otherSubject.Id]
						}));
					denied.Code.ShouldBe(UserScopeErrorCodes.AccessDenied);
				}
			}
		});
	}

	[Fact]
	public async Task ChangeStatusAsync_Should_Reject_Completion_Before_Final_Subject_End_Time()
	{
		await WithUnitOfWorkAsync(async () =>
		{
			using (_currentTenant.Change(TestTenantId))
			{
				var (exam, _) = await CreateScheduledExamAsync(ExamStatus.Published);

				var exception = await Should.ThrowAsync<BusinessException>(() =>
					_examAppService.ChangeStatusAsync(exam.Id, ExamStatus.Completed));

				exception.Code.ShouldBe("ShikshaMitra.Exam:CompletionBeforeScheduleEnd");
			}
		});
	}

	[Fact]
	public async Task GetListAsync_Should_Move_Published_Exam_To_Ongoing_During_Schedule()
	{
		await WithUnitOfWorkAsync(async () =>
		{
			using (_currentTenant.Change(TestTenantId))
			{
				await _scopeRepository.InsertAsync(
					new UserScopeAssignment(
						Guid.NewGuid(),
						TestTenantId,
						userId: FakeAdminUserId,
						description: "Tenant-wide live exam status test scope"),
					autoSave: true);
				await _userScopeService.InvalidateUserScopeCacheAsync(FakeAdminUserId);

				var (exam, mediumId) = await CreateScheduledExamAsync(
					ExamStatus.Published,
					activeToday: true);

				var all = await _examAppService.GetListAsync(new GetExamsInput
				{
					AcademicMediumId = mediumId,
					MaxResultCount = 1000
				});
				var published = await _examAppService.GetListAsync(new GetExamsInput
				{
					AcademicMediumId = mediumId,
					Status = ExamStatus.Published,
					MaxResultCount = 10
				});
				var ongoing = await _examAppService.GetListAsync(new GetExamsInput
				{
					AcademicMediumId = mediumId,
					Status = ExamStatus.Ongoing,
					MaxResultCount = 10
				});

				all.Items.Single(item => item.ExamId == exam.Id).Status.ShouldBe(ExamStatus.Ongoing);
				published.Items.Select(item => item.ExamId).ShouldNotContain(exam.Id);
				ongoing.Items.Select(item => item.ExamId).ShouldContain(exam.Id);
				ongoing.Items.Single(item => item.ExamId == exam.Id).Status.ShouldBe(ExamStatus.Ongoing);
				ongoing.OngoingCount.ShouldBeGreaterThanOrEqualTo(1);
			}
		});
	}

	[Fact]
	public async Task GetListAsync_Should_Keep_Persisted_Completed_Status_Before_A_Future_Schedule()
	{
		await WithUnitOfWorkAsync(async () =>
		{
			using (_currentTenant.Change(TestTenantId))
			{
				var (exam, mediumId) = await CreateScheduledExamAsync(ExamStatus.Completed);

				var published = await _examAppService.GetListAsync(new GetExamsInput
				{
					AcademicMediumId = mediumId,
					Status = ExamStatus.Published,
					StartDate = DateTime.Today,
					EndDate = DateTime.Today.AddDays(7),
					MaxResultCount = 10
				});

				var completed = await _examAppService.GetListAsync(new GetExamsInput
				{
					AcademicMediumId = mediumId,
					Status = ExamStatus.Completed,
					StartDate = DateTime.Today,
					EndDate = DateTime.Today.AddDays(7),
					MaxResultCount = 10
				});

				published.Items.Select(item => item.ExamId).ShouldNotContain(exam.Id);
				completed.Items.Single(item => item.ExamId == exam.Id).Status.ShouldBe(ExamStatus.Completed);
			}
		});
	}

	[Fact]
	public async Task GetListAsync_Should_Return_Start_And_End_Date_For_Scheduled_Pending_Exam()
	{
		await WithUnitOfWorkAsync(async () =>
		{
			using (_currentTenant.Change(TestTenantId))
			{
				var (exam, mediumId) = await CreateScheduledExamAsync(ExamStatus.Pending);

				var result = await _examAppService.GetListAsync(new GetExamsInput
				{
					AcademicMediumId = mediumId,
					Status = ExamStatus.Pending,
					MaxResultCount = 10
				});

				var item = result.Items.Single(item => item.ExamId == exam.Id);
				item.SubjectCount.ShouldBe(2);
				item.StartDate.ShouldBe(DateTime.Now.Date.AddDays(1));
				item.EndDate.ShouldBe(DateTime.Now.Date.AddDays(6).AddHours(15));
			}
		});
	}

	[Fact]
	public async Task GetListAsync_Should_Return_Undated_Pending_Exam_When_Explicit_Date_Range_Is_Used()
	{
		await WithUnitOfWorkAsync(async () =>
		{
			using (_currentTenant.Change(TestTenantId))
			{
				var (exam, mediumId) = await CreateScheduledExamAsync(ExamStatus.Pending);
				foreach (var subject in exam.ExamSubjects)
				{
					subject.UpdateSchedule(null, null, null, null, null, notes: null);
				}
				await _examRepository.UpdateAsync(exam, autoSave: true);

				var today = DateTime.Today;
				var result = await _examAppService.GetListAsync(new GetExamsInput
				{
					AcademicMediumId = mediumId,
					Status = ExamStatus.Pending,
					StartDate = today,
					EndDate = today.AddDays(6),
					MaxResultCount = 10
				});

				var item = result.Items.Single(item => item.ExamId == exam.Id);
				item.Status.ShouldBe(ExamStatus.Pending);
				item.StartDate.ShouldBeNull();
				item.EndDate.ShouldBeNull();
			}
		});
	}

	[Fact]
	public async Task GetListAsync_Should_Return_Correct_Subject_Count_For_Assigned_Subjects()
	{
		await WithUnitOfWorkAsync(async () =>
		{
			using (_currentTenant.Change(TestTenantId))
			{
				var (exam, mediumId) = await CreateScheduledExamAsync(ExamStatus.Pending);

				var result = await _examAppService.GetListAsync(new GetExamsInput
				{
					AcademicMediumId = mediumId,
					Status = ExamStatus.Pending,
					MaxResultCount = 10
				});

				var item = result.Items.Single(item => item.ExamId == exam.Id);
				item.SubjectCount.ShouldBe(exam.ExamSubjects.Count);
				item.SubjectCount.ShouldBe(2);
			}
		});
	}

	[Fact]
	public async Task GetSubjectsAsync_Should_Expand_Elective_Group_Subjects()
	{
		await WithUnitOfWorkAsync(async () =>
		{
			using (_currentTenant.Change(TestTenantId))
			{
				await EnsureDefaultExamTestScopeAsync();
				var stageId = await EmployeeOrganizationTestHelper.EnsureOrganizationExistsAsync(ServiceProvider);
				var stage = await _stageRepository.GetAsync(stageId);
				var unique = Guid.NewGuid().ToString("N");

				var standard = await _standardManager.CreateAsync(
					stageId,
					$"Exam Std {unique}"[..30],
					7,
					shiftId: null);
				await _standardRepository.InsertAsync(standard, autoSave: true);

				var division = await _divisionManager.CreateAsync(
					standard.Id,
					$"A-{unique}"[..30],
					35);
				await _divisionRepository.InsertAsync(division, autoSave: true);

				var computer = await CreateSubjectAsync(stage.EducationMediumId, $"Computer {unique}"[..30]);
				var pt = await CreateSubjectAsync(stage.EducationMediumId, $"Pt {unique}"[..30]);
				var electiveGroup = new ElectiveGroup(
					Guid.NewGuid(),
					TestTenantId,
					$"Pt & Computer {unique}"[..30],
					"#FFAB00",
					stage.EducationMediumId);
				electiveGroup.AddSubject(computer.Id);
				electiveGroup.AddSubject(pt.Id);
				await _electiveGroupRepository.InsertAsync(electiveGroup, autoSave: true);

				await _assignedSubjectRepository.InsertManyAsync(
					[
						new AssignedSubject(
							Guid.NewGuid(),
							TestTenantId,
							standard.Id,
							computer.Id,
							"COM06",
							SubjectStatus.Published,
							SubjectType.Elective,
							electiveGroup.Id),
						new AssignedSubject(
							Guid.NewGuid(),
							TestTenantId,
							standard.Id,
							pt.Id,
							"PT07",
							SubjectStatus.Published,
							SubjectType.Elective,
							electiveGroup.Id)
					],
					autoSave: true);

				var examType = await _examTypeManager.CreateAsync(
					$"Mid Term {unique}"[..30],
					description: null,
					stage.EducationMediumId);

				var exam = _examManager.Create(
					examType.Id,
					$"Winter Exam {unique}"[..40],
					standard.Id,
					new List<Guid> { division.Id },
					new List<Guid> { computer.Id });
				await _examRepository.InsertAsync(exam, autoSave: true);

				var result = await _examAppService.GetSubjectsAsync(exam.Id);

				result.Select(subject => subject.SubjectId).ShouldBe([computer.Id, pt.Id], ignoreOrder: true);
				result.ShouldAllBe(subject => subject.ElectiveGroupId == electiveGroup.Id);
				result.ShouldAllBe(subject => subject.ElectiveGroupName == electiveGroup.Name);
				result.Single(subject => subject.SubjectId == computer.Id).ExamSubjectId.ShouldNotBe(Guid.Empty);
				result.Single(subject => subject.SubjectId == pt.Id).ExamSubjectId.ShouldBe(Guid.Empty);
			}
		});
	}

	[Fact]
	public async Task SaveScheduleAsync_Should_Create_Fully_Linked_Markers_Only_In_The_Exam_Medium()
	{
		await WithUnitOfWorkAsync(async () =>
		{
			using (_currentTenant.Change(TestTenantId))
			{
				await _scopeRepository.InsertAsync(
					new UserScopeAssignment(
						Guid.NewGuid(),
						TestTenantId,
						userId: FakeAdminUserId,
						description: "Tenant-wide academic calendar sync test scope"),
					autoSave: true);
				await _userScopeService.InvalidateUserScopeCacheAsync(FakeAdminUserId);

				var (exam, mediumId) = await CreateScheduledExamAsync(ExamStatus.Pending);
				var medium = await _mediumRepository.GetAsync(mediumId);
				var otherMedium = await _educationMediumManager.CreateAsync(
					medium.EducationBoardId,
					$"Other {Guid.NewGuid():N}"[..30]);
				await _mediumRepository.InsertAsync(otherMedium, autoSave: true);

				var calendarStart = DateTime.Today;
				var calendarEnd = DateTime.Today.AddMonths(1);
				var expectedCalendar = new AcademicCalendar(
					Guid.NewGuid(), TestTenantId, mediumId, null, "Expected calendar",
					calendarStart, calendarEnd, isDefault: true);
				var unrelatedCalendar = new AcademicCalendar(
					Guid.NewGuid(), TestTenantId, otherMedium.Id, null, "Other medium calendar",
					calendarStart, calendarEnd, isDefault: true);
				unrelatedCalendar.Publish(FakeAdminUserId);
				await _calendarRepository.InsertManyAsync(
					[expectedCalendar, unrelatedCalendar],
					autoSave: true);

				await _examAppService.SaveScheduleAsync(exam.Id, new ExamScheduleRequestDto
				{
					SyncToAcademicCalendar = true,
					Subjects = exam.ExamSubjects.Select(subject => new ExamSubjectScheduleDto
					{
						SubjectId = subject.SubjectId,
						ExamDate = subject.ExamDate,
						StartTime = subject.StartTime,
						EndTime = subject.EndTime,
						TotalMarks = subject.TotalMarks,
						PassingMarks = subject.PassingMarks
					}).ToList()
				});

				var markers = await _calendarDayRepository.GetListAsync(day =>
					day.LinkedExamId == exam.Id && day.Status == CalendarDayStatus.Active);
				markers.Count.ShouldBe(2);
				markers.ShouldAllBe(day => day.AcademicCalendarId == expectedCalendar.Id);
				markers.ShouldAllBe(day => day.ExamTypeId == exam.ExamTypeId);
				markers.ShouldAllBe(day => day.StandardId == exam.StandardId);
				markers.ShouldAllBe(day => day.ExamStartTime.HasValue && day.ExamEndTime.HasValue);

				// Markers produced before the linkage fix only carried the exam id in this
				// stable sync description. The read model must still hydrate those rows.
				await _calendarDayRepository.InsertAsync(
					new AcademicCalendarDay(
						Guid.NewGuid(),
						TestTenantId,
						expectedCalendar.Id,
						DateTime.Today.AddDays(10),
						DayType.Exam,
						"Legacy exam marker",
						$"{AcademicCalendarConsts.ExamSyncDescriptionPrefix}{exam.Id}",
						appliesToAll: false,
						standardId: exam.StandardId),
					autoSave: true);

				var calendarDays = await _calendarAppService.GetCalendarDaysAsync(
					expectedCalendar.Id,
					dayType: DayType.Exam,
					month: null,
					year: null);
				calendarDays.Count.ShouldBe(3);
				calendarDays.ShouldAllBe(day => day.LinkedExamId == exam.Id);
				calendarDays.ShouldAllBe(day => day.LinkedExamName == exam.ExamName);
				calendarDays.ShouldAllBe(day => !string.IsNullOrWhiteSpace(day.ExamTypeName));
				calendarDays.ShouldAllBe(day => !string.IsNullOrWhiteSpace(day.StandardName));
			}
		});
	}

	[Fact]
	public async Task GetScheduleAsync_Should_Exclude_Unselected_Elective_Group_Subjects()
	{
		await WithUnitOfWorkAsync(async () =>
		{
			using (_currentTenant.Change(TestTenantId))
			{
				await _scopeRepository.InsertAsync(
					new UserScopeAssignment(
						Guid.NewGuid(),
						TestTenantId,
						userId: FakeAdminUserId,
						description: "Tenant-wide elective schedule projection test scope"),
					autoSave: true);
				await _userScopeService.InvalidateUserScopeCacheAsync(FakeAdminUserId);

				var stageId = await EmployeeOrganizationTestHelper.EnsureOrganizationExistsAsync(ServiceProvider);
				var stage = await _stageRepository.GetAsync(stageId);
				var medium = await _mediumRepository.GetAsync(stage.EducationMediumId);
				var board = await _boardRepository.GetAsync(medium.EducationBoardId);
				var unique = Guid.NewGuid().ToString("N");

				var standard = await _standardManager.CreateAsync(
					stageId,
					$"Elective Exam {unique}"[..30],
					12,
					shiftId: null);
				await _standardRepository.InsertAsync(standard, autoSave: true);

				var division = await _divisionManager.CreateAsync(standard.Id, $"A-{unique}"[..30], 35);
				await _divisionRepository.InsertAsync(division, autoSave: true);

				var coreMathematics = await CreateSubjectAsync(
					medium.Id,
					$"Core Mathematics {unique}"[..30]);
				var appliedMathematics = await CreateSubjectAsync(
					medium.Id,
					$"Applied Mathematics {unique}"[..30]);
				var electiveGroup = new ElectiveGroup(
					Guid.NewGuid(),
					TestTenantId,
					$"12th Eleven Maths {unique}"[..30],
					"#FFAB00",
					medium.Id);
				electiveGroup.AddSubject(coreMathematics.Id);
				electiveGroup.AddSubject(appliedMathematics.Id);
				await _electiveGroupRepository.InsertAsync(electiveGroup, autoSave: true);

				await _assignedSubjectRepository.InsertManyAsync(
					[
						new AssignedSubject(
							Guid.NewGuid(), TestTenantId, standard.Id, coreMathematics.Id,
							$"CORE{unique[..5]}", SubjectStatus.Published, SubjectType.Elective, electiveGroup.Id),
						new AssignedSubject(
							Guid.NewGuid(), TestTenantId, standard.Id, appliedMathematics.Id,
							$"APP{unique[..5]}", SubjectStatus.Published, SubjectType.Elective, electiveGroup.Id)
					],
					autoSave: true);

				var coreStudent = CreateApprovedStudent(
					"Core", 1, board.AcademicYearId, board.Id, medium.Id, stage.Id,
					standard.Id, division.Id, unique);
				var appliedStudent = CreateApprovedStudent(
					"Applied", 2, board.AcademicYearId, board.Id, medium.Id, stage.Id,
					standard.Id, division.Id, unique);
				await _studentRepository.InsertManyAsync([coreStudent, appliedStudent], autoSave: true);

				var coreSelection = new StudentElectiveSelection(
					Guid.NewGuid(), TestTenantId, coreStudent.Id, electiveGroup.Id,
					coreMathematics.Id, medium.Id);
				coreSelection.LockSelection();
				var appliedSelection = new StudentElectiveSelection(
					Guid.NewGuid(), TestTenantId, appliedStudent.Id, electiveGroup.Id,
					appliedMathematics.Id, medium.Id);
				appliedSelection.LockSelection();
				await _selectionRepository.InsertManyAsync(
					[coreSelection, appliedSelection],
					autoSave: true);

				var examType = await _examTypeManager.CreateAsync(
					$"Final {unique}"[..30],
					description: null,
					medium.Id);
				var exam = _examManager.Create(
					examType.Id,
					$"Applied Maths {unique}"[..40],
					standard.Id,
					[division.Id],
					[appliedMathematics.Id]);
				var examDate = DateTime.Today.AddDays(1);
				exam.ExamSubjects.Single().UpdateSchedule(
					examDate,
					examDate.AddHours(12),
					examDate.AddHours(13),
					100,
					33,
					notes: null);
				await _examRepository.InsertAsync(exam, autoSave: true);

				var result = await _examAppService.GetScheduleAsync(exam.Id);

				result.Subjects.Count.ShouldBe(1);
				var groupSchedule = result.Subjects.Single();
				groupSchedule.ElectiveGroupId.ShouldBe(electiveGroup.Id);
				groupSchedule.ElectiveGroupSubjects
					.Select(subject => subject.SubjectId)
					.ShouldBe([appliedMathematics.Id]);
			}
		});
	}

	private async Task EnsureDefaultExamTestScopeAsync()
	{
		if (!await _scopeRepository.AnyAsync(scope => scope.UserId == FakeAdminUserId))
		{
			await _scopeRepository.InsertAsync(new UserScopeAssignment(Guid.NewGuid(), TestTenantId,
				userId: FakeAdminUserId, description: "Exam schedule test operator scope"), autoSave: true);
			await _userScopeService.InvalidateUserScopeCacheAsync(FakeAdminUserId);
		}
	}

	private async Task<(Exam exam, Guid mediumId)> CreateScheduledExamAsync(
		ExamStatus status,
		bool activeToday = false)
	{
		await EnsureDefaultExamTestScopeAsync();
		var stageId = await EmployeeOrganizationTestHelper.EnsureOrganizationExistsAsync(ServiceProvider);
		var stage = await _stageRepository.GetAsync(stageId);
		var unique = Guid.NewGuid().ToString("N");

		var standard = await _standardManager.CreateAsync(
			stageId,
			$"Exam Std {unique}"[..30],
			7,
			shiftId: null);
		await _standardRepository.InsertAsync(standard, autoSave: true);

		var division = await _divisionManager.CreateAsync(
			standard.Id,
			$"A-{unique}"[..30],
			35);
		await _divisionRepository.InsertAsync(division, autoSave: true);

		var firstSubject = await CreateSubjectAsync(stage.EducationMediumId, $"Science {unique}"[..30]);
		var finalSubject = await CreateSubjectAsync(stage.EducationMediumId, $"Social Science {unique}"[..30]);

		var examType = await _examTypeManager.CreateAsync(
			$"Mid Term {unique}"[..30],
			description: null,
			stage.EducationMediumId);

		var exam = _examManager.Create(
			examType.Id,
			$"Janmashtami Vacation {unique}"[..40],
			standard.Id,
			new List<Guid> { division.Id },
			new List<Guid> { firstSubject.Id, finalSubject.Id });

		var firstExamDate = DateTime.Now.Date.AddDays(activeToday ? 0 : 1);
		var finalExamDate = DateTime.Now.Date.AddDays(activeToday ? 0 : 6);
		exam.ExamSubjects.Single(subject => subject.SubjectId == firstSubject.Id)
			.UpdateSchedule(
				firstExamDate,
				firstExamDate.AddHours(activeToday ? 0 : 14),
				firstExamDate.AddHours(activeToday ? 12 : 15),
				100,
				33,
				notes: null);
		exam.ExamSubjects.Single(subject => subject.SubjectId == finalSubject.Id)
			.UpdateSchedule(
				finalExamDate,
				finalExamDate.AddHours(activeToday ? 12 : 14),
				finalExamDate.AddHours(activeToday ? 24 : 15).AddTicks(activeToday ? -1 : 0),
				100,
				33,
				notes: null);

		exam.ChangeStatus(status);
		await _examRepository.InsertAsync(exam, autoSave: true);

		return (exam, stage.EducationMediumId);
	}

	private async Task<Subject> CreateSubjectAsync(Guid mediumId, string name)
	{
		var subject = new Subject(
			Guid.NewGuid(),
			TestTenantId,
			name,
			"#00A76F",
			mediumId);
		await _subjectRepository.InsertAsync(subject, autoSave: true);

		return subject;
	}

	private static Student CreateApprovedStudent(
		string firstName,
		int rollNo,
		Guid academicYearId,
		Guid boardId,
		Guid mediumId,
		Guid stageId,
		Guid standardId,
		Guid divisionId,
		string unique)
	{
		var student = new Student(
			Guid.NewGuid(),
			TestTenantId,
			firstName,
			null,
			$"Student {unique}"[..30],
			academicYearId,
			boardId,
			mediumId,
			stageId,
			standardId,
			divisionId,
			grNo: 20000 + rollNo,
			rollNo,
			StudentLifeCycleStatusEnum.Admission);
		student.ApproveAdmission();
		return student;
	}

	private async Task<AssignedSubject> CreatePrimaryTeacherSubjectAsync(
		Guid standardId,
		Guid mediumId,
		Guid teacherEmployeeId,
		string subjectName)
	{
		var subject = await CreateSubjectAsync(mediumId, subjectName);
		var assignedSubject = new AssignedSubject(
			Guid.NewGuid(),
			TestTenantId,
			standardId,
			subject.Id,
			$"SUB-{Guid.NewGuid():N}"[..12],
			SubjectStatus.Published);
		assignedSubject.SetPrimaryTeacher(teacherEmployeeId);
		return await _assignedSubjectRepository.InsertAsync(assignedSubject, autoSave: true);
	}

	private async Task<Employee> CreateTeacherAsync(Guid userId, string unique)
	{
		var roleId = Guid.NewGuid();
		await _identityUserRepository.InsertAsync(
			new IdentityUser(
				userId,
				$"exam-teacher-{unique[..8]}",
				$"{unique[..12]}@test.local",
				TestTenantId),
			autoSave: true);
		await _identityRoleRepository.InsertAsync(
			new IdentityRole(roleId, $"exam-teacher-{unique[..8]}", TestTenantId),
			autoSave: true);

		var employee = new Employee(
			Guid.NewGuid(),
			TestTenantId,
			userId,
			$"EXM{unique[..9]}",
			new EmployeeBasicInfo(
				ProfilePictureBlobName: null,
				LastName: "Patel",
				FirstName: "Akshay",
				MiddleName: null,
				ShortName: "Akshay",
				EmployeeStatusType: EmployeeStatusType.Active,
				DateOfBirth: new DateOnly(1990, 1, 1),
				FamilyCast: null,
				CastType: null,
				GenderType: null,
				Religion: null,
				MaritalStatus: null,
				BloodGroup: null,
				MobileNo: null,
				SecondaryMobileNo: null,
				Email: $"{unique[..12]}@test.local",
				PanNo: $"ABCDE{unique[..4].ToUpperInvariant()}F",
				AdharNo: $"1{Math.Abs((long)unique.GetHashCode()):D11}"[..12],
				Address1: null,
				Address2: null,
				Address3: null,
				Department: "Academics",
				Designation: "Teacher",
				DateOfJoining: new DateOnly(2025, 1, 1),
				EmployeeGroup: EmployeeGroupType.Teaching,
				Qualification: null,
				RoleId: roleId,
				IdentificationMark1: null,
				City: null,
				State: null,
				Country: null,
				PinCode: null,
				ShiftId: null),
			new BankDetails(null, null, null, null, null),
			null);

		return await _employeeRepository.InsertAsync(employee, autoSave: true);
	}

	private static ClaimsPrincipal BuildTeacherPrincipal(Guid userId)
	{
		var claims = new List<Claim>
		{
			new(AbpClaimTypes.UserId, userId.ToString()),
			new(AbpClaimTypes.UserName, "akshay.patel"),
			new(AbpClaimTypes.Role, "teacher"),
			new(AbpClaimTypes.TenantId, TestTenantId.ToString())
		};

		return new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
	}
}
