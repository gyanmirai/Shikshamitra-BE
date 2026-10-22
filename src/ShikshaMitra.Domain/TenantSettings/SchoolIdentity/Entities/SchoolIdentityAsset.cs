using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.TenantSettings.SchoolIdentity;

public class SchoolIdentityAsset : CreationAuditedEntity<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public SchoolIdentityAssetKind Kind { get; private set; }
	public Guid? EducationBoardId { get; private set; }
	public Guid? EducationMediumId { get; private set; }
	public Guid? OrganizationId { get; private set; }
	public string BlobName { get; private set; } = "";
	public string FileName { get; private set; } = "";
	public string ContentType { get; private set; } = "";
	public long Size { get; private set; }
	public int Width { get; private set; }
	public int Height { get; private set; }
	public bool IsRetained { get; private set; }
	public bool IsCleanupPending { get; private set; }
	protected SchoolIdentityAsset() { }

	public SchoolIdentityAsset(Guid id, Guid tenantId, SchoolIdentityAssetKind kind, string blobName,
		string fileName, string contentType, long size, int width, int height, Guid? boardId, Guid? mediumId, Guid? organizationId = null) : base(id)
	{
		TenantId = tenantId; Kind = kind; BlobName = blobName; FileName = fileName;
		ContentType = contentType; Size = size; Width = width; Height = height;
		EducationBoardId = boardId; EducationMediumId = mediumId;
		OrganizationId = organizationId;
	}

	public void Retain()
	{
		if (IsCleanupPending) throw new Volo.Abp.BusinessException(SchoolIdentityErrorCodes.DraftExpired);
		IsRetained = true;
	}
}
