using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmExportWorkflow(
	IRepository<AdmissionCrmExportRequest, Guid> exportRepository,
	AdmissionCrmExportContentBuilder contentBuilder,
	AdmissionCrmGovernanceAccess access)
	: ApplicationService, ITransientDependency
{
	public async Task<AdmissionCrmExportRequestDto> RequestExportAsync(Guid academicYearId, RequestAdmissionCrmExportDto input)
	{
		await access.EnsureAcademicYearAccessAsync(academicYearId, nameof(RequestExportAsync));
		if (input.ExpiresInHours is < 1 or > 168) throw new BusinessException("AdmissionCrm:InvalidExportExpiry");
		var now = Clock.Now;
		var content = await contentBuilder.BuildAsync(academicYearId, input, now, enforceUserScope: true);
		var id = GuidGenerator.Create();
		var entity = new AdmissionCrmExportRequest(id, CurrentTenant.Id, academicYearId, input.ReportType,
			AdmissionCrmGovernanceMapper.Serialize(input.Filters), $"admissions-{input.ReportType.ToString().ToLowerInvariant()}-{now:yyyyMMddHHmm}-{id:N}.csv",
			content, UserId(), now, now.AddHours(input.ExpiresInHours));
		await exportRepository.InsertAsync(entity, autoSave: true);
		return AdmissionCrmGovernanceMapper.Export(entity);
	}

	public async Task<IReadOnlyList<AdmissionCrmExportRequestDto>> GetExportsAsync(Guid academicYearId)
	{
		await access.EnsureAcademicYearAccessAsync(academicYearId, nameof(GetExportsAsync));
		var queryable = await exportRepository.GetQueryableAsync();
		var query = queryable.Where(x => x.TenantId == CurrentTenant.Id && x.AcademicYearId == academicYearId);
		if (!await access.HasAcademicYearWideScopeAsync(academicYearId))
		{
			var currentUserId = UserId();
			query = query.Where(x => x.RequestedByUserId == currentUserId);
		}
		var items = await AsyncExecuter.ToListAsync(query);
		var expired = items.Where(x => x.ExpireContent(Clock.Now)).ToList();
		if (expired.Count > 0) await exportRepository.UpdateManyAsync(expired, autoSave: true);
		return items.OrderByDescending(x => x.RequestedAt).Take(200).Select(AdmissionCrmGovernanceMapper.Export).ToList();
	}

	public async Task<AdmissionCrmExportDownloadDto> DownloadExportAsync(Guid id)
	{
		var entity = await exportRepository.GetAsync(id);
		await access.EnsureAcademicYearAccessAsync(entity.AcademicYearId, nameof(DownloadExportAsync));
		if (!await access.HasAcademicYearWideScopeAsync(entity.AcademicYearId) && entity.RequestedByUserId != UserId())
			throw new AbpAuthorizationException("The export belongs to a different admissions scope.");
		entity.RegisterDownload(UserId(), Clock.Now);
		await exportRepository.UpdateAsync(entity, autoSave: true);
		return new AdmissionCrmExportDownloadDto
		{
			FileName = entity.FileName, ContentType = entity.ContentType,
			Base64Content = Convert.ToBase64String(Encoding.UTF8.GetBytes(entity.Content)), ExpiresAt = entity.ExpiresAt
		};
	}

	private Guid UserId() => CurrentUser.Id ?? throw new AbpAuthorizationException("Authenticated user required.");
}
