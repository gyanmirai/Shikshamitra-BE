//using ShikshaMitra.Common;
//using System;
//using System.Collections.Generic;
//using Volo.Abp;
//using Volo.Abp.Domain.Values;

//namespace ShikshaMitra.Students.StudentEnrollments.Registration;

//public class StudentBasicDetails(
//	Guid? userId,
//	byte[]? profilePicture,
//	string? email,
//	string? aadhaarNo,
//	string? religion,
//	string? familyCast,
//	CastType? castType,
//	string? motherTongue,
//	string? nationality,
//	string? birthPlace,
//	string? temporaryGr,
//	string? remarks
//	) : ValueObject
//{

//	public Guid? UserId { get; private set; } = userId;
//	public byte[]? ProfilePicture { get; private set; } = profilePicture;
//	public string? Email { get; private set; } = email;
//	public string? AadhaarNo { get; private set; } = aadhaarNo;
//	public string? Religion { get; private set; } = religion;
//	public string? FamilyCast { get; private set; } = familyCast;
//	public CastType? CastType { get; private set; } = castType;
//	public string? MotherTongue { get; private set; } = motherTongue;
//	public string? Nationality { get; private set; } = nationality;
//	public string? BirthPlace { get; private set; } = birthPlace;
//	public string? TemporaryGr { get; private set; } = temporaryGr;
//	public string? Remarks { get; private set; } = remarks;

//	protected override IEnumerable<object> GetAtomicValues()
//	{
//		yield return UserId;
//		yield return ProfilePicture;
//		yield return Email;
//		yield return AadhaarNo;
//		yield return Religion;
//		yield return FamilyCast;
//		yield return CastType;
//		yield return MotherTongue;
//		yield return Nationality;
//		yield return BirthPlace;
//		yield return TemporaryGr;
//		yield return Remarks;
//	}
//}