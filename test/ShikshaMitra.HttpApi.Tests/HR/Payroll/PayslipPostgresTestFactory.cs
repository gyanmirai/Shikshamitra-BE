using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Mail;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using ShikshaMitra.HR.Payroll.SalarySlips;
using Volo.Abp.Emailing;

namespace ShikshaMitra.HR.Payroll;

public class PayslipPostgresTestFactory : PayrollPostgresTestFactory
{
	protected override string ConnectionVariable => "PAYROLL_REGISTRATION_TEST_POSTGRES";
	public ConcurrentQueue<CapturedPayslipEmail> Messages { get; } = new();
	public bool FailEmail { get; set; }
	public ILogger<PayslipDeliveryBackgroundJob> DeliveryLogger { get; } = Substitute.For<ILogger<PayslipDeliveryBackgroundJob>>();
	private readonly IEmailSender _sender = Substitute.For<IEmailSender>();

	protected override void ConfigureWebHost(IWebHostBuilder builder)
	{
		base.ConfigureWebHost(builder);
		_sender.SendAsync(Arg.Any<MailMessage>(), Arg.Any<bool>()).Returns(call => CaptureAsync(call.Arg<MailMessage>()));
		builder.ConfigureTestServices(services =>
		{
			services.Replace(ServiceDescriptor.Singleton(_sender));
			services.Replace(ServiceDescriptor.Singleton(DeliveryLogger));
		});
	}

	private async Task CaptureAsync(MailMessage message)
	{
		if (FailEmail) throw new SmtpException("Synthetic provider failure with protected-provider-details marker");
		if (message.To.Count != 1 || message.CC.Count != 0 || message.Bcc.Count != 0 || message.Attachments.Count != 1)
			throw new InvalidOperationException("A payslip email must contain one recipient and one attachment.");
		using var output = new MemoryStream();
		await message.Attachments[0].ContentStream.CopyToAsync(output);
		Messages.Enqueue(new CapturedPayslipEmail(message.To[0].Address,
			message.Attachments[0].Name ?? throw new InvalidOperationException("Attachment filename is required."), output.ToArray()));
	}
}

public record CapturedPayslipEmail(string Recipient, string FileName, byte[] Pdf);
