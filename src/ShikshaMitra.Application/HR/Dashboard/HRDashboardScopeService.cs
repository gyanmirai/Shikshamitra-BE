using ShikshaMitra.Common;
using ShikshaMitra.Security.UserScope;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.HR.Dashboard;

[RemoteService(false)]
public class HRDashboardScopeService(IUserScopeService userScopeService)
	: ApplicationService
{
	public async Task<UserScopeContext> EnsureCanReadScopedEmployeesAsync()
	{
		var uid = CurrentUser.Id;
		if (!uid.HasValue)
		{
			throw new BusinessException(HRErrorCodes.AttendanceImportPreviewInvalid);
		}

		var scope = await userScopeService.GetUserScopeAsync(uid.Value);
		if (HrUserScopeAccess.HasHrWideAccess(scope))
		{
			return scope;
		}

		if (!HrUserScopeAccess.HasAnyConcreteHrStageAccess(scope))
		{
			throw new BusinessException(HRErrorCodes.TakeAttendanceMediumStageOutOfScope);
		}

		return scope;
	}

	public Task<UserScopeContext> ResolveRefreshScopeAsync(HrDashboardPreviewRefreshJobArgs args) =>
		args.RequestedByUserId.HasValue
			? userScopeService.GetUserScopeAsync(args.RequestedByUserId.Value)
			: Task.FromResult(CreateHrWideScope());

	public UserScopeContext CreateHrWideScope() =>
		new()
		{
			UserId = Guid.Empty,
			TenantId = CurrentTenant.Id,
			IsAdmin = true,
			CalculatedAt = Clock.Now
		};

	public static string BuildScopeKey(UserScopeContext scope)
	{
		if (HrUserScopeAccess.HasHrWideAccess(scope))
		{
			return HrDashboardScopeKeys.HrWide;
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
		return $"scoped-{Convert.ToHexString(hash)[..32].ToLowerInvariant()}";
	}

	private static string JoinIds(IEnumerable<Guid> ids) =>
		string.Join(",", ids.OrderBy(x => x).Select(x => x.ToString("N")));
}
