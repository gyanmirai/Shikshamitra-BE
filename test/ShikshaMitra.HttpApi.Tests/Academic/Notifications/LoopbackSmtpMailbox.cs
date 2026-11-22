using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MimeKit;
using Volo.Abp.Emailing.Smtp;

namespace ShikshaMitra.Academic.Notifications;

internal sealed class LoopbackSmtpMailbox : IAsyncDisposable
{
	private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
	private readonly CancellationTokenSource _stop = new();
	private readonly Task _server;

	public const string FromAddress = "school-notifications@example.invalid";
	public ConcurrentQueue<AcceptedMail> Accepted { get; } = new();
	public ConcurrentDictionary<string, string> RecipientRejections { get; } = new(StringComparer.OrdinalIgnoreCase);
	public ISmtpEmailSenderConfiguration Configuration { get; }

	public LoopbackSmtpMailbox()
	{
		_listener.Start();
		Configuration = new LoopbackConfiguration(((IPEndPoint)_listener.LocalEndpoint).Port);
		_server = ServeAsync(_stop.Token);
	}

	private async Task ServeAsync(CancellationToken cancellationToken)
	{
		while (!cancellationToken.IsCancellationRequested)
		{
			using var client = await _listener.AcceptTcpClientAsync(cancellationToken);
			await ReceiveAsync(client, cancellationToken);
		}
	}

	private async Task ReceiveAsync(TcpClient client, CancellationToken cancellationToken)
	{
		using var stream = client.GetStream();
		using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
		await using var writer = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true)
		{
			AutoFlush = true, NewLine = "\r\n"
		};
		string? recipient = null;
		await ReplyAsync("220 loopback.test ESMTP");
		while (await reader.ReadLineAsync(cancellationToken) is { } command)
		{
			switch (command.Split(' ', 2)[0].ToUpperInvariant())
			{
				case "EHLO":
				case "HELO":
					await ReplyAsync("250 loopback.test");
					break;
				case "MAIL":
				case "RSET":
					recipient = null;
					await ReplyAsync("250 OK");
					break;
				case "RCPT":
					var address = command[(command.IndexOf('<') + 1)..command.IndexOf('>')];
					if (RecipientRejections.TryGetValue(address, out var rejection))
						await ReplyAsync(rejection);
					else
					{
						recipient = address;
						await ReplyAsync("250 OK");
					}
					break;
				case "DATA" when recipient != null:
					await ReplyAsync("354 End data with <CRLF>.<CRLF>");
					Accepted.Enqueue(new(recipient, await ReadMessageAsync(reader, cancellationToken)));
					await ReplyAsync("250 OK queued");
					break;
				case "QUIT":
					await ReplyAsync("221 Goodbye");
					return;
				default:
					throw new InvalidOperationException("Unexpected SMTP command at the loopback mailbox.");
			}
		}

		Task ReplyAsync(string response) => writer.WriteLineAsync(response.AsMemory(), cancellationToken);
	}

	private static async Task<MimeMessage> ReadMessageAsync(StreamReader reader, CancellationToken cancellationToken)
	{
		var data = new StringBuilder();
		while (await reader.ReadLineAsync(cancellationToken) is { } line)
		{
			if (line == ".")
			{
				using var message = new MemoryStream(Encoding.UTF8.GetBytes(data.ToString()));
				return MimeMessage.Load(message, cancellationToken);
			}
			data.Append(line.StartsWith("..", StringComparison.Ordinal) ? line[1..] : line).Append("\r\n");
		}
		throw new EndOfStreamException("SMTP client disconnected before the end of DATA.");
	}

	public async ValueTask DisposeAsync()
	{
		await _stop.CancelAsync();
		try
		{
			await _server;
		}
		catch (OperationCanceledException) when (_stop.IsCancellationRequested)
		{
		}
		finally
		{
			_listener.Stop();
			_stop.Dispose();
		}
	}

	internal sealed record AcceptedMail(string Recipient, MimeMessage Message);

	private sealed class LoopbackConfiguration(int port) : ISmtpEmailSenderConfiguration
	{
		public Task<string> GetHostAsync() => Task.FromResult(IPAddress.Loopback.ToString());
		public Task<int> GetPortAsync() => Task.FromResult(port);
		public Task<string> GetUserNameAsync() => Task.FromResult(string.Empty);
		public Task<string> GetPasswordAsync() => Task.FromResult(string.Empty);
		public Task<string?> GetDomainAsync() => Task.FromResult<string?>(null);
		public Task<bool> GetEnableSslAsync() => Task.FromResult(false);
		public Task<bool> GetUseDefaultCredentialsAsync() => Task.FromResult(true);
		public Task<string> GetDefaultFromAddressAsync() => Task.FromResult(FromAddress);
		public Task<string> GetDefaultFromDisplayNameAsync() => Task.FromResult("Fixture school");
	}
}
