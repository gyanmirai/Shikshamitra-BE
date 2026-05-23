using System;

namespace ShikshaMitra.Finance.Fees.Concessions.Dtos;

public class ConcessionCancellationResponseDto
{
    /// <summary>ID of the cancelled request.</summary>
    public Guid StudentConcessionTypeRequestId { get; init; }

    /// <summary>Status integer (4 = Cancelled).</summary>
    public int Status { get; init; }

    public string StatusName { get; init; } = string.Empty;

    public DateTime CancelledAt { get; init; }

    public string Message { get; init; } = string.Empty;
}
