using System;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.HR.Payroll.Settings;

public interface IDayCalculationSettingRepository : IRepository<DayCalculationSetting, Guid>
{
	Task<DayCalculationSetting?> FindByTenantAsync(Guid? tenantId, CancellationToken cancellationToken = default);
	Task<DayCalculationSetting?> FindByTenantAndAcademicYearAsync(Guid? tenantId, Guid academicYearId, CancellationToken cancellationToken = default);
	Task<DayCalculationSetting?> FindEffectiveAsync(Guid? tenantId, Guid? academicYearId, CancellationToken cancellationToken = default);
}
