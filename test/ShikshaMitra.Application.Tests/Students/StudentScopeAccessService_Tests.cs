using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Students.StudentEnrollments.Settings;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;
using Xunit;

namespace ShikshaMitra.Students;

public class StudentScopeAccessService_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	private readonly StudentScopeAccessService _studentScopeAccessService;
	private readonly IRepository<UserScopeAssignment, Guid> _scopeRepository;
	private readonly IRepository<InquiryPreference, Guid> _inquiryPreferenceRepository;
	private readonly InquiryPreferenceManager _inquiryPreferenceManager;
	private readonly IUserScopeService _userScopeService;
	private readonly ICurrentPrincipalAccessor _currentPrincipalAccessor;
	private readonly ICurrentTenant _currentTenant;

	public StudentScopeAccessService_Tests()
	{
		_studentScopeAccessService = GetRequiredService<StudentScopeAccessService>();
		_scopeRepository = GetRequiredService<IRepository<UserScopeAssignment, Guid>>();
		_inquiryPreferenceRepository = GetRequiredService<IRepository<InquiryPreference, Guid>>();
		_inquiryPreferenceManager = GetRequiredService<InquiryPreferenceManager>();
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
	public async Task EnsureCanUseInquiryPreferenceAsync_Should_Deny_Preference_From_Different_Academic_Year()
	{
		var userId = Guid.NewGuid();
		var inScopeAcademicYearId = Guid.NewGuid();
		var preferenceAcademicYearId = Guid.NewGuid();
		await SeedAcademicYearScopeAsync(userId, inScopeAcademicYearId);
		var preference = await SeedInquiryPreferenceAsync(preferenceAcademicYearId);

		using (_currentPrincipalAccessor.Change(BuildPrincipal(userId)))
		{
			var exception = await Should.ThrowAsync<BusinessException>(async () =>
				await _studentScopeAccessService.EnsureCanUseInquiryPreferenceAsync(
					preference.Id,
					inScopeAcademicYearId,
					nameof(EnsureCanUseInquiryPreferenceAsync_Should_Deny_Preference_From_Different_Academic_Year)));

			exception.Code.ShouldBe(UserScopeErrorCodes.AccessDenied);
		}
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

	private async Task<InquiryPreference> SeedInquiryPreferenceAsync(Guid academicYearId)
	{
		return await WithUnitOfWorkAsync(async () =>
		{
			var preference = await _inquiryPreferenceManager.CreateAsync(
				_currentTenant.Id,
				academicYearId,
				$"Scope Test {Guid.NewGuid():N}");

			return await _inquiryPreferenceRepository.InsertAsync(preference, autoSave: true);
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
}
