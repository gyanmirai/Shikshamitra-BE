using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationMediums;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;
using Xunit;

namespace ShikshaMitra.Students;

public class StudentScopeAccessService_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	private static readonly Guid TestTenantId = Guid.Parse("22222222-2222-2222-2222-222222222222");

	private readonly StudentScopeAccessService _studentScopeAccessService;
	private readonly IRepository<UserScopeAssignment, Guid> _scopeRepository;
	private readonly IRepository<AcademicYear, Guid> _academicYearRepository;
	private readonly IRepository<EducationBoard, Guid> _boardRepository;
	private readonly IRepository<EducationMedium, Guid> _mediumRepository;
	private readonly IRepository<EducationMediumStage, Guid> _stageRepository;
	private readonly IRepository<Standard, Guid> _standardRepository;
	private readonly IRepository<Division, Guid> _divisionRepository;
	private readonly IRepository<AdmissionInquiryLead, Guid> _leadRepository;
	private readonly AcademicYearManager _academicYearManager;
	private readonly EducationBoardManager _boardManager;
	private readonly EducationMediumManager _mediumManager;
	private readonly EducationMediumStageManager _stageManager;
	private readonly StandardManager _standardManager;
	private readonly DivisionManager _divisionManager;
	private readonly IUserScopeService _userScopeService;
	private readonly ICurrentPrincipalAccessor _currentPrincipalAccessor;
	private readonly ICurrentTenant _currentTenant;

	public StudentScopeAccessService_Tests()
	{
		_studentScopeAccessService = GetRequiredService<StudentScopeAccessService>();
		_scopeRepository = GetRequiredService<IRepository<UserScopeAssignment, Guid>>();
		_academicYearRepository = GetRequiredService<IRepository<AcademicYear, Guid>>();
		_boardRepository = GetRequiredService<IRepository<EducationBoard, Guid>>();
		_mediumRepository = GetRequiredService<IRepository<EducationMedium, Guid>>();
		_stageRepository = GetRequiredService<IRepository<EducationMediumStage, Guid>>();
		_standardRepository = GetRequiredService<IRepository<Standard, Guid>>();
		_divisionRepository = GetRequiredService<IRepository<Division, Guid>>();
		_leadRepository = GetRequiredService<IRepository<AdmissionInquiryLead, Guid>>();
		_academicYearManager = GetRequiredService<AcademicYearManager>();
		_boardManager = GetRequiredService<EducationBoardManager>();
		_mediumManager = GetRequiredService<EducationMediumManager>();
		_stageManager = GetRequiredService<EducationMediumStageManager>();
		_standardManager = GetRequiredService<StandardManager>();
		_divisionManager = GetRequiredService<DivisionManager>();
		_userScopeService = GetRequiredService<IUserScopeService>();
		_currentPrincipalAccessor = GetRequiredService<ICurrentPrincipalAccessor>();
		_currentTenant = GetRequiredService<ICurrentTenant>();
	}

	[Fact]
	public async Task EnsureCanAccessAcademicYearAsync_Should_Allow_Assigned_Academic_Year()
	{
		var userId = Guid.NewGuid();
		var academicYearId = Guid.NewGuid();
		await SeedAcademicYearScopeAsync(userId, academicYearId);

		using (_currentPrincipalAccessor.Change(BuildPrincipal(userId)))
		{
			await _studentScopeAccessService.EnsureCanAccessAcademicYearAsync(
				academicYearId,
				nameof(EnsureCanAccessAcademicYearAsync_Should_Allow_Assigned_Academic_Year));
		}
	}

	[Fact]
	public async Task EnsureCanAccessAcademicYearAsync_Should_Deny_Unassigned_Academic_Year()
	{
		var userId = Guid.NewGuid();
		await SeedAcademicYearScopeAsync(userId, Guid.NewGuid());

		using (_currentPrincipalAccessor.Change(BuildPrincipal(userId)))
		{
			var exception = await Should.ThrowAsync<BusinessException>(async () =>
				await _studentScopeAccessService.EnsureCanAccessAcademicYearAsync(
					Guid.NewGuid(),
					nameof(EnsureCanAccessAcademicYearAsync_Should_Deny_Unassigned_Academic_Year)));

			exception.Code.ShouldBe(UserScopeErrorCodes.AccessDenied);
		}
	}

	[Fact]
	public async Task EnsureCanAccessAdmissionLeadAsync_Should_Allow_Assigned_Standard_And_Deny_Sibling_And_Unclassified_Leads()
	{
		using (_currentTenant.Change(TestTenantId))
		{
			var hierarchy = await CreateAdmissionLeadHierarchyAsync();
			var userId = Guid.NewGuid();
			await SeedStandardScopeAsync(userId, hierarchy);
			var assignedLead = await SeedAdmissionLeadAsync(
				hierarchy.AcademicYearId,
				hierarchy.AssignedStandardId,
				"Assigned");
			var siblingLead = await SeedAdmissionLeadAsync(
				hierarchy.AcademicYearId,
				hierarchy.SiblingStandardId,
				"Sibling");
			var unclassifiedLead = await SeedAdmissionLeadAsync(
				hierarchy.AcademicYearId,
				targetStandardId: null,
				"Unclassified");

			using (_currentPrincipalAccessor.Change(BuildPrincipal(userId)))
			{
				await WithUnitOfWorkAsync(() =>
					_studentScopeAccessService.EnsureCanAccessAdmissionLeadAsync(
						assignedLead,
						nameof(EnsureCanAccessAdmissionLeadAsync_Should_Allow_Assigned_Standard_And_Deny_Sibling_And_Unclassified_Leads)));

				var siblingException = await Should.ThrowAsync<BusinessException>(() =>
					WithUnitOfWorkAsync(() =>
						_studentScopeAccessService.EnsureCanAccessAdmissionLeadAsync(
							siblingLead,
							nameof(EnsureCanAccessAdmissionLeadAsync_Should_Allow_Assigned_Standard_And_Deny_Sibling_And_Unclassified_Leads))));
				siblingException.Code.ShouldBe(UserScopeErrorCodes.AccessDenied);

				var unclassifiedException = await Should.ThrowAsync<BusinessException>(() =>
					WithUnitOfWorkAsync(() =>
						_studentScopeAccessService.EnsureCanAccessAdmissionLeadAsync(
							unclassifiedLead,
							nameof(EnsureCanAccessAdmissionLeadAsync_Should_Allow_Assigned_Standard_And_Deny_Sibling_And_Unclassified_Leads))));
				unclassifiedException.Code.ShouldBe(UserScopeErrorCodes.AccessDenied);
			}
		}
	}

	[Fact]
	public async Task ApplyCurrentUserAdmissionLeadScopeAsync_Should_Return_Only_Leads_In_Assigned_Standard()
	{
		using (_currentTenant.Change(TestTenantId))
		{
			var hierarchy = await CreateAdmissionLeadHierarchyAsync();
			var userId = Guid.NewGuid();
			await SeedStandardScopeAsync(userId, hierarchy);
			var assignedLead = await SeedAdmissionLeadAsync(
				hierarchy.AcademicYearId,
				hierarchy.AssignedStandardId,
				"Visible");
			await SeedAdmissionLeadAsync(
				hierarchy.AcademicYearId,
				hierarchy.SiblingStandardId,
				"HiddenSibling");
			await SeedAdmissionLeadAsync(
				hierarchy.AcademicYearId,
				targetStandardId: null,
				"HiddenUnclassified");

			using (_currentPrincipalAccessor.Change(BuildPrincipal(userId)))
			{
				var visibleLeadIds = await WithUnitOfWorkAsync(async () =>
				{
					var query = await _leadRepository.GetQueryableAsync();
					var scopedQuery = await _studentScopeAccessService.ApplyCurrentUserAdmissionLeadScopeAsync(
						query.Where(x => x.AcademicYearId == hierarchy.AcademicYearId));
					return await scopedQuery.Select(x => x.Id).ToListAsync();
				});

				visibleLeadIds.ShouldBe([assignedLead.Id]);
			}
		}
	}

	[Fact]
	public async Task ApplyCurrentUserAdmissionLeadScopeAsync_Should_Not_Treat_Exact_Division_As_Standard_Wide()
	{
		using (_currentTenant.Change(TestTenantId))
		{
			var hierarchy = await CreateAdmissionLeadHierarchyAsync();
			var userId = Guid.NewGuid();
			await SeedDivisionScopeAsync(userId, hierarchy);
			await SeedAdmissionLeadAsync(
				hierarchy.AcademicYearId,
				hierarchy.AssignedStandardId,
				"HiddenSameStandard");
			await SeedAdmissionLeadAsync(
				hierarchy.AcademicYearId,
				hierarchy.SiblingStandardId,
				"HiddenSiblingStandard");

			using (_currentPrincipalAccessor.Change(BuildPrincipal(userId)))
			{
				var visibleLeadIds = await WithUnitOfWorkAsync(async () =>
				{
					var query = await _leadRepository.GetQueryableAsync();
					var scopedQuery = await _studentScopeAccessService.ApplyCurrentUserAdmissionLeadScopeAsync(
						query.Where(x => x.AcademicYearId == hierarchy.AcademicYearId));
					return await scopedQuery.Select(x => x.Id).ToListAsync();
				});

				visibleLeadIds.ShouldBeEmpty();
			}
		}
	}

	[Fact]
	public async Task ApplyCurrentUserDivisionScopeAsync_Should_Return_Only_The_Exact_Division()
	{
		using (_currentTenant.Change(TestTenantId))
		{
			var hierarchy = await CreateAdmissionLeadHierarchyAsync();
			var userId = Guid.NewGuid();
			await SeedDivisionScopeAsync(userId, hierarchy);

			using (_currentPrincipalAccessor.Change(BuildPrincipal(userId)))
			{
				var visibleDivisionIds = await WithUnitOfWorkAsync(async () =>
				{
					var query = await _divisionRepository.GetQueryableAsync();
					var scopedQuery = await _studentScopeAccessService.ApplyCurrentUserDivisionScopeAsync(
						query,
						hierarchy.AcademicYearId);
					return await scopedQuery.Select(x => x.Id).ToListAsync();
				});

				visibleDivisionIds.ShouldBe([hierarchy.AssignedDivisionId]);
			}
		}
	}

	private async Task<AdmissionLeadHierarchy> CreateAdmissionLeadHierarchyAsync()
	{
		return await WithUnitOfWorkAsync(async () =>
		{
			var suffix = Guid.NewGuid().ToString("N")[..8];
			var academicYear = await _academicYearRepository.InsertAsync(
				await _academicYearManager.CreateAsync(
					$"Admission Lead Scope {suffix}",
					new DateTime(2060, 6, 1),
					new DateTime(2061, 5, 31)),
				autoSave: true);
			var board = await _boardRepository.InsertAsync(
				await _boardManager.CreateAsync(academicYear.Id, EducationBoardType.CBSE),
				autoSave: true);
			var medium = await _mediumRepository.InsertAsync(
				await _mediumManager.CreateAsync(board.Id, $"English {suffix}"),
				autoSave: true);
			var stage = await _stageRepository.InsertAsync(
				await _stageManager.CreateAsync(medium.Id, $"Primary {suffix}"),
				autoSave: true);
			var assignedStandard = await _standardRepository.InsertAsync(
				await _standardManager.CreateAsync(stage.Id, $"Grade 4 {suffix}", 9, null),
				autoSave: true);
			var siblingStandard = await _standardRepository.InsertAsync(
				await _standardManager.CreateAsync(stage.Id, $"Grade 5 {suffix}", 10, null),
				autoSave: true);
			var assignedDivision = await _divisionRepository.InsertAsync(
				await _divisionManager.CreateAsync(assignedStandard.Id, $"A {suffix}", 40),
				autoSave: true);
			var siblingDivision = await _divisionRepository.InsertAsync(
				await _divisionManager.CreateAsync(assignedStandard.Id, $"B {suffix}", 40),
				autoSave: true);

			return new AdmissionLeadHierarchy(
				academicYear.Id,
				board.Id,
				medium.Id,
				stage.Id,
				assignedStandard.Id,
				siblingStandard.Id,
				assignedDivision.Id,
				siblingDivision.Id);
		});
	}

	private async Task SeedStandardScopeAsync(Guid userId, AdmissionLeadHierarchy hierarchy)
	{
		await WithUnitOfWorkAsync(async () =>
		{
			await _scopeRepository.InsertAsync(
				new UserScopeAssignment(
					Guid.NewGuid(),
					_currentTenant.Id,
					userId: userId,
					academicYearId: hierarchy.AcademicYearId,
					educationBoardId: hierarchy.BoardId,
					educationMediumId: hierarchy.MediumId,
					educationMediumStageId: hierarchy.StageId,
					standardId: hierarchy.AssignedStandardId,
					description: "Admission lead standard-scope regression test"),
				autoSave: true);

			await _userScopeService.InvalidateUserScopeCacheAsync(userId);
		});
	}

	private async Task SeedDivisionScopeAsync(Guid userId, AdmissionLeadHierarchy hierarchy)
	{
		await WithUnitOfWorkAsync(async () =>
		{
			await _scopeRepository.InsertAsync(
				new UserScopeAssignment(
					Guid.NewGuid(),
					_currentTenant.Id,
					userId: userId,
					academicYearId: hierarchy.AcademicYearId,
					educationBoardId: hierarchy.BoardId,
					educationMediumId: hierarchy.MediumId,
					educationMediumStageId: hierarchy.StageId,
					standardId: hierarchy.AssignedStandardId,
					divisionId: hierarchy.AssignedDivisionId,
					description: "Admission exact-division scope regression test"),
				autoSave: true);

			await _userScopeService.InvalidateUserScopeCacheAsync(userId);
		});
	}

	private async Task<AdmissionInquiryLead> SeedAdmissionLeadAsync(
		Guid academicYearId,
		Guid? targetStandardId,
		string namePrefix)
	{
		return await WithUnitOfWorkAsync(async () =>
		{
			var suffix = Guid.NewGuid().ToString("N");
			var lead = new AdmissionInquiryLead(
				Guid.NewGuid(),
				_currentTenant.Id,
				academicYearId,
				inquirySourceId: null,
				inquirySourceSnapshot: null,
				AdmissionInquiryChannel.QuickCallEntry,
				campaignName: null,
				campusBranchName: null,
				preferredCommunicationLanguage: null,
				firstName: $"{namePrefix}{suffix[..6]}",
				middleName: null,
				lastName: "Applicant",
				dateOfBirth: null,
				parentGuardianName: "Scope Test Parent",
				primaryContactNo: $"9{Random.Shared.NextInt64(100000000, 999999999)}",
				email: $"{namePrefix.ToLowerInvariant()}.{suffix}@test.local",
				admissionIntent: "Admission scope regression test",
				targetStandardId,
				desiredClassName: namePrefix,
				notes: null,
				DateTimeOffset.UtcNow);

			return await _leadRepository.InsertAsync(lead, autoSave: true);
		});
	}

	private async Task SeedAcademicYearScopeAsync(Guid userId, Guid academicYearId)
	{
		await WithUnitOfWorkAsync(async () =>
		{
			await _scopeRepository.InsertAsync(
				new UserScopeAssignment(
					Guid.NewGuid(),
					_currentTenant.Id,
					userId: userId,
					academicYearId: academicYearId,
					description: "Student scope access regression test"),
				autoSave: true);

			await _userScopeService.InvalidateUserScopeCacheAsync(userId);
		});
	}

	private ClaimsPrincipal BuildPrincipal(Guid userId)
	{
		var claims = new List<Claim>
		{
			new(AbpClaimTypes.UserId, userId.ToString()),
			new(AbpClaimTypes.UserName, $"student-scope-{userId:N}"),
			new(AbpClaimTypes.Email, $"student-scope-{userId:N}@test.local")
		};

		if (_currentTenant.Id.HasValue)
		{
			claims.Add(new Claim(AbpClaimTypes.TenantId, _currentTenant.Id.Value.ToString()));
		}

		return new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
	}

	private sealed record AdmissionLeadHierarchy(
		Guid AcademicYearId,
		Guid BoardId,
		Guid MediumId,
		Guid StageId,
		Guid AssignedStandardId,
		Guid SiblingStandardId,
		Guid AssignedDivisionId,
		Guid SiblingDivisionId);
}
