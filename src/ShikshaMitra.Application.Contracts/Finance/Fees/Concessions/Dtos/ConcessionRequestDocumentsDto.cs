using System;
using System.Collections.Generic;

namespace ShikshaMitra.Finance.Fees.Concessions.Dtos;

/// <summary>
/// Response body for GET /api/concession-request/{concessionRequestId}/document-upload.
///
/// <para>
/// Step 5 — Document Upload (Edit / Resume). Returns every document slot defined on the
/// concession type (template) merged with previously saved upload data so the UI can
/// pre-populate the document list.
/// </para>
/// </summary>
public class ConcessionRequestDocumentsDto
{
    /// <summary>ID of the anchor concession request.</summary>
    public Guid ConcessionRequestId { get; set; }

    /// <summary>ID of the student who owns this request batch.</summary>
    public Guid StudentId { get; set; }

    /// <summary>
    /// One entry per concession type selected in Step 2.
    /// Each entry lists all document slots (template) merged with previously saved upload data.
    /// </summary>
    public List<DocumentsByTypeDto> ConcessionTypeDocuments { get; set; } = new();
}
