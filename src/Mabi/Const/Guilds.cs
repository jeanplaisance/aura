﻿// Copyright (c) Aura development team - Licensed under GNU GPL
// For more information, see license file in the main folder

using System;

namespace Aura.Mabi.Const
{
	public enum GuildLevel : byte
	{
		Beginner = 0,
		Basic = 1,
		Advanced = 2,
		Great = 3,
		Grand = 4,
	}

	public enum GuildType : byte
	{
		Battle = 0,
		Adventure = 1,
		Manufacturing = 2,
		Commerce = 3,
		Social = 4,
		Other = 5,
	}

	public enum GuildMemberRank
	{
		Leader = 0,
		Officer = 1,
		BronzeCrown = 2,
		SeniorMember = 3,
		UnkMember = 4,
		Member = 5,
		Applied = 254,
		Declined = 255,
	}

	public enum GuildStoneType
	{
		Normal = 211,
		Hope = 40209,
		Courage = 40210,
	}

	[Flags]
	public enum GuildMessages : byte
	{
		None = 0x00,
		Accepted = 0x01,
		Rejected = 0x02,
		NewApplication = 0x04,
		MemberLeft = 0x08,
		RankChanged = 0x10,
	}

	[Flags]
	public enum GuildOptions : byte
	{
		None = 0x00,
		GuildHall = 0x01,
		Warp = 0x02,
	}
}
