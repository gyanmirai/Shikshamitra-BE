using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Common;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.BlobStoring;
using Volo.Abp.Content;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.Academic.Mobile;

public record MobileStoredAttachment(Guid Id, string Name, string BlobName, string ContentType, long Size);

[Authorize]
[RemoteService(false)]
public class MobileAttachmentStore(IBlobContainer blobs) : ApplicationService, ITransientDependency
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
	private string Prefix(Guid studentId, Guid referenceId) => $"mobile-academics/{CurrentTenant.Id:N}/{CurrentUser.Id:N}/{studentId:N}/{referenceId:N}/";

	public async Task<MobileStoredAttachment> UploadAsync(Guid studentId, Guid referenceId, IRemoteStreamContent file)
	{
		if (!CurrentTenant.Id.HasValue || !CurrentUser.Id.HasValue) throw MobileAcademicContextAppService.Invalid("Authentication required.");
		using var content = new MemoryStream();
		var stream = file.GetStream();
		var buffer = new byte[81920];
		int length;
		while ((length = await stream.ReadAsync(buffer)) > 0)
		{
			if (content.Length + length > 5 * 1024 * 1024) throw MobileAcademicContextAppService.Invalid("Files must be at most 5 MB.");
			await content.WriteAsync(buffer.AsMemory(0, length));
		}
		var name = Path.GetFileName((file.FileName ?? "").Replace('\\', '/'));
		var bytes = content.ToArray();
		FileValidationService.ValidateDocumentUpload(name, bytes, bytes.Length);
		var extension = Path.GetExtension(name).ToLowerInvariant();
		if (!FileValidationService.IsMatchingDocumentContentType(extension, file.ContentType))
			throw MobileAcademicContextAppService.Invalid("The file content type does not match its extension.");
		var id = GuidGenerator.Create();
		var blob = $"files/{CurrentTenant.Id}/{id:N}{extension}";
		var attachment = new MobileStoredAttachment(id, name, blob, file.ContentType!, bytes.Length);
		await blobs.SaveAsync(blob, bytes);
		await blobs.SaveAsync(Prefix(studentId, referenceId) + id.ToString("N") + ".json", JsonSerializer.SerializeToUtf8Bytes(attachment, JsonOptions));
		return attachment;
	}

	public async Task<List<MobileStoredAttachment>> ResolveAsync(Guid studentId, Guid referenceId, List<Guid> ids, string? retainedJson = null)
	{
		if (ids.Count > 5 || ids.Distinct().Count() != ids.Count) throw MobileAcademicContextAppService.Invalid("Supply up to five distinct attachment IDs.");
		var retained = Read(retainedJson);
		var result = new List<MobileStoredAttachment>();
		foreach (var id in ids)
		{
			var item = retained.SingleOrDefault(x => x.Id == id);
			if (item == null)
			{
				var meta = await blobs.GetAllBytesOrNullAsync(Prefix(studentId, referenceId) + id.ToString("N") + ".json");
				if (meta == null) throw MobileAcademicContextAppService.Invalid("The attachment is unavailable or belongs to another upload.");
				item = JsonSerializer.Deserialize<MobileStoredAttachment>(meta, JsonOptions)!;
			}
			if (!item.BlobName.StartsWith($"files/{CurrentTenant.Id}/", StringComparison.Ordinal) || !await blobs.ExistsAsync(item.BlobName))
				throw MobileAcademicContextAppService.Invalid("The attachment is unavailable.");
			result.Add(item);
		}
		return result;
	}

	public async Task DeleteAsync(Guid studentId, Guid referenceId, Guid id, string? retainedJson)
	{
		if (Read(retainedJson).Any(x => x.Id == id)) throw MobileAcademicContextAppService.Invalid("Remove the attachment from the draft before deleting it.");
		var item = (await ResolveAsync(studentId, referenceId, [id])).Single();
		await blobs.DeleteAsync(item.BlobName);
		await blobs.DeleteAsync(Prefix(studentId, referenceId) + id.ToString("N") + ".json");
	}

	public async Task<MobileFileDto> DownloadAsync(MobileStoredAttachment item) =>
		new(await blobs.GetAllBytesAsync(item.BlobName), item.Name, item.ContentType);

	public async Task<MobileFileDto> DownloadPublishedAsync(string blobName, string fileName, string contentType)
	{
		var key = blobName;
		if (Uri.TryCreate(blobName, UriKind.Absolute, out var uri)) key = uri.AbsolutePath;
		if (key.StartsWith("/api/files/", StringComparison.Ordinal)) key = Uri.UnescapeDataString(key[11..]);
		if (key.Contains("..", StringComparison.Ordinal) || key.Contains('\\') || key.StartsWith('/'))
			throw MobileAcademicContextAppService.Invalid("Invalid resource reference.");
		if (!key.Contains('/')) key = $"files/{CurrentTenant.Id}/{key}";
		if (!CurrentTenant.Id.HasValue || !key.StartsWith($"files/{CurrentTenant.Id}/", StringComparison.Ordinal))
			throw MobileAcademicContextAppService.Invalid("The resource does not belong to this school.");
		return new(await blobs.GetAllBytesAsync(key), Path.GetFileName(fileName), contentType);
	}

	public static string Serialize(List<MobileStoredAttachment> attachments) => JsonSerializer.Serialize(attachments, JsonOptions);
	public static List<MobileStoredAttachment> Read(string? json) => string.IsNullOrEmpty(json) ? [] : JsonSerializer.Deserialize<List<MobileStoredAttachment>>(json, JsonOptions)!;
	public static MobileAttachmentDto Map(MobileStoredAttachment item, string route) => new(item.Id, item.Name, route, item.ContentType, item.Size);
}
