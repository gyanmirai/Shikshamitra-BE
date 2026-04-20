using System.Collections.Generic;
using System.Text.Json.Serialization;
using Volo.Abp.Domain.Values;

namespace ShikshaMitra.Finance.FeeCollection.PaymentDetails;

/// <summary>
/// Abstract base value object for payment method-specific details.
/// Each concrete subclass carries data relevant only to its payment method type.
/// Stored as a JSON column via EF Core OwnsOne + ToJson().
/// The <see cref="JsonDerivedTypeAttribute"/> attributes enable polymorphic
/// System.Text.Json deserialization without a custom converter.
/// </summary>
[JsonDerivedType(typeof(ChequeDetails), typeDiscriminator: nameof(ChequeDetails))]
[JsonDerivedType(typeof(DemandDraftDetails), typeDiscriminator: nameof(DemandDraftDetails))]
[JsonDerivedType(typeof(UpiDetails), typeDiscriminator: nameof(UpiDetails))]
[JsonDerivedType(typeof(BankTransferDetails), typeDiscriminator: nameof(BankTransferDetails))]
public abstract class PaymentDetails : ValueObject
{
    // Intentionally empty — acts as a discriminated union base.
    // Derived classes define their own fields and equality atoms.
}
