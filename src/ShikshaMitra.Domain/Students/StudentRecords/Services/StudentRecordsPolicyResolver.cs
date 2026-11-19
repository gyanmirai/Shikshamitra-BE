using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace ShikshaMitra.Students.StudentRecords;

public sealed record StudentRecordsPolicySnapshot(StudentRecordsPolicy Policy, Guid? VersionId, Guid? ScopeAcademicYearId, bool IsStarter, int VersionNumber = 0);

public class StudentRecordsPolicyResolver(IRepository<StudentRecordsConfiguration, Guid> configurations,
	IRepository<StudentRecordsPolicyVersion, Guid> versions) : DomainService
{
	public async Task<StudentRecordsPolicySnapshot> ResolveAsync(Guid? academicYearId)
	{
		if (!CurrentTenant.Id.HasValue) throw new BusinessException(StudentRecordsErrorCodes.TenantRequired);
		var query = await configurations.GetQueryableAsync();
		var configuration = await AsyncExecuter.FirstOrDefaultAsync(query.Where(x => x.TenantId == CurrentTenant.Id &&
			x.PublishedVersionId.HasValue && (x.AcademicYearId == academicYearId || x.AcademicYearId == null))
			.OrderByDescending(x => x.AcademicYearId.HasValue));
		if (configuration == null) return new(StudentRecordsPolicy.Starter(), null, null, true);
		var version = await versions.GetAsync(configuration.PublishedVersionId!.Value);
		if (version.TenantId != CurrentTenant.Id || version.ConfigurationId != configuration.Id)
			throw new BusinessException(StudentRecordsErrorCodes.InvalidPublishedVersion);
		return new(StudentRecordsPolicy.FromJson(version.PolicyJson), version.Id, version.AcademicYearId, false, version.VersionNumber);
	}
}
