using System;
using System.Collections.Generic;
using System.Linq;
using ShikshaMitra.Academic.Mobile;

namespace ShikshaMitra.Academic.HomeworkAssignment;

internal static class SubmissionAttachmentMapper
{
    public static List<SubmissionAttachmentDto> Map(StudentSubmissionEntity? submission)
    {
        if (submission == null || submission.Status == SubmissionStatus.Draft) return [];
        var attachments = MobileAttachmentStore.Read(submission.MobileAttachmentsJson);
        if (attachments.Count > 0)
            return attachments.Select(file => new SubmissionAttachmentDto(file.Id, file.Name,
                FileUrl(file.BlobName, submission.TenantId), file.ContentType, file.Size)).ToList();
        if (string.IsNullOrWhiteSpace(submission.SubmittedDocumentUrl)) return [];
        var url = FileUrl(submission.SubmittedDocumentUrl, submission.TenantId);
        var name = string.IsNullOrWhiteSpace(submission.OriginalFileName)
            ? Uri.UnescapeDataString(url[(url.LastIndexOf('/') + 1)..])
            : submission.OriginalFileName;
        return [new(null, name, url, null, null)];
    }

    private static string FileUrl(string reference, Guid? tenantId)
    {
        var path = Uri.TryCreate(reference, UriKind.Absolute, out var uri) ? uri.AbsolutePath : reference;
        if (path.StartsWith("/api/files/", StringComparison.Ordinal)) path = Uri.UnescapeDataString(path[11..]);
        var prefix = $"files/{tenantId?.ToString() ?? "host"}/";
        if (path.StartsWith(prefix, StringComparison.Ordinal)) path = path[prefix.Length..];
        if (string.IsNullOrWhiteSpace(path) || path.Contains('/') || path.Contains('\\') || path.Contains("..", StringComparison.Ordinal))
            throw new Volo.Abp.BusinessException("ShikshaMitra.Homework:InvalidAttachmentReference");
        return $"/api/files/{Uri.EscapeDataString(path)}";
    }
}
