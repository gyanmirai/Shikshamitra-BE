using System;
using ShikshaMitra.Library.Receiving;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Library;

public class LibraryStockReceiptCommand_Tests
{
	[Fact]
	public void An_Accepted_Receiving_Command_Should_Keep_The_Actual_School_Actor_Receipt_And_Retry_Identity()
	{
		var tenant = Guid.NewGuid(); var actor = Guid.NewGuid(); var receipt = Guid.NewGuid();
		var instant = new DateTimeOffset(2026, 11, 18, 13, 0, 0, TimeSpan.FromHours(5.5));
		var command = new LibraryStockReceiptCommand(Guid.NewGuid(), tenant, actor, receipt, "receive-123", new('A', 64), instant);
		Assert.Equal(tenant, command.TenantId); Assert.Equal(actor, command.ActorId); Assert.Equal(receipt, command.ReceiptId);
		Assert.Equal("receive-123", command.IdempotencyKey); Assert.Equal(new string('A', 64), command.PayloadFingerprint);
		Assert.Equal(instant.UtcDateTime, command.RecordedAt.UtcDateTime); Assert.Equal(TimeSpan.Zero, command.RecordedAt.Offset);
	}

	[Theory]
	[InlineData("")]
	[InlineData(" key")]
	[InlineData("key ")]
	[InlineData("key\nvalue")]
	public void A_Retry_Key_Should_Be_Explicit_And_Must_Not_Be_Rewritten(string key) =>
		Assert.Throws<BusinessException>(() => Command(key, new('A', 64)));

	[Fact]
	public void A_Retry_Key_Should_Be_Bounded() => Assert.Throws<BusinessException>(() => Command(new('K', 101), new('A', 64)));

	[Theory]
	[InlineData("short")]
	[InlineData("GGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGG")]
	public void An_Accepted_Command_Requires_An_Actual_Hexadecimal_Payload_Fingerprint(string fingerprint) =>
		Assert.Throws<BusinessException>(() => Command("receive-123", fingerprint));

	private static LibraryStockReceiptCommand Command(string key, string fingerprint) => new(Guid.NewGuid(),
		Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), key, fingerprint, DateTimeOffset.UtcNow);
}
