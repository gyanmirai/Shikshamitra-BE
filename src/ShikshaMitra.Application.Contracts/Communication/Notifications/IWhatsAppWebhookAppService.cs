using System;
using System.Threading;
using System.Threading.Tasks;

namespace ShikshaMitra.Communication.Notifications;

public enum WhatsAppWebhookReceiptResult { Accepted, InvalidSignature, InvalidPayload }

public interface IWhatsAppWebhookAppService
{
	const int MaximumBodyBytes = 256 * 1024;

	string? VerifyChallenge(string? mode, string? token, string? challenge);
	Task<WhatsAppWebhookReceiptResult> ReceiveAsync(string? signature, ReadOnlyMemory<byte> payload, CancellationToken cancellationToken);
}
