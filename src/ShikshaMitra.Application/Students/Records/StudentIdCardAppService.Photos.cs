using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Common.IdentityCards;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp.BlobStoring;

namespace ShikshaMitra.Students.Records;

public partial class StudentIdCardAppService
{
	public async Task<IdentityCardPhotoDto> GetPhotoAsync(Guid studentId)
	{
		await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.Students.Records_StudentICard.View);
		EnsureTenant();
		var snapshot = await snapshots.CaptureAsync(studentId);
		return IdentityCardPhotos.Read(studentId, await LoadOriginalPhotoAsync(snapshot.Fields));
	}
	private async Task<byte[]?> LoadOriginalPhotoAsync(Dictionary<string, string?> fields)
	{
		return fields.TryGetValue("_PhotoBlobName", out var blob) && !string.IsNullOrWhiteSpace(blob) ? await blobs.GetAllBytesOrNullAsync(blob) : null;
	}
	private async Task<Dictionary<Guid, StudentDocumentSnapshot>> CapturePhotoCohortAsync(List<Guid> ids, List<IdentityCardPhotoDto>? edits)
	{
		var cohort = new Dictionary<Guid, StudentDocumentSnapshot>();
		foreach (var id in ids) cohort.Add(id, await snapshots.CaptureAsync(id));
		var processed = await IdentityCardPhotos.ValidateAsync(edits, ids, id => LoadOriginalPhotoAsync(cohort[id].Fields));
		foreach (var (id, photo) in processed) cohort[id].Fields["_ProcessedPhotoData"] = IdentityCardPhotos.DataUri(photo);
		return cohort;
	}
}
