using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using ShikshaMitra.Security.UserScope;
using Volo.Abp;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.Records;

[RemoteService(false)]
public class StudentDashboardScopeService(IUserScopeService userScopeService) : ApplicationService
{
	public async Task<UserScopeContext> EnsureCanReadStudentDashboardAsync()
	{
		var userId = CurrentUser.Id;
		if (!userId.HasValue)
		{
			throw new BusinessException(UserScopeErrorCodes.AccessDenied);
		}

		var scope = await userScopeService.GetUserScopeAsync(userId.Value);
		if (!scope.HasAnyAccess)
		{
			throw new BusinessException(UserScopeErrorCodes.AccessDenied);
		}

		return scope;
	}

	public Task<UserScopeContext> ResolveRefreshScopeAsync(StudentDashboardPreviewRefreshJobArgs args) =>
		args.RequestedByUserId.HasValue
			? userScopeService.GetUserScopeAsync(args.RequestedByUserId.Value)
			: Task.FromResult(CreateStudentWideScope());

	public UserScopeContext CreateStudentWideScope() =>
		new()
		{
			UserId = Guid.Empty,
			TenantId = CurrentTenant.Id,
			IsAdmin = true,
			CalculatedAt = Clock.Now
		};

	public static string BuildScopeKey(UserScopeContext scope)
	{
		if (scope.HasTenantWideScope())
		{
			return StudentDashboardScopeKeys.StudentWide;
		}

		var raw = string.Join("|",
			$"ay:{JoinIds(scope.AcademicYearIds)}",
			$"b:{JoinIds(scope.BoardIds)}",
			$"m:{JoinIds(scope.MediumIds)}",
			$"ms:{JoinIds(scope.MediumStageIds)}",
			$"s:{JoinIds(scope.StandardIds)}",
			$"d:{JoinIds(scope.DivisionIds)}",
			$"way:{JoinIds(scope.WildcardBoardAcademicYearIds)}",
			$"wb:{JoinIds(scope.WildcardMediumBoardIds)}",
			$"wm:{JoinIds(scope.WildcardMediumStageMediumIds)}",
			$"wms:{JoinIds(scope.WildcardStandardMediumStageIds)}",
			$"ws:{JoinIds(scope.WildcardDivisionStandardIds)}");
		var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
		return $"student-scoped-{Convert.ToHexString(hash)[..32].ToLowerInvariant()}";
	}

	private static string JoinIds(IEnumerable<Guid> ids) =>
		string.Join(",", ids.OrderBy(x => x).Select(x => x.ToString("N")));
}
