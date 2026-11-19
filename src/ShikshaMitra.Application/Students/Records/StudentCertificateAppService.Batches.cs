using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students.StudentRecords;
using Volo.Abp;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Students.Records;

public partial class StudentCertificateAppService
{
	public async Task<PagedResultDto<StudentCertificateBatchDto>> GetBatchesAsync(StudentCertificateBatchQuery input)
	{
		await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.Students.Records_Certificates.View);
		ValidateCertificateType(input.CertificateType);
		if (input.SkipCount < 0 || input.MaxResultCount is < 1 or > 100 || input.GeneratedTo <= input.GeneratedFrom ||
			input.GeneratedTo - input.GeneratedFrom > TimeSpan.FromDays(32))
			throw new BusinessException(StudentErrorCodes.InvalidCertificateBatchQuery);
		var from = Clock.Kind == DateTimeKind.Utc ? input.GeneratedFrom.UtcDateTime : input.GeneratedFrom.LocalDateTime;
		var to = Clock.Kind == DateTimeKind.Utc ? input.GeneratedTo.UtcDateTime : input.GeneratedTo.LocalDateTime;
		var query = (await GetVisibleCertificateQueryAsync()).Where(c => c.CertificateType == input.CertificateType && c.CreationTime >= from && c.CreationTime < to);
		var rows = await AsyncExecuter.ToListAsync(query.Select(c => new
		{
			c.Id, c.StudentId, c.IdempotencyKey, c.CertificateType, c.CreationTime, c.CertificateNumber, c.Status
		}));
		var batches = rows.GroupBy(c => GetGenerationBatchId(c.Id, c.StudentId, c.IdempotencyKey))
			.Select(group => new StudentCertificateBatchDto
			{
				Id = group.Key, CertificateType = input.CertificateType.ToString(),
				GeneratedAt = new DateTimeOffset(DateTime.SpecifyKind(group.Min(c => c.CreationTime), Clock.Kind == DateTimeKind.Utc ? DateTimeKind.Utc : DateTimeKind.Local)),
				FirstCertificateNumber = group.Min(c => c.CertificateNumber)!, LastCertificateNumber = group.Max(c => c.CertificateNumber)!,
				StudentCount = group.Count(),
				Status = group.All(c => c.Status == StudentCertificateStatus.Cancelled) ? "Voided" :
					group.Any(c => c.Status == StudentCertificateStatus.Cancelled) ? "Partly voided" : "Generated"
			}).OrderByDescending(b => b.GeneratedAt).ThenBy(b => b.Id).ToList();
		return new(batches.Count, batches.Skip(input.SkipCount).Take(input.MaxResultCount).ToList());
	}

	public async Task<StudentCertificateBatchDetailDto> GetBatchAsync(Guid batchId, StudentCertificateType certificateType)
	{
		await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.Students.Records_Certificates.View);
		ValidateCertificateType(certificateType);
		var prefix = batchId.ToString("D") + "-";
		var query = (await GetVisibleCertificateQueryAsync()).Where(c => c.CertificateType == certificateType &&
			(c.Id == batchId || c.IdempotencyKey != null && c.IdempotencyKey.StartsWith(prefix)));
		var rows = await AsyncExecuter.ToListAsync(query.OrderBy(c => c.CertificateNumber));
		var certificates = rows.Where(c => GetGenerationBatchId(c.Id, c.StudentId, c.IdempotencyKey) == batchId).ToList();
		if (certificates.Count == 0) throw new BusinessException(StudentErrorCodes.CertificateNotFound);
		return new() { Id = batchId, Certificates = certificates.Select(BuildStoredCertificateResponse).ToList() };
	}

	private async Task<IQueryable<StudentCertificate>> GetVisibleCertificateQueryAsync()
	{
		await recordsAccess.EnsureActiveActorAsync();
		if (!CurrentTenant.Id.HasValue) throw new BusinessException("Students:Records:TenantRequired");
		var students = await enrollmentScope.ApplyStudentScopeAsync(await studentRepository.GetQueryableWithoutAutoIncludesAsync());
		var onboardingIds = await recordsAccess.GetOnboardingEligibleStudentIdsAsync();
		var overrides = await legacyOverrides.GetQueryableAsync();
		students = students.Where(s => s.Status == StudentLifeCycleStatusEnum.Enrolled || onboardingIds.Contains(s.Id) ||
			overrides.Any(o => o.TenantId == CurrentTenant.Id && o.StudentId == s.Id && o.Status == "Approved"));
		var roles = await recordsAccess.GetDatabaseRoleNamesAsync();
		var query = (await certificateRepository.GetQueryableAsync()).Where(c => c.TenantId == CurrentTenant.Id && students.Any(s => s.Id == c.StudentId));
		var years = await AsyncExecuter.ToListAsync(students.Select(s => s.AcademicYearId).Union(query.Select(c => c.AcademicYearId)).Distinct());
		var hiddenYears = new List<Guid>();
		var hideNullYear = false;
		foreach (var year in years)
		{
			if (!(await recordsPolicies.ResolveAsync(year)).Policy.IsMasked("Identity", roles)) continue;
			if (year.HasValue) hiddenYears.Add(year.Value); else hideNullYear = true;
		}
		var hidden = hiddenYears.ToArray();
		return query.Where(c => (c.AcademicYearId.HasValue ? !Enumerable.Contains(hidden, c.AcademicYearId.Value) : !hideNullYear) &&
			students.Any(s => s.Id == c.StudentId && (s.AcademicYearId.HasValue ? !Enumerable.Contains(hidden, s.AcademicYearId.Value) : !hideNullYear)));
	}

	private static Guid GetGenerationBatchId(Guid certificateId, Guid studentId, string? key) =>
		key?.Length == 73 && key[36] == '-' && Guid.TryParse(key[..36], out var batchId) &&
		Guid.TryParse(key[37..], out var selectedStudent) && selectedStudent == studentId ? batchId : certificateId;
}
