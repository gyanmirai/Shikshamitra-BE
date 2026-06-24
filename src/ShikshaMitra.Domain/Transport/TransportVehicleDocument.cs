using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Transport;

public class TransportVehicleDocument : FullAuditedEntity<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid AcademicMediumId { get; private set; }
	public Guid VehicleId { get; private set; }
	public Guid RequiredDocumentId { get; private set; }
	public string FileName { get; private set; } = string.Empty;
	public string ContentType { get; private set; } = string.Empty;
	public long Size { get; private set; }
	public string BlobName { get; private set; } = string.Empty;

	private TransportVehicleDocument()
	{
	}

	public TransportVehicleDocument(
		Guid id,
		Guid? tenantId,
		Guid academicMediumId,
		Guid vehicleId,
		Guid requiredDocumentId,
		string fileName,
		string contentType,
		long size,
		string blobName) : base(id)
	{
		TenantId = tenantId;
		AcademicMediumId = academicMediumId;
		VehicleId = vehicleId;
		RequiredDocumentId = requiredDocumentId;
		UpdateFile(fileName, contentType, size, blobName);
	}

	public void UpdateFile(string fileName, string contentType, long size, string blobName)
	{
		FileName = fileName.Trim();
		ContentType = contentType.Trim();
		Size = size;
		BlobName = blobName.Trim();
	}
}
