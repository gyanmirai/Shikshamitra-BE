using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Payroll.RunPayroll;
using ShikshaMitra.Security.UserScope;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;
using Permissions = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsPfRule;

namespace ShikshaMitra.HR.Payroll.Statutory;

[RemoteService(false), Authorize]
public class PfCoverageAppService(IRepository<PfCoverageConfiguration, Guid> configurations,
	IUserScopeService scope, IPayrollInputRevisionStore inputRevisions,
	EsiConfigurationPeriodGuard periods) : ApplicationService, IPfCoverageAppService
{
	[Authorize(Permissions.View)]
	public async Task<PfCoverageSettingsDto> GetAsync()
	{
		await AuthorizationService.CheckAsync(Permissions.View);
		return await ReadAsync();
	}
	[Authorize(Permissions.Add), UnitOfWork(isTransactional: true)]
	public async Task<PfCoverageSettingsDto> SaveAsync(SavePfCoverageDto input)
	{
		await AuthorizationService.CheckAsync(Permissions.Add);
		if (!await SchoolWideAsync()) throw new BusinessException(HRErrorCodes.TakeAttendanceMediumStageOutOfScope);
		ArgumentNullException.ThrowIfNull(input);
		if (!new SavePfCoverageDtoValidator().Validate(input).IsValid)
			throw new BusinessException("HR:Payroll:PfCoverageConfigurationInvalid");
		var localNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(Clock.Now, DateTimeKind.Utc), TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata"));
		if (input.EffectiveMonth.Date < new DateTime(localNow.Year, localNow.Month, 1))
			throw new BusinessException("HR:Payroll:PfCoveragePastMonth");
		await inputRevisions.LockAsync();
		var key = input.IdempotencyKey.ToString("N");
		var replay = await configurations.FindAsync(x => x.RequestKey == key);
		if (replay != null)
		{
			if (replay.IsCovered != input.IsCovered || replay.EffectiveMonth != input.EffectiveMonth.Date ||
				replay.EvidenceReference != input.EvidenceReference.Trim())
				throw new BusinessException("HR:Payroll:PfCoverageSaveConflict");
			return await ReadAsync();
		}
		var current = (await configurations.GetListAsync(x => !x.IsCancelled && x.EffectiveMonth <= input.EffectiveMonth))
			.OrderByDescending(x => x.EffectiveMonth).FirstOrDefault();
		if (current?.Id != input.ExpectedConfigurationId) throw new BusinessException("HR:Payroll:PfCoverageSaveConflict");
		if (current?.EffectiveMonth == input.EffectiveMonth.Date)
		{
			current.Cancel();
			await configurations.UpdateAsync(current, autoSave: true);
		}
		await configurations.InsertAsync(new PfCoverageConfiguration(GuidGenerator.Create(), CurrentTenant.Id,
			input.EffectiveMonth, input.IsCovered, input.EvidenceReference, key));
		await periods.InvalidateAsync(input.EffectiveMonth, null);
		await CurrentUnitOfWork!.SaveChangesAsync();
		return await ReadAsync();
	}
	private async Task<bool> SchoolWideAsync() => CurrentUser.Id.HasValue &&
		HrUserScopeAccess.HasHrWideAccess(await scope.GetUserScopeAsync(CurrentUser.Id.Value));
	private async Task<PfCoverageSettingsDto> ReadAsync() => new() {
		HasSchoolWideScope = await SchoolWideAsync(),
		History = (await configurations.GetListAsync()).OrderByDescending(x => x.EffectiveMonth).Select(x => new PfCoverageDto {
			Id = x.Id, EffectiveMonth = x.EffectiveMonth, IsCovered = x.IsCovered,
			EvidenceReference = x.EvidenceReference, IsCancelled = x.IsCancelled
		}).ToList()
	};
}
