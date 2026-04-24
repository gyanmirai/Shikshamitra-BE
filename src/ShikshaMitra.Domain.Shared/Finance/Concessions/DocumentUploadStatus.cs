namespace ShikshaMitra.Finance.ConcessionRequests;

/// <summary>
/// Upload state of a single concession-request document.
/// </summary>
public enum DocumentUploadStatus
{
    /// <summary>Document has been selected/queued but not yet successfully uploaded.</summary>
    Pending = 0,

    /// <summary>Document has been successfully uploaded and the URL is accessible.</summary>
    Uploaded = 1,
}
