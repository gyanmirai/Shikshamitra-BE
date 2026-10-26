using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.BlobStoring;
using Volo.Abp.Uow;
using ShikshaMitra.TenantSettings.Formatting;

namespace ShikshaMitra.TenantSettings.SchoolDocuments;

public partial class SchoolDocumentAppService
{
	[UnitOfWork(isTransactional: true)]
	public async Task<SchoolDocumentDto> CreateAsync(SchoolDocumentWriteDto input)
	{
		await CheckWriteAsync(SchoolDocumentPermissions.Add);
		var tenantId = RequireTenant();
		if (input.RetainedFileIds.Count != 0) throw new BusinessException(SchoolDocumentConsts.InvalidFiles);
		SchoolDocumentFileRules.ValidateCount(input.Files.Count);
		var rules = await LazyServiceProvider.LazyGetRequiredService<InstituteFieldFormattingStore>().GetAsync("schoolDocuments");
		var name = InstituteFieldFormattingCatalog.Required(rules, "name", input.Name, SchoolDocumentConsts.MaxNameLength);
		var notes = InstituteFieldFormattingCatalog.Optional(rules, "notes", input.Notes, SchoolDocumentConsts.MaxNotesLength);
		var document = new SchoolDocument(GuidGenerator.Create(), tenantId, name, input.Category, notes,
			CurrentUser.UserName ?? "School staff");
		var prepared = PrepareFiles(document.Id, input.Files);
		await documents.InsertAsync(document);
		await SaveFilesAsync(prepared);
		await CurrentUnitOfWork!.SaveChangesAsync();
		return Map(document, prepared.Select(x => x.File));
	}

	[UnitOfWork(isTransactional: true)]
	public async Task<SchoolDocumentDto> UpdateAsync(Guid id, SchoolDocumentWriteDto input)
	{
		await CheckWriteAsync(SchoolDocumentPermissions.Add);
		var document = await FindDocumentAsync(id);
		var existing = await FilesAsync(id);
		var retained = input.RetainedFileIds.ToHashSet();
		if (retained.Count != input.RetainedFileIds.Count || retained.Any(fileId => existing.All(x => x.Id != fileId)))
			throw new BusinessException(SchoolDocumentConsts.InvalidFiles);
		var removed = existing.Where(x => !retained.Contains(x.Id)).ToList();
		if (removed.Count != 0) await AuthorizationService.CheckAsync(SchoolDocumentPermissions.Delete);
		SchoolDocumentFileRules.ValidateCount(retained.Count + input.Files.Count);
		document.UpdateDetails(input.Name, input.Category, input.Notes, input.ConcurrencyStamp ?? "");
		// Force a parent write for attachment-only edits; ABP owns the concurrency stamp.
		document.LastModificationTime = Clock.Now;
		var prepared = PrepareFiles(id, input.Files);
		await documents.UpdateAsync(document);
		foreach (var file in removed) await files.DeleteAsync(file);
		await SaveFilesAsync(prepared);
		await CurrentUnitOfWork!.SaveChangesAsync();
		return Map(document, existing.Where(x => retained.Contains(x.Id)).Concat(prepared.Select(x => x.File)));
	}

	[UnitOfWork(isTransactional: true)]
	public async Task DeleteAsync(Guid id)
	{
		await CheckWriteAsync(SchoolDocumentPermissions.Delete);
		var document = await FindDocumentAsync(id);
		foreach (var file in await FilesAsync(id)) await files.DeleteAsync(file);
		await documents.DeleteAsync(document);
	}

	private async Task CheckWriteAsync(string permission)
	{
		await AuthorizationService.CheckAsync(SchoolDocumentPermissions.View);
		await AuthorizationService.CheckAsync(permission);
		RequireTenant();
	}

	private List<(SchoolDocumentFile File, byte[] Content)> PrepareFiles(Guid documentId, List<SchoolDocumentUploadDto> uploads)
	{
		var tenantId = RequireTenant();
		return uploads.Select(upload =>
		{
			var validated = SchoolDocumentFileRules.Validate(upload.FileName, upload.ContentType, upload.Content);
			return (new SchoolDocumentFile(GuidGenerator.Create(), tenantId, documentId,
				validated.FileName, validated.ContentType, upload.Content.LongLength), upload.Content);
		}).ToList();
	}

	private async Task SaveFilesAsync(List<(SchoolDocumentFile File, byte[] Content)> prepared)
	{
		foreach (var (file, content) in prepared)
		{
			await blobs.SaveAsync(file.BlobName, content, overrideExisting: false);
			await files.InsertAsync(file);
		}
	}
}
