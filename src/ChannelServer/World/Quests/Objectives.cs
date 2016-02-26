﻿// Copyright (c) Aura development team - Licensed under GNU GPL
// For more information, see license file in the main folder

using System.Linq;
using Aura.Data;
using Aura.Mabi.Const;
using Aura.Mabi;
using Aura.Channel.World.Entities;
using System;

namespace Aura.Channel.World.Quests
{
	public abstract class QuestObjective
	{
		public string Ident { get; set; }
		public string Description { get; set; }

		public int Amount { get; set; }

		public int RegionId { get; set; }
		public int X { get; set; }
		public int Y { get; set; }

		public MabiDictionary MetaData { get; protected set; }

		public abstract ObjectiveType Type { get; }

		protected QuestObjective(int amount)
		{
			this.MetaData = new MabiDictionary();
			this.Amount = amount;
		}
	}

	/// <summary>
	/// Objective to kill creatures of a race type.
	/// </summary>
	public class QuestObjectiveKill : QuestObjective
	{
		public override ObjectiveType Type { get { return ObjectiveType.Kill; } }

		public string[] RaceTypes { get; set; }

		public QuestObjectiveKill(int amount, params string[] raceTypes)
			: base(amount)
		{
			this.RaceTypes = raceTypes;

			this.MetaData.SetString("TGTSID", string.Join("|", raceTypes));
			this.MetaData.SetInt("TARGETCOUNT", amount);
			this.MetaData.SetShort("TGTCLS", 0);
		}

		/// <summary>
		/// Returns true if creature matches one of the race types.
		/// </summary>
		/// <param name="killedCreature"></param>
		/// <returns></returns>
		public bool Check(Creature killedCreature)
		{
			return this.RaceTypes.Any(type => killedCreature.RaceData.HasTag(type));
		}
	}

	/// <summary>
	/// Objective to collect a certain item.
	/// </summary>
	public class QuestObjectiveCollect : QuestObjective
	{
		public override ObjectiveType Type { get { return ObjectiveType.Collect; } }

		public int ItemId { get; set; }

		public QuestObjectiveCollect(int itemId, int amount)
			: base(amount)
		{
			this.ItemId = itemId;
			this.Amount = amount;

			this.MetaData.SetInt("TARGETITEM", itemId);
			this.MetaData.SetInt("TARGETCOUNT", amount);
			this.MetaData.SetInt("QO_FLAG", 1);
		}
	}

	/// <summary>
	/// Objective to talk to a specific NPC.
	/// </summary>
	public class QuestObjectiveTalk : QuestObjective
	{
		public override ObjectiveType Type { get { return ObjectiveType.Talk; } }

		public string Name { get; set; }

		public QuestObjectiveTalk(string npcName)
			: base(1)
		{
			this.Name = npcName;

			this.MetaData.SetString("TARGECHAR", npcName);
			this.MetaData.SetInt("TARGETCOUNT", 1);
		}
	}

	/// <summary>
	/// Objective to deliver something to a specific NPC.
	/// </summary>
	/// <remarks>
	/// The item is automatically given to the player on quest start,
	/// if this is the first quest objective.
	/// </remarks>
	public class QuestObjectiveDeliver : QuestObjective
	{
		public override ObjectiveType Type { get { return ObjectiveType.Deliver; } }

		public int ItemId { get; set; }
		public string NpcName { get; set; }

		public QuestObjectiveDeliver(int itemId, int amount, string npcName)
			: base(amount)
		{
			this.ItemId = itemId;
			this.NpcName = npcName;

			this.MetaData.SetString("TARGECHAR", this.NpcName);
			this.MetaData.SetInt("TARGETCOUNT", this.Amount);
			this.MetaData.SetInt("TARGETITEM", this.ItemId);
		}
	}

	/// <summary>
	/// Objective to reach a rank in a certain skill.
	/// </summary>
	public class QuestObjectiveReachRank : QuestObjective
	{
		public override ObjectiveType Type { get { return ObjectiveType.ReachRank; } }

		public SkillId Id { get; set; }
		public SkillRank Rank { get; set; }

		public QuestObjectiveReachRank(SkillId skillId, SkillRank rank)
			: base(1)
		{
			this.Id = skillId;
			this.Rank = rank;

			this.MetaData.SetUShort("TGTSKL", (ushort)skillId);
			this.MetaData.SetShort("TGTLVL", (short)rank);
			this.MetaData.SetInt("TARGETCOUNT", 1);
		}
	}

	/// <summary>
	/// Objective to reach a certain level.
	/// </summary>
	public class QuestObjectiveReachLevel : QuestObjective
	{
		public override ObjectiveType Type { get { return ObjectiveType.ReachLevel; } }

		public QuestObjectiveReachLevel(int level)
			: base(level)
		{
			this.MetaData.SetShort("TGTLVL", (short)level);
			this.MetaData.SetInt("TARGETCOUNT", 1);
		}
	}

	/// <summary>
	/// Objective to get a certain keyword.
	/// </summary>
	public class QuestObjectiveGetKeyword : QuestObjective
	{
		public override ObjectiveType Type { get { return ObjectiveType.GetKeyword; } }

		public int KeywordId { get; private set; }

		public QuestObjectiveGetKeyword(string keyword)
			: base(1)
		{
			var keywordData = AuraData.KeywordDb.Find(keyword);
			if (keywordData == null)
				throw new ArgumentException("Keyword '" + keyword + "' not found.");

			this.KeywordId = keywordData.Id;
			this.MetaData.SetInt("TGTKEYWORD", this.KeywordId); // Unofficial
			this.MetaData.SetInt("TARGETCOUNT", 1);
		}

		public QuestObjectiveGetKeyword(int keywordId)
			: base(1)
		{
			this.KeywordId = keywordId;
			this.MetaData.SetInt("TGTKEYWORD", this.KeywordId); // Unofficial
			this.MetaData.SetInt("TARGETCOUNT", 1);
		}
	}

	/// <summary>
	/// Objective to equip a certain type of item.
	/// </summary>
	public class QuestObjectiveEquip : QuestObjective
	{
		public override ObjectiveType Type { get { return ObjectiveType.Equip; } }

		public string Tag { get; private set; }

		public QuestObjectiveEquip(string tag)
			: base(1)
		{
			this.Tag = tag;
			this.MetaData.SetString("TGTSID", this.Tag);
			this.MetaData.SetInt("TARGETCOUNT", 1);
		}
	}

	/// <summary>
	/// Objective to gather a specific item.
	/// </summary>
	public class QuestObjectiveGather : QuestObjective
	{
		public override ObjectiveType Type { get { return ObjectiveType.Gather; } }

		public int ItemId { get; private set; }

		public QuestObjectiveGather(int itemId, int amount)
			: base(amount)
		{
			this.ItemId = itemId;
			this.MetaData.SetInt("TARGETITEM", this.ItemId);
			//this.MetaData.SetString("TGTSID", "/Gathering_Knife/"); // Tool to use (ignored?)
			this.MetaData.SetInt("TARGETCOUNT", this.Amount);
		}
	}

	/// <summary>
	/// Objective to use a certain skill.
	/// </summary>
	public class QuestObjectiveUseSkill : QuestObjective
	{
		public override ObjectiveType Type { get { return ObjectiveType.UseSkill; } }

		public SkillId Id { get; set; }

		public QuestObjectiveUseSkill(SkillId skillId)
			: base(1)
		{
			this.Id = skillId;

			this.MetaData.SetUShort("TGTSKL", (ushort)skillId);
			this.MetaData.SetInt("TARGETCOUNT", 1);
		}
	}

	/// <summary>
	/// Objective to clear a certain dungeon.
	/// </summary>
	public class QuestObjectiveClearDungeon : QuestObjective
	{
		public override ObjectiveType Type { get { return ObjectiveType.ClearDungeon; } }

		public string DungeonName { get; set; }

		public QuestObjectiveClearDungeon(string dungeonName)
			: base(1)
		{
			this.DungeonName = dungeonName;

			this.MetaData.SetInt("TARGETCOUNT", 1);
			this.MetaData.SetString("TGTCLS", dungeonName);
		}
	}
}
