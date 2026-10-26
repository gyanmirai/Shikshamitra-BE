using System;
using System.Linq;
using System.Threading.Tasks;
using System.Security.Claims;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.Security.Claims;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.HR.Policies;

public partial class HrPolicyAppService_Tests
{
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task ResolutionPreview_Samples_All_Matching_Groups_And_Preserves_Staff_Precedence(bool staffOverride)
	{
		var context = await CreatePolicyContextAsync();
		var roleId = await CreateUniqueRoleAsync();
		var employee = await CreateEmployeeAsync(roleId);
		var firstGroup = await CreateRoleGroupAsync(context.AcademicYearId, roleId, "Current group");
		var proposedGroup = await CreateRoleGroupAsync(context.AcademicYearId, roleId, "Proposed group");
		var original = BuildAssignment(context, "group", firstGroup.Id.ToString());
		original.Priority = 100;
		await CreateAssignmentWithImpactTokenAsync(original);
		if (staffOverride)
		{
			var direct = BuildAssignment(context, "employee", employee.Id.ToString());
			direct.Priority = 1;
			await CreateAssignmentWithImpactTokenAsync(direct);
		}
		var draft = BuildAssignment(context, "group", proposedGroup.Id.ToString());
		draft.Priority = 200;
		using var authorized = GetRequiredService<ICurrentPrincipalAccessor>().Change(await BuildResolutionPreviewAdminAsync());
		var preview = await _assignments.PreviewResolutionAsync(new()
		{
			Assignment = draft,
			SampleEmployeeId = employee.Id,
			Date = new DateTime(2026, 5, 1)
		});
		preview.IsComplete.ShouldBeTrue();
		preview.Scope.ShouldBe("selected_staff");
		preview.Current.MatchedTargetKey.ShouldBe(staffOverride ? employee.Id.ToString() : firstGroup.Id.ToString());
		preview.Proposed!.MatchedTargetKey.ShouldBe(staffOverride ? employee.Id.ToString() : proposedGroup.Id.ToString());
		preview.Current.MatchedPriority.ShouldBe(staffOverride ? 1 : 100);
		preview.Proposed.MatchedPriority.ShouldBe(staffOverride ? 1 : 200);
		preview.Proposed.PayrollPolicy.Name.ShouldBe(context.PayrollPolicy.Name);
		preview.Proposed.AttendancePolicy.Name.ShouldBe(context.AttendancePolicy.Name);
		preview.Proposed.LeavePolicy!.Name.ShouldBe(context.LeavePolicy.PolicyName);
	}

	private async Task<ClaimsPrincipal> BuildResolutionPreviewAdminAsync()
	{
		using var unit = _unitOfWorkManager.Begin(requiresNew: true);
		var role = (await _roleRepository.GetListAsync()).FirstOrDefault(x => x.Name == "admin")
			?? await _roleRepository.InsertAsync(new IdentityRole(Guid.NewGuid(), "admin", _currentTenant.Id), autoSave: true);
		var id = Guid.NewGuid();
		var user = new IdentityUser(id, $"synthetic-policy-preview-{id:N}", $"policy-preview-{id:N}@example.invalid", _currentTenant.Id);
		var manager = GetRequiredService<IdentityUserManager>();
		// Synthetic test identity only: no password, token, email or usable login is created.
		(await manager.CreateAsync(user)).Succeeded.ShouldBeTrue();
		(await manager.AddToRoleAsync(user, role.Name)).Succeeded.ShouldBeTrue();
		await unit.CompleteAsync();
		return new ClaimsPrincipal(new ClaimsIdentity([new Claim(AbpClaimTypes.UserId, id.ToString()), new Claim(AbpClaimTypes.UserName, user.UserName)], "Synthetic test"));
	}

	[Fact]
	public async Task ResolutionPreview_Does_Not_Expose_A_Sample_Outside_The_Callers_Staff_Scope()
	{
		var context = await CreatePolicyContextAsync();
		var group = await CreateGroupAsync(context.AcademicYearId, "Restricted sample");
		var employee = await CreateEmployeeAsync();
		await Should.ThrowAsync<Volo.Abp.Authorization.AbpAuthorizationException>(() => _assignments.PreviewResolutionAsync(new()
		{
			Assignment = BuildAssignment(context, "group", group.Id.ToString()),
			SampleEmployeeId = employee.Id,
			Date = new DateTime(2026, 5, 1)
		}));
	}

	[Fact]
	public async Task ResolutionPreview_Uses_Detached_Proposal_Without_Changing_Current_Or_Persisted_Assignment()
	{
		var context = await CreatePolicyContextAsync();
		var group = await CreateGroupAsync(context.AcademicYearId, "Synthetic resolution");
		var original = await CreateAssignmentWithImpactTokenAsync(BuildAssignment(context, "group", group.Id.ToString()));
		var draft = BuildAssignment(context, "group", group.Id.ToString());
		draft.Priority = 200;
		var preview = await _assignments.PreviewResolutionAsync(new()
		{
			AssignmentId = original.Id,
			Assignment = draft,
			Date = new DateTime(2026, 5, 1)
		});
		preview.IsComplete.ShouldBeTrue();
		preview.Scope.ShouldBe("selected_group_only");
		preview.Current.MatchedPriority.ShouldBe(10);
		preview.Proposed!.MatchedPriority.ShouldBe(200);
		(await _assignments.GetAsync(original.Id)).Priority.ShouldBe(10);
		preview.Current.MatchedAssignmentId.ShouldBe(original.Id);
		preview.Proposed.MatchedAssignmentId.ShouldBe(original.Id);
	}

	[Fact]
	public async Task ResolutionPreview_Does_Not_Declare_A_New_Id_Tie_Resolved()
	{
		var context = await CreatePolicyContextAsync();
		var group = await CreateGroupAsync(context.AcademicYearId, "Synthetic tie");
		var draft = BuildAssignment(context, "group", group.Id.ToString());
		await CreateAssignmentWithImpactTokenAsync(draft);
		var preview = await _assignments.PreviewResolutionAsync(new()
		{
			Assignment = draft,
			Date = new DateTime(2026, 5, 1)
		});
		preview.IsComplete.ShouldBeFalse();
		preview.Proposed.ShouldBeNull();
		preview.UnavailableReason.ShouldContain("final ID");
		preview.Current.MatchedPriority.ShouldBe(10);
	}

	[Theory]
	[InlineData("2026-04-30", false)]
	[InlineData("2026-05-01", true)]
	[InlineData("2026-05-31", true)]
	[InlineData("2026-06-01", false)]
	public async Task ResolutionPreview_Applies_The_Existing_Inclusive_Assignment_Date_Range(string date, bool applies)
	{
		var context = await CreatePolicyContextAsync();
		var group = await CreateGroupAsync(context.AcademicYearId, "Synthetic boundaries");
		var draft = BuildAssignment(context, "group", group.Id.ToString(),
			effectiveFrom: new DateTime(2026, 5, 1), effectiveTo: new DateTime(2026, 5, 31));
		var preview = await _assignments.PreviewResolutionAsync(new() { Assignment = draft, Date = DateTime.Parse(date) });
		preview.Current.PolicySource.ShouldBe("default");
		preview.IsComplete.ShouldBeTrue();
		(preview.Proposed!.MatchedAssignmentId == Guid.Empty).ShouldBe(applies);
		(await GetRequiredService<IRepository<HrPolicyAssignment, Guid>>().GetCountAsync()).ShouldBe(0);
	}

	[Fact]
	public async Task ResolutionPreview_Rejects_An_Edit_From_A_Different_Year()
	{
		var context = await CreatePolicyContextAsync();
		var group = await CreateGroupAsync(context.AcademicYearId, "Synthetic original year");
		var original = await CreateAssignmentWithImpactTokenAsync(BuildAssignment(context, "group", group.Id.ToString()));
		var other = await CreatePolicyContextAsync();
		var otherGroup = await CreateGroupAsync(other.AcademicYearId, "Synthetic other year");
		var error = await Should.ThrowAsync<BusinessException>(() => _assignments.PreviewResolutionAsync(new()
		{
			AssignmentId = original.Id,
			Assignment = BuildAssignment(other, "group", otherGroup.Id.ToString()),
			Date = new DateTime(2026, 5, 1)
		}));
		error.Code.ShouldBe(HRErrorCodes.HrPolicyAssignmentAcademicYearMismatch);
	}

	[Fact]
	public async Task ResolutionPreview_Rejects_A_Foreign_Tenant_Assignment_Id()
	{
		var context = await CreatePolicyContextAsync();
		var group = await CreateGroupAsync(context.AcademicYearId, "Synthetic tenant");
		var foreignId = Guid.NewGuid();
		using (_currentTenant.Change(Guid.NewGuid()))
		{
			await GetRequiredService<IRepository<HrPolicyAssignment, Guid>>().InsertAsync(new HrPolicyAssignment(
				foreignId, _currentTenant.Id, context.AcademicYearId, HrPolicyAssignmentTargetType.Group,
				group.Id.ToString(), "Foreign tenant", context.AttendancePolicy.Id, context.PayrollPolicy.Id,
				context.LeavePolicy.Id, new DateTime(2026, 4, 1), null, true), autoSave: true);
		}
		await Should.ThrowAsync<Volo.Abp.Domain.Entities.EntityNotFoundException>(() => _assignments.PreviewResolutionAsync(new()
		{
			AssignmentId = foreignId,
			Assignment = BuildAssignment(context, "group", group.Id.ToString()),
			Date = new DateTime(2026, 5, 1)
		}));
	}
}