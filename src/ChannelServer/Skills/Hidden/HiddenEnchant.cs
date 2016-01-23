﻿// Copyright (c) Aura development team - Licensed under GNU GPL
// For more information, see license file in the main folder

using Aura.Channel.Network.Sending;
using Aura.Channel.Skills.Base;
using Aura.Channel.World.Entities;
using Aura.Data;
using Aura.Data.Database;
using Aura.Mabi;
using Aura.Mabi.Const;
using Aura.Mabi.Network;
using Aura.Shared.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aura.Channel.Skills.Hidden
{
	/// <summary>
	/// Handler for both the hidden Enchant skill used by items and the normal one.
	/// </summary>
	[Skill(SkillId.HiddenEnchant, SkillId.Enchant)]
	public class HiddenEnchant : IPreparable, ICompletable
	{
		private float[] _baseChanceB00 = { 69, 65, 60, 55, 51, 46, 32, 30, 27, 25, 20, 14, 10, 6, 4 };
		private float[] _baseChanceB05 = { 73, 68, 63, 58, 53, 48, 34, 32, 29, 26, 21, 15, 10, 6, 4 };
		private float[] _baseChanceB10 = { 77, 71, 66, 61, 56, 51, 35, 33, 30, 27, 22, 16, 11, 7, 5 };
		private float[] _baseChanceB50 = { 90, 90, 90, 85, 78, 71, 50, 47, 42, 38, 31, 22, 15, 10, 7 };
		private float[] _baseChanceB60 = { 90, 90, 90, 90, 84, 76, 53, 50, 45, 41, 33, 24, 16, 10, 7 };

		/// <summary>
		/// Durability reduction for enchant scrolls.
		/// </summary>
		private const int DurabilityReduction = 1000;

		/// <summary>
		/// Prepares skill.
		/// </summary>
		/// <param name="creature"></param>
		/// <param name="skill"></param>
		/// <param name="packet"></param>
		/// <returns></returns>
		public bool Prepare(Creature creature, Skill skill, Packet packet)
		{
			var itemEntityId = packet.GetLong();
			long enchantEntityId = 0;

			if (skill.Info.Id == SkillId.HiddenEnchant)
			{
				enchantEntityId = packet.GetLong();
			}
			else if (skill.Info.Id == SkillId.Enchant)
			{
				var rightHand = creature.RightHand;
				var magazine = creature.Magazine;

				enchantEntityId = (magazine == null ? 0 : magazine.EntityId);

				if (rightHand == null || !rightHand.HasTag("/enchant/powder/"))
				{
					Log.Warning("HiddenEnchant.Prepare: Creature '{0:X16}' tried to use Enchant without powder.");
					return false;
				}

				if (magazine == null || !magazine.HasTag("/lefthand/enchant/"))
				{
					Log.Warning("HiddenEnchant.Prepare: Creature '{0:X16}' tried to use Enchant without enchant.");
					return false;
				}
			}

			// Get items
			var item = creature.Inventory.GetItem(itemEntityId);
			var enchant = creature.Inventory.GetItem(enchantEntityId);

			// Check item
			if (item == null)
			{
				Log.Warning("HiddenEnchant.Prepare: Creature '{0:X16}' tried to enchant non-existing item.");
				return false;
			}

			// Check enchant
			if (enchant == null)
			{
				Log.Warning("HiddenEnchant.Prepare: Creature '{0:X16}' tried to enchant with non-existing enchant item.");
				return false;
			}

			if (!enchant.HasTag("/enchant/"))
			{
				Log.Warning("HiddenEnchant.Prepare: Creature '{0:X16}' tried to enchant with invalid enchant scroll.");
				return false;
			}

			if (enchant.Durability == 0)
			{
				Send.Notice(creature, Localization.Get("This scroll is no longer valid for enchantment."));
				return false;
			}

			// Save items for Complete
			creature.Temp.SkillItem1 = item;
			creature.Temp.SkillItem2 = enchant;

			// Response
			Send.Echo(creature, Op.SkillUse, packet);
			skill.State = SkillState.Used;

			return true;
		}

		/// <summary>
		/// Completes skill, applying the enchant.
		/// </summary>
		/// <param name="creature"></param>
		/// <param name="skill"></param>
		/// <param name="packet"></param>
		public void Complete(Creature creature, Skill skill, Packet packet)
		{
			// Ignore parameters, use data saved in Prepare.

			var item = creature.Temp.SkillItem1;
			var enchant = creature.Temp.SkillItem2;
			var rightHand = creature.RightHand;

			var optionSetId = 0;
			var prefix = false;
			var suffix = false;

			creature.Temp.SkillItem1 = null;
			creature.Temp.SkillItem2 = null;

			// Get option set id

			// Elementals
			if (enchant.HasTag("/elemental/"))
			{
				optionSetId = enchant.MetaData1.GetInt("ENELEM");
			}
			// Enchants
			else if (enchant.MetaData1.Has("ENPFIX") || enchant.MetaData1.Has("ENSFIX"))
			{
				var prefixId = enchant.MetaData1.GetInt("ENPFIX");
				var suffixId = enchant.MetaData1.GetInt("ENSFIX");

				if (prefixId != 0)
				{
					optionSetId = prefixId;
					prefix = true;
				}
				else if (suffixId != 0)
				{
					optionSetId = suffixId;
					suffix = true;
				}
			}
			// Fallback? (Pages)
			else
			{
				var prefixId = enchant.OptionInfo.Prefix;
				var suffixId = enchant.OptionInfo.Suffix;

				if (prefixId != 0)
				{
					optionSetId = prefixId;
					prefix = true;
				}
				else if (suffixId != 0)
				{
					optionSetId = suffixId;
					suffix = true;
				}
			}

			// Get and apply option set
			var optionSetData = AuraData.OptionSetDb.Find(optionSetId);
			if (optionSetData == null)
			{
				Log.Error("HiddenEnchant.Complete: Unknown option set '{0}'.", optionSetId);
				goto L_End;
			}

			// Check target
			if (!item.HasTag(optionSetData.Allow) || item.HasTag(optionSetData.Disallow))
			{
				Log.Warning("HiddenEnchant.Complete: Creature '{0:X16}' tried to use set '{0}' on invalid item '{1}'.", optionSetData.Id, item.Info.Id);
				goto L_End;
			}

			// Check success
			var success = optionSetData.AlwaysSuccess;
			if (!success)
			{
				var rnd = RandomProvider.Get();
				var num = rnd.Next(100);
				var chance = this.GetChance(creature, rightHand, skill, optionSetData);
				success = num < chance;
			}

			// Handle result
			var result = EnchantResult.Fail;
			if (success)
			{
				item.ApplyOptionSet(optionSetData, true);
				if (prefix) item.OptionInfo.Prefix = (ushort)optionSetId;
				if (suffix) item.OptionInfo.Suffix = (ushort)optionSetId;

				result = EnchantResult.Success;
			}
			else
			{
				// Destroy enchant on fail if not using Enchant skill,
				// otherwise reduce durability
				if (skill.Info.Id == SkillId.HiddenEnchant)
				{
					// Decrement enchant
					//creature.Inventory.Decrement(enchant);
					Log.Debug("destroyed");
				}
				else
				{
					creature.Inventory.ReduceDurability(enchant, DurabilityReduction);
				}
			}

			Send.Effect(creature, Effect.Enchant, (byte)result);
			if (result == EnchantResult.Success)
			{
				Send.ItemUpdate(creature, item);
				Send.AcquireEnchantedItemInfo(creature, item.EntityId, item.Info.Id, optionSetId);
			}

		L_End:
			Send.Echo(creature, packet);
		}

		/// <summary>
		/// Returns success chance, based on skill, option set, and powder
		/// used.
		/// <remarks>
		/// Unofficial. It kinda matches the debug output of the client,
		/// but it is a little off.
		/// </remarks>
		/// </summary>
		/// <param name="creature"></param>
		/// <param name="rightHand"></param>
		/// <param name="skill"></param>
		/// <param name="optionSetData"></param>
		/// <returns></returns>
		private float GetChance(Creature creature, Item rightHand, Skill skill, OptionSetData optionSetData)
		{
			// Check right hand, only use it if it's powder
			if (rightHand != null && !rightHand.HasTag("/enchant/powder/"))
				rightHand = null;

			// Get base chance, based on skill and powder
			var baseChance = _baseChanceB00; // (Blessed) Magic Powder/None
			if (skill.Info.Id == SkillId.Enchant && rightHand != null)
			{
				if (rightHand.HasTag("/powder02/")) // Elite Magic Powder
					baseChance = _baseChanceB05;
				else if (rightHand.HasTag("/powder03/")) // Elven Magic Powder
					baseChance = _baseChanceB10;
				else if (rightHand.HasTag("/powder01/")) // Ancient Magic Powder
					baseChance = _baseChanceB50;
				else if (rightHand.HasTag("/powder04/") && rightHand.Info.Id == 85865) // Notorious Magic Powder
					baseChance = _baseChanceB60;
			}

			// Get chance
			var rank = Math2.Clamp(0, _baseChanceB00.Length - 1, (int)optionSetData.Rank - 1);
			var chance = baseChance[rank];
			var intBonus = 1f;
			var thursdayBonus = 0f;

			// Int bonus if using powder
			if (skill.Info.Id == SkillId.Enchant && rightHand != null)
				intBonus = 1f + ((creature.Int - 35f) / 350f);

			// Thursday bonus
			if (ErinnTime.Now.Day == 4)
				thursdayBonus = Math.Max(0, (15 - rank) / 2f);

			// Result
			var result = Math2.Clamp(0, 90, chance * intBonus + thursdayBonus);

			// Debug
			if (creature.Titles.SelectedTitle == TitleId.devCAT)
			{
				Send.ServerMessage(creature,
					"Debug: Enchant success chance: {0} (base: {1}, int: {2}, thu: {3})",
					result.ToInvariant("0"),
					chance.ToInvariant("0"),
					(chance / 1f * (intBonus - 1f)).ToInvariant("0"),
					thursdayBonus.ToInvariant("0"));
			}

			return result;
		}
	}
}
