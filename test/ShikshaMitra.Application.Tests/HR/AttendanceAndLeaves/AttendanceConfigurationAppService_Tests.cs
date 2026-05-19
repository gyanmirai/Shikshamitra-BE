using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Common;
using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Employees.PersonalInfo;
using ShikshaMitra.HR.Payroll.RunPayroll;
using ShikshaMitra.HR.Payroll.SalarySlips;
using ShikshaMitra.TenantSettings;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

public class AttendanceConfigurationAppService_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	private readonly IAttendanceConfigurationAppService _appService;
	private readonly IAttendanceConfigurationRepository _repository;
	private readonly IEmployeePersonalInfoAppService _employeePersonalInfoAppService;
	private readonly IEmployeeAttendanceRepository _employeeAttendanceRepository;
	private readonly IIdentityRoleRepository _roleRepository;
	private readonly IRepository<ShiftManagement, Guid> _shiftRepository;
	private readonly IRepository<PayrollRunItemSnapshot, Guid> _payrollSnapshotRepository;
	private readonly IRepository<PayrollRunReview, Guid> _payrollReviewRepository;
	private readonly IRepository<AttendancePolicyEvaluationRun, Guid> _evaluationRunRepository;
	private readonly IRepository<AttendancePolicyEvaluationResult, Guid> _evaluationResultRepository;
	private readonly ISalarySlipRepository _salarySlipRepository;
	private readonly ICurrentTenant _currentTenant;
	private readonly IGuidGenerator _guidGenerator;

	public AttendanceConfigurationAppService_Tests()
	{
		_appService = GetRequiredService<IAttendanceConfigurationAppService>();
		_repository = GetRequiredService<IAttendanceConfigurationRepository>();
		_employeePersonalInfoAppService = GetRequiredService<IEmployeePersonalInfoAppService>();
		_employeeAttendanceRepository = GetRequiredService<IEmployeeAttendanceRepository>();
		_roleRepository = GetRequiredService<IIdentityRoleRepository>();
		_shiftRepository = GetRequiredService<IRepository<ShiftManagement, Guid>>();
		_payrollSnapshotRepository = GetRequiredService<IRepository<PayrollRunItemSnapshot, Guid>>();
		_payrollReviewRepository = GetRequiredService<IRepository<PayrollRunReview, Guid>>();
		_evaluationRunRepository = GetRequiredService<IRepository<AttendancePolicyEvaluationRun, Guid>>();
		_evaluationResultRepository = GetRequiredService<IRepository<AttendancePolicyEvaluationResult, Guid>>();
		_salarySlipRepository = GetRequiredService<ISalarySlipRepository>();
		_currentTenant = GetRequiredService<ICurrentTenant>();
		_guidGenerator = GetRequiredService<IGuidGenerator>();
	}

	[Fact]
	public async Task Should_Save_And_Roundtrip_Canonical_Full_Day_Threshold()
	{
		using (_currentTenant.Change(Guid.NewGuid()))
		{
			var academicYearId = Guid.NewGuid();
			var request = BuildValidRequest();
			request.PunchRules.FullDayThresholdPercentage = 82;
			request.DailyRules.HoursBasedPayPercentage = 82;

			var response = await _appService.CreateOrUpdateAsync(academicYearId, request);

			response.Success.ShouldBeTrue();
			response.PunchRules!.FullDayThresholdPercentage.ShouldBe(82);
			response.DailyRules!.HoursBasedPayPercentage.ShouldBe(82);
			var saved = await _repository.FindByAcademicYearIdAsync(academicYearId);
			saved.ShouldNotBeNull();
			saved.HoursBasedPayPercentage.ShouldBe(82);
		}
	}

	[Fact]
	public async Task Should_Accept_Legacy_Full_Day_Threshold_When_Punch_Value_Omitted()
	{
		using (_currentTenant.Change(Guid.NewGuid()))
		{
			var request = BuildValidRequest();
			request.PunchRules.FullDayThresholdPercentage = null;
			request.DailyRules.HoursBasedPayPercentage = 88;

			var response = await _appService.CreateOrUpdateAsync(Guid.NewGuid(), request);

			response.PunchRules!.FullDayThresholdPercentage.ShouldBe(88);
			response.DailyRules!.HoursBasedPayPercentage.ShouldBe(88);
		}
	}

	[Fact]
	public async Task Should_Reject_Conflicting_Full_Day_Thresholds()
	{
		using (_currentTenant.Change(Guid.NewGuid()))
		{
			var request = BuildValidRequest();
			request.PunchRules.FullDayThresholdPercentage = 85;
			request.DailyRules.HoursBasedPayPercentage = 75;

			var ex = await Should.ThrowAsync<BusinessException>(() =>
				_appService.CreateOrUpdateAsync(Guid.NewGuid(), request));

			ex.Code.ShouldBe("ShikshaMitra:ConflictingFullDayThreshold");
		}
	}

	[Fact]
	public async Task Should_Preserve_Inactive_Overrides()
	{
		using (_currentTenant.Change(Guid.NewGuid()))
		{
			var request = BuildValidRequest();
			request.Overrides.Add(new AttendancePolicyOverrideRequest
			{
				TargetType = "department",
				TargetId = "Science",
				TargetName = "Science",
				IsActive = false,
				GraceTime = 5
			});

			var response = await _appService.CreateOrUpdateAsync(Guid.NewGuid(), request);

			response.Overrides.Count.ShouldBe(1);
			response.Overrides[0].IsActive.ShouldBeFalse();
			response.Overrides[0].TargetName.ShouldBe("Science");
		}
	}

	[Fact]
	public async Task Should_Reject_Duplicate_Active_Overrides()
	{
		using (_currentTenant.Change(Guid.NewGuid()))
		{
			var request = BuildValidRequest();
			request.Overrides.Add(BuildOverride("role", "role-1", true));
			request.Overrides.Add(BuildOverride("role", "role-1", true));

			var ex = await Should.ThrowAsync<BusinessException>(() =>
				_appService.CreateOrUpdateAsync(Guid.NewGuid(), request));

			ex.Code.ShouldBe("ShikshaMitra:DuplicateAttendancePolicyOverride");
		}
	}

	[Fact]
	public async Task Should_Allow_Duplicate_Inactive_Overrides()
	{
		using (_currentTenant.Change(Guid.NewGuid()))
		{
			var request = BuildValidRequest();
			request.Overrides.Add(BuildOverride("shift", "shift-1", false));
			request.Overrides.Add(BuildOverride("shift", "shift-1", false));

			var response = await _appService.CreateOrUpdateAsync(Guid.NewGuid(), request);

			response.Overrides.Count.ShouldBe(2);
			response.Overrides.All(x => !x.IsActive).ShouldBeTrue();
		}
	}

	[Fact]
	public async Task Should_Accept_Null_Overtime_Details_When_Disabled()
	{
		using (_currentTenant.Change(Guid.NewGuid()))
		{
			var request = BuildValidRequest();
			request.Overtime.Enabled = false;
			request.Overtime.Rate = null;
			request.Overtime.MinOvertimeMinutes = null;

			var response = await _appService.CreateOrUpdateAsync(Guid.NewGuid(), request);

			response.Overtime!.Enabled.ShouldBeFalse();
			response.Overtime.Rate.ShouldBeNull();
			response.Overtime.MinOvertimeMinutes.ShouldBeNull();
		}
	}

	[Fact]
	public async Task Preview_Should_Use_Seeded_Attendance_And_Not_Mutate_Payroll()
	{
		using (_currentTenant.Change(Guid.NewGuid()))
		{
			var roleId = await CreateRoleAsync("preview");
			var shiftId = await CreateShiftAsync("preview");
			var employee = await CreateEmployeeAsync("Preview", roleId, shiftId, "Science", EmployeeGroupType.Teaching);
			await InsertAttendanceAsync(employee.Id, 2026, 4, 6, 9, 20, 18, 30, EmployeeAttendanceStatusType.Present);
			await InsertAttendanceAsync(employee.Id, 2026, 4, 7, null, null, null, null, EmployeeAttendanceStatusType.Present);

			var configCountBefore = await CountAsync(_repository);
			var attendanceCountBefore = await CountAsync(_employeeAttendanceRepository);
			var payrollSnapshotCountBefore = await CountAsync(_payrollSnapshotRepository);
			var payrollReviewCountBefore = await CountAsync(_payrollReviewRepository);
			var salarySlipCountBefore = await CountAsync(_salarySlipRepository);

			var request = BuildPreviewRequest(BuildValidRequest());
			request.Configuration.PunchRules.MissingPunchBehavior = "mark_absent";
			request.Configuration.Overtime.MinOvertimeMinutes = 20;

			var response = await _appService.PreviewAsync(Guid.NewGuid(), request);

			response.AffectedEmployeeCount.ShouldBe(1);
			response.EstimatedLateCount.ShouldBe(1);
			response.EstimatedAbsenceCount.ShouldBe(1);
			response.EstimatedOvertimeMinutes.ShouldBe(70);
			response.EstimatedPayableDayDelta.ShouldBe(-1.5);
			response.Warnings.ShouldContain(x => x.Contains("Preview only"));
			response.Warnings.ShouldContain(x => x.Contains("Payroll currently uses persisted attendance statuses"));

			(await CountAsync(_repository)).ShouldBe(configCountBefore);
			(await CountAsync(_employeeAttendanceRepository)).ShouldBe(attendanceCountBefore);
			(await CountAsync(_payrollSnapshotRepository)).ShouldBe(payrollSnapshotCountBefore);
			(await CountAsync(_payrollReviewRepository)).ShouldBe(payrollReviewCountBefore);
			(await CountAsync(_salarySlipRepository)).ShouldBe(salarySlipCountBefore);
		}
	}

	[Fact]
	public async Task Preview_Filters_Should_Change_Affected_Employee_Count()
	{
		using (_currentTenant.Change(Guid.NewGuid()))
		{
			var roleOne = await CreateRoleAsync("role-one");
			var roleTwo = await CreateRoleAsync("role-two");
			var shiftOne = await CreateShiftAsync("shift-one");
			var shiftTwo = await CreateShiftAsync("shift-two");
			await CreateEmployeeAsync("ScienceTeaching", roleOne, shiftOne, "Science", EmployeeGroupType.Teaching);
			await CreateEmployeeAsync("MathNonTeaching", roleTwo, shiftTwo, "Math", EmployeeGroupType.NonTeaching);

			var baseRequest = BuildPreviewRequest(BuildValidRequest());
			(await _appService.PreviewAsync(Guid.NewGuid(), baseRequest)).AffectedEmployeeCount.ShouldBe(2);

			baseRequest.DepartmentId = "Science";
			(await _appService.PreviewAsync(Guid.NewGuid(), baseRequest)).AffectedEmployeeCount.ShouldBe(1);

			baseRequest.DepartmentId = null;
			baseRequest.RoleId = roleTwo.ToString();
			(await _appService.PreviewAsync(Guid.NewGuid(), baseRequest)).AffectedEmployeeCount.ShouldBe(1);

			baseRequest.RoleId = null;
			baseRequest.EmployeeType = "non_teaching";
			(await _appService.PreviewAsync(Guid.NewGuid(), baseRequest)).AffectedEmployeeCount.ShouldBe(1);

			baseRequest.EmployeeType = null;
			baseRequest.ShiftId = shiftOne.ToString();
			(await _appService.PreviewAsync(Guid.NewGuid(), baseRequest)).AffectedEmployeeCount.ShouldBe(1);
		}
	}

	[Fact]
	public async Task Evaluation_Should_Create_Approve_And_Publish_Monthly_Run()
	{
		using (_currentTenant.Change(Guid.NewGuid()))
		{
			var academicYearId = Guid.NewGuid();
			var request = BuildValidRequest();
			request.PayrollIntegrationMode = "approved_results";
			request.PunchRules.MissingPunchBehavior = "mark_absent";
			await _appService.CreateOrUpdateAsync(academicYearId, request);

			var roleId = await CreateRoleAsync("evaluation");
			var shiftId = await CreateShiftAsync("evaluation");
			var employee = await CreateEmployeeAsync("Evaluation", roleId, shiftId, "Science", EmployeeGroupType.Teaching);
			await InsertAttendanceAsync(employee.Id, 2026, 4, 6, 9, 0, 17, 0, EmployeeAttendanceStatusType.Present);

			var preview = await _appService.PreviewEvaluationAsync(new AttendancePolicyEvaluationRequest
			{
				AcademicYearId = academicYearId,
				Month = 4,
				Year = 2026
			});
			preview.AffectedEmployeeCount.ShouldBe(1);
			preview.Results.Count.ShouldBeGreaterThan(1);
			preview.Results.ShouldContain(x => x.SourceAttendanceId == null);
			(await CountAsync(_evaluationRunRepository)).ShouldBe(0);

			var created = await _appService.CreateEvaluationAsync(new AttendancePolicyEvaluationRequest
			{
				AcademicYearId = academicYearId,
				Month = 4,
				Year = 2026
			});
			created.Status.ShouldBe("evaluated");
			created.CreatedAt.ShouldNotBe(default);
			(await CountAsync(_evaluationRunRepository)).ShouldBe(1);
			(await CountAsync(_evaluationResultRepository)).ShouldBeGreaterThan(1);

			var list = await _appService.GetEvaluationsAsync(academicYearId, 4, 2026);
			list.Single(x => x.Id == created.Id).CreatedAt.ShouldNotBe(default);
			list.Single(x => x.Id == created.Id).Results.ShouldBeEmpty();

			var detail = await _appService.GetEvaluationAsync(created.Id);
			detail.TotalResultCount.ShouldBe(created.Results.Count);
			detail.Results.ShouldBeEmpty();

			var firstPage = await _appService.GetEvaluationResultsAsync(created.Id, new AttendancePolicyEvaluationResultsRequest
			{
				SkipCount = 0,
				MaxResultCount = 2
			});
			firstPage.TotalCount.ShouldBe(created.Results.Count);
			firstPage.Items.Count.ShouldBe(2);
			firstPage.Items.ShouldContain(x => x.EmployeeCode == employee.EmployeeCode);
			firstPage.Items.ShouldContain(x => !string.IsNullOrWhiteSpace(x.EmployeeName));

			var secondPage = await _appService.GetEvaluationResultsAsync(created.Id, new AttendancePolicyEvaluationResultsRequest
			{
				SkipCount = 2,
				MaxResultCount = 2
			});
			secondPage.Items.Select(x => x.Id).ShouldNotContain(firstPage.Items.Select(x => x.Id).First());

			var warningRows = await _appService.GetEvaluationResultsAsync(created.Id, new AttendancePolicyEvaluationResultsRequest
			{
				HasWarnings = true,
				MaxResultCount = 100
			});
			warningRows.Items.ShouldContain(x => x.SourceAttendanceId == null);

			var presentRows = await _appService.GetEvaluationResultsAsync(created.Id, new AttendancePolicyEvaluationResultsRequest
			{
				EvaluatedStatus = nameof(EmployeeAttendanceStatusType.Present),
				MaxResultCount = 100
			});
			presentRows.Items.ShouldAllBe(x => x.EvaluatedStatus == nameof(EmployeeAttendanceStatusType.Present));

			var teachingRows = await _appService.GetEvaluationResultsAsync(created.Id, new AttendancePolicyEvaluationResultsRequest
			{
				EmployeeType = "teaching",
				MaxResultCount = 100
			});
			teachingRows.TotalCount.ShouldBe(created.Results.Count);

			var invalidEmployeeTypeRows = await _appService.GetEvaluationResultsAsync(created.Id, new AttendancePolicyEvaluationResultsRequest
			{
				EmployeeType = "contractor",
				MaxResultCount = 100
			});
			invalidEmployeeTypeRows.TotalCount.ShouldBe(0);
			invalidEmployeeTypeRows.Items.ShouldBeEmpty();

			var approved = await _appService.ApproveEvaluationAsync(created.Id);
			approved.Status.ShouldBe("approved");
			approved.TotalResultCount.ShouldBe(created.Results.Count);
			approved.Results.ShouldBeEmpty();

			var published = await _appService.PublishEvaluationAsync(created.Id);
			published.Status.ShouldBe("published");
			published.PublishedAt.ShouldNotBeNull();
			published.TotalResultCount.ShouldBe(created.Results.Count);
			published.Results.ShouldBeEmpty();

			var cancellable = await _appService.CreateEvaluationAsync(new AttendancePolicyEvaluationRequest
			{
				AcademicYearId = academicYearId,
				Month = 5,
				Year = 2026
			});
			var cancelled = await _appService.CancelEvaluationAsync(cancellable.Id);
			cancelled.Status.ShouldBe("cancelled");
			cancelled.TotalResultCount.ShouldBe(cancellable.Results.Count);
			cancelled.Results.ShouldBeEmpty();
		}
	}

	[Fact]
	public async Task CreateEvaluation_Should_Reject_Scoped_Filters()
	{
		using (_currentTenant.Change(Guid.NewGuid()))
		{
			var academicYearId = Guid.NewGuid();
			await _appService.CreateOrUpdateAsync(academicYearId, BuildValidRequest());

			await Should.ThrowAsync<BusinessException>(() => _appService.CreateEvaluationAsync(new AttendancePolicyEvaluationRequest
			{
				AcademicYearId = academicYearId,
				Month = 4,
				Year = 2026,
				DepartmentId = "Science"
			}));
		}
	}

	[Fact]
	public async Task NeedsReview_Evaluation_Should_Block_Approval_And_Allow_Cancel()
	{
		using (_currentTenant.Change(Guid.NewGuid()))
		{
			var academicYearId = Guid.NewGuid();
			await _appService.CreateOrUpdateAsync(academicYearId, BuildValidRequest());
			var roleId = await CreateRoleAsync("needs-review");
			var shiftId = await CreateShiftAsync("needs-review");
			await CreateEmployeeAsync("NeedsReview", roleId, shiftId, "Science", EmployeeGroupType.Teaching);

			var created = await _appService.CreateEvaluationAsync(new AttendancePolicyEvaluationRequest
			{
				AcademicYearId = academicYearId,
				Month = 4,
				Year = 2026
			});

			created.Status.ShouldBe("needs_review");
			await Should.ThrowAsync<BusinessException>(() => _appService.ApproveEvaluationAsync(created.Id));

			var cancelled = await _appService.CancelEvaluationAsync(created.Id);
			cancelled.Status.ShouldBe("cancelled");
		}
	}

	[Fact]
	public async Task Evaluation_Should_Apply_Active_Shift_Override()
	{
		using (_currentTenant.Change(Guid.NewGuid()))
		{
			var academicYearId = Guid.NewGuid();
			var roleId = await CreateRoleAsync("override");
			var shiftId = await CreateShiftAsync("override");
			var employee = await CreateEmployeeAsync("Override", roleId, shiftId, "Science", EmployeeGroupType.Teaching);
			var request = BuildValidRequest();
			request.PunchRules.MissingPunchBehavior = "mark_absent";
			request.PunchRules.FullDayThresholdPercentage = 90;
			request.DailyRules.HoursBasedPayPercentage = 90;
			request.Overrides.Add(new AttendancePolicyOverrideRequest
			{
				TargetType = "shift",
				TargetId = shiftId.ToString(),
				TargetName = "override",
				IsActive = true,
				HoursBasedPayPercentage = 75
			});
			await _appService.CreateOrUpdateAsync(academicYearId, request);
			await InsertAttendanceAsync(employee.Id, 2026, 4, 6, 9, 0, 15, 30, EmployeeAttendanceStatusType.Present);

			var preview = await _appService.PreviewEvaluationAsync(new AttendancePolicyEvaluationRequest
			{
				AcademicYearId = academicYearId,
				Month = 4,
				Year = 2026
			});

			var attendedRow = preview.Results.Single(x => x.SourceAttendanceId.HasValue);
			attendedRow.EvaluatedPayableUnits.ShouldBe(1);
		}
	}

	[Fact]
	public async Task Publish_Should_Supersede_Previous_Run_And_Delete_Unlocked_Payroll_Snapshots()
	{
		using (_currentTenant.Change(Guid.NewGuid()))
		{
			var academicYearId = Guid.NewGuid();
			var request = BuildValidRequest();
			request.PunchRules.MissingPunchBehavior = "mark_absent";
			await _appService.CreateOrUpdateAsync(academicYearId, request);
			var roleId = await CreateRoleAsync("publish");
			var shiftId = await CreateShiftAsync("publish");
			var employee = await CreateEmployeeAsync("Publish", roleId, shiftId, "Science", EmployeeGroupType.Teaching);
			await InsertAttendanceAsync(employee.Id, 2026, 4, 6, 9, 0, 17, 0, EmployeeAttendanceStatusType.Present);

			var snapshot = new PayrollRunItemSnapshot(_guidGenerator.Create(), _currentTenant.Id, "2026-04", employee.Id)
			{
				EmployeeName = "Publish Config",
				EmployeeIdentifier = "EMP",
				DayCalculationMode = "working-days"
			};
			await _payrollSnapshotRepository.InsertAsync(snapshot, autoSave: true);

			var first = await CreateApprovedEvaluationAsync(academicYearId, 4, 2026);
			await _appService.PublishEvaluationAsync(first.Id);
			(await CountAsync(_payrollSnapshotRepository)).ShouldBe(0);

			var second = await CreateApprovedEvaluationAsync(academicYearId, 4, 2026);
			var publishedSecond = await _appService.PublishEvaluationAsync(second.Id);

			var firstRun = await _evaluationRunRepository.GetAsync(first.Id);
			firstRun.Status.ShouldBe(AttendancePolicyEvaluationRunStatus.Superseded);
			publishedSecond.Status.ShouldBe("published");
		}
	}

	private async Task<EmployeePersonalInfoDto> CreateEmployeeAsync(
		string suffix,
		Guid roleId,
		Guid shiftId,
		string department,
		EmployeeGroupType employeeGroup)
	{
		return await _employeePersonalInfoAppService.CreateAsync(new CreateEmployeePersonalInfoDto
		{
			FirstName = $"Emp{suffix}{Guid.NewGuid():N}"[..18],
			LastName = "Config",
			MobileNo = BuildPhoneNumber(),
			Email = $"{suffix}.{Guid.NewGuid():N}@test.com",
			DateOfBirth = new DateOnly(1990, 5, 15),
			GenderType = GenderType.Male,
			DateOfJoining = new DateOnly(2024, 1, 1),
			EmployeeStatusType = EmployeeStatusType.Active,
			RoleId = roleId,
			ShiftId = shiftId,
			Department = department,
			EmployeeGroup = employeeGroup
		});
	}

	private async Task<Guid> CreateRoleAsync(string suffix)
	{
		var id = _guidGenerator.Create();
		await _roleRepository.InsertAsync(new IdentityRole(id, $"AttendanceConfig-{suffix}-{id:N}", _currentTenant.Id), autoSave: true);
		return id;
	}

	private async Task<Guid> CreateShiftAsync(string suffix)
	{
		var shift = new ShiftManagement
		{
			ShiftName = $"Attendance Config Test Shift {suffix} {Guid.NewGuid():N}",
			StartTime = new TimeOnly(9, 0),
			EndTime = new TimeOnly(17, 0)
		};

		await _shiftRepository.InsertAsync(shift, autoSave: true);
		return shift.Id;
	}

	private async Task InsertAttendanceAsync(
		Guid employeeId,
		int year,
		int month,
		int day,
		int? inHour,
		int? inMinute,
		int? outHour,
		int? outMinute,
		EmployeeAttendanceStatusType status)
	{
		var date = new DateTime(year, month, day);
		var offset = TimeZoneInfo.Local.GetUtcOffset(date);
		await _employeeAttendanceRepository.InsertAsync(new EmployeeAttendance
		{
			TenantId = _currentTenant.Id,
			EmployeeId = employeeId,
			AttendanceDate = new DateOnly(year, month, day),
			TimeIn = inHour.HasValue ? new DateTimeOffset(year, month, day, inHour.Value, inMinute!.Value, 0, offset) : null,
			TimeOut = outHour.HasValue ? new DateTimeOffset(year, month, day, outHour.Value, outMinute!.Value, 0, offset) : null,
			StatusType = status
		}, autoSave: true);
	}

	private static AttendancePolicyOverrideRequest BuildOverride(string targetType, string targetId, bool isActive) =>
		new()
		{
			TargetType = targetType,
			TargetId = targetId,
			TargetName = targetId,
			IsActive = isActive
		};

	private static AttendanceConfigurationPreviewRequest BuildPreviewRequest(AttendanceConfigurationRequest configuration) =>
		new()
		{
			FromDate = new DateTime(2026, 4, 1),
			ToDate = new DateTime(2026, 4, 30),
			Configuration = configuration
		};

	private async Task<AttendancePolicyEvaluationRunResponse> CreateApprovedEvaluationAsync(Guid academicYearId, int month, int year)
	{
		var created = await _appService.CreateEvaluationAsync(new AttendancePolicyEvaluationRequest
		{
			AcademicYearId = academicYearId,
			Month = month,
			Year = year
		});

		return await _appService.ApproveEvaluationAsync(created.Id);
	}

	private static AttendanceConfigurationRequest BuildValidRequest() =>
		new()
		{
			PolicyName = "Default Attendance Policy",
			Status = "draft",
			WorkdayRules = new AttendanceWorkdayRulesRequest
			{
				WeeklyOffPattern = "saturday_sunday"
			},
			PunchRules = new AttendancePunchRulesRequest
			{
				FullDayThresholdPercentage = 80,
				HalfDayThresholdPercentage = 50,
				AbsentThresholdPercentage = 0,
				MissingPunchBehavior = "requires_review"
			},
			DailyRules = new AttendanceDailyRulesRequest
			{
				HoursBasedPayPercentage = 80
			},
			LateEarlyRules = new AttendanceLateEarlyRulesRequest
			{
				GraceTime = 15,
				MaxLateAllowedPerMonth = 3,
				PenaltyType = "deduct_half_day",
				PenaltyCountThreshold = 0,
				EarlyExitEnabled = false,
				EarlyExitGraceTime = 0
			},
			Overtime = new AttendanceOvertimeRulesRequest
			{
				Enabled = true,
				Rate = "1.5x",
				MinOvertimeMinutes = 30
			},
			HolidayRules = new AttendanceHolidayRulesRequest
			{
				WorkOnHolidayBehavior = "comp_off"
			},
			ManualCorrectionRules = new AttendanceManualCorrectionRulesRequest
			{
				Allowed = true
			},
			PayrollIntegrationMode = "advisory",
			RequirePublishedAttendanceForPayrollLock = false
		};

	private static string BuildPhoneNumber() => $"91{Random.Shared.Next(10000000, 99999999)}";

	private Task<long> CountAsync<TEntity>(IReadOnlyRepository<TEntity, Guid> repository)
		where TEntity : class, Volo.Abp.Domain.Entities.IEntity<Guid> =>
		WithUnitOfWorkAsync(async () => await repository.LongCountAsync());
}
