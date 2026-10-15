using System;
using System.Text.Json;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.TenantSettings.SchoolIdentity;

public class SchoolIdentityProfile : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid? AcademicYearId { get; private set; }
	public Guid? EducationBoardId { get; private set; }
	public Guid? EducationMediumId { get; private set; }
	public string ScopeKey { get; private set; } = "school";
	public string ChoicesJson { get; private set; } = "{}";

	protected SchoolIdentityProfile() { }

	public SchoolIdentityProfile(Guid id, Guid tenantId, Guid? yearId = null, Guid? boardId = null, Guid? mediumId = null) : base(id)
	{
		if (tenantId == Guid.Empty || yearId.HasValue != boardId.HasValue || (mediumId.HasValue && !boardId.HasValue))
			throw new BusinessException(SchoolIdentityErrorCodes.InvalidContext);
		TenantId = tenantId;
		AcademicYearId = yearId;
		EducationBoardId = boardId;
		EducationMediumId = mediumId;
		ScopeKey = Key(boardId, mediumId);
	}

	public static string Key(Guid? boardId, Guid? mediumId) => mediumId.HasValue
		? $"medium:{boardId:N}:{mediumId:N}"
		: boardId.HasValue ? $"board:{boardId:N}" : "school";

	public SchoolIdentityChoices Choices => JsonSerializer.Deserialize<SchoolIdentityChoices>(ChoicesJson) ?? new();

	public void Replace(SchoolIdentityChoices choices, string expectedStamp)
	{
		if (!string.Equals(ConcurrencyStamp, expectedStamp, StringComparison.Ordinal))
			throw new AbpDbConcurrencyException();
		ChoicesJson = JsonSerializer.Serialize(choices.Validated());
	}
}
