using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Academic.Curriculum.Settings;
using ShikshaMitra.Academic.Examination.Exams.Dtos;
using ShikshaMitra.Academic.Examination.Settings;
using ShikshaMitra.Common;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.Standards;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;
using Xunit;

namespace ShikshaMitra.Academic.Examination.Exams;

public class ExamAppService_ScheduleStatus_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	private static readonly Guid TestTenantId = Guid.Parse("22222222-2222-2222-2222-222222222222");

	private readonly IExamAppService _examAppService;
	private readonly IRepository<EducationMediumStage, Guid> _stageRepository;
	private readonly IRepository<Standard, Guid> _standardRepository;
	private readonly IRepository<Division, Guid> _divisionRepository;
	private readonly IRepository<Subject, Guid> _subjectRepository;
	private readonly IRepository<Exam, Guid> _examRepository;
	private readonly IRepository<AssignedSubject, Guid> _assignedSubjectRepository;
	private readonly IRepository<ElectiveGroup, Guid> _electiveGroupRepository;
	private readonly IRepository<GradingScale, Guid> _gradingScaleRepository;
	private readonly IRepository<Employee, Guid> _employeeRepository;
	private readonly IRepository<IdentityUser, Guid> _identityUserRepository;
	private readonly IRepository<IdentityRole, Guid> _identityRoleRepository;
	private readonly StandardManager _standardManager;
	private readonly DivisionManager _divisionManager;
	private readonly ExamTypeManager _examTypeManager;
	private readonly ExamManager _examManager;
	private readonly ICurrentTenant _currentTenant;
	private readonly ICurrentPrincipalAccessor _currentPrincipalAccessor;

	public ExamAppService_ScheduleStatus_Tests()
	{
		_examAppService = GetRequiredService<IExamAppService>();
		_stageRepository = GetRequiredService<IRepository<EducationMediumStage, Guid>>();
		_standardRepository = GetRequiredService<IRepository<Standard, Guid>>();
		_divisionRepository = GetRequiredService<IRepository<Division, Guid>>();
		_subjectRepository = GetRequiredService<IRepository<Subject, Guid>>();
		_examRepository = GetRequiredService<IRepository<Exam, Guid>>();
		_assignedSubjectRepository = GetRequiredService<IRepository<AssignedSubject, Guid>>();
		_electiveGroupRepository = GetRequiredService<IRepository<ElectiveGroup, Guid>>();
		_gradingScaleRepository = GetRequiredService<IRepository<GradingScale, Guid>>();
		_employeeRepository = GetRequiredService<IRepository<Employee, Guid>>();
		_identityUserRepository = GetRequiredService<IRepository<IdentityUser, Guid>>();
		_identityRoleRepository = GetRequiredService<IRepository<IdentityRole, Guid>>();
		_standardManager = GetRequiredService<StandardManager>();
		_divisionManager = GetRequiredService<DivisionManager>();
		_examTypeManager = GetRequiredService<ExamTypeManager>();
		_examManager = GetRequiredService<ExamManager>();
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
	public async Task GetListAsync_Should_Report_Future_Completed_Exam_As_Published()
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
					MaxResultCount = 10
				});

				var completed = await _examAppService.GetListAsync(new GetExamsInput
				{
					AcademicMediumId = mediumId,
					Status = ExamStatus.Completed,
					MaxResultCount = 10
				});

				published.Items.Select(item => item.ExamId).ShouldContain(exam.Id);
				published.Items.Single(item => item.ExamId == exam.Id).Status.ShouldBe(ExamStatus.Published);
				completed.Items.Select(item => item.ExamId).ShouldNotContain(exam.Id);
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

	private async Task<(Exam exam, Guid mediumId)> CreateScheduledExamAsync(ExamStatus status)
	{
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

		var firstExamDate = DateTime.Now.Date.AddDays(1);
		var finalExamDate = DateTime.Now.Date.AddDays(6);
		exam.ExamSubjects.Single(subject => subject.SubjectId == firstSubject.Id)
			.UpdateSchedule(
				firstExamDate,
				firstExamDate.AddHours(14),
				firstExamDate.AddHours(15),
				100,
				33,
				notes: null);
		exam.ExamSubjects.Single(subject => subject.SubjectId == finalSubject.Id)
			.UpdateSchedule(
				finalExamDate,
				finalExamDate.AddHours(14),
				finalExamDate.AddHours(15),
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
