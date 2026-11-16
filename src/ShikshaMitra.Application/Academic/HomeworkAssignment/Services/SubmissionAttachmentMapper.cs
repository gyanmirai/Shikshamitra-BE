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
        return Map(submission.Status, submission.MobileAttachmentsJson, submission.SubmittedDocumentUrl,
            submission.OriginalFileName, submission.TenantId);
    }

    public static List<SubmissionAttachmentDto> Map(EvaluationSubmissionRead? submission) => submission == null
        ? [] : Map(submission.Status, submission.MobileAttachmentsJson, submission.SubmittedDocumentUrl,
            submission.OriginalFileName, submission.TenantId);

    private static List<SubmissionAttachmentDto> Map(SubmissionStatus status, string? attachmentsJson,
        string? documentUrl, string? originalName, Guid? tenantId)
    {
        if (status == SubmissionStatus.Draft) return [];
        var attachments = MobileAttachmentStore.Read(attachmentsJson);
        if (attachments.Count > 0)
            return attachments.Select(file => new SubmissionAttachmentDto(file.Id, file.Name,
                FileUrl(file.BlobName, tenantId), file.ContentType, file.Size)).ToList();
        if (string.IsNullOrWhiteSpace(documentUrl)) return [];
        var url = FileUrl(documentUrl, tenantId);
        var name = string.IsNullOrWhiteSpace(originalName)
            ? Uri.UnescapeDataString(url[(url.LastIndexOf('/') + 1)..])
            : originalName;
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
