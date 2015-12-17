﻿// Copyright (c) Aura development team - Licensed under GNU GPL
// For more information, see license file in the main folder

using Aura.Channel.Network;
using Aura.Channel.Network.Sending;
using Aura.Channel.World.Entities;
using Aura.Channel.World.Entities.Creatures;
using Aura.Channel.World.Weather;
using Aura.Data;
using Aura.Data.Database;
using Aura.Shared;
using Aura.Shared.Database;
using Aura.Mabi;
using Aura.Mabi.Const;
using Aura.Shared.Network;
using Aura.Shared.Util;
using Aura.Shared.Util.Commands;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Aura.Mabi.Network;
using Aura.Channel.World;
using System.Text.RegularExpressions;
using System.Threading;

namespace Aura.Channel.Util
{
    public class GmCommandManager : CommandManager<GmCommand, GmCommandFunc>
    {
        public GmCommandManager()
        {
            // Players
            Add(00, 50, "where", "", HandleWhere);
            Add(00, 50, "cp", "", HandleCp);
            Add(00, 50, "distance", "", HandleDistance);
            Add(00, 50, "partysize", "<size>", HandlePartySize);

            // VIPs
            Add(01, 50, "go", "<location>", HandleGo);
            Add(01, 50, "iteminfo", "<name>", HandleItemInfo);
            Add(01, 50, "skillinfo", "<name>", HandleSkillInfo);
            Add(01, 50, "raceinfo", "<name>", HandleRaceInfo);
            Add(01, 50, "height", "<height>", HandleBody);
            Add(01, 50, "weight", "<weight>", HandleBody);
            Add(01, 50, "upper", "<upper>", HandleBody);
            Add(01, 50, "lower", "<lower>", HandleBody);
            Add(01, 50, "haircolor", "<hex color>", HandleHairColor);
            Add(01, 50, "die", "", HandleDie);
            Add(01, 50, "who", "", HandleWho);
            Add(01, 50, "motion", "<category> <motion>", HandleMotion);
            Add(01, 50, "gesture", "<gesture>", HandleGesture);
            Add(01, 50, "lasttown", "", HandleLastTown);
            Add(01, 50, "cutscene", "<name>", HandleCutscene);
            Add(01, 50, "openshop", "<name>", HandleOpenShop);
            Add(01, 50, "nccolor", "<id (0-32)>", HandleNameChatColor);

            // GMs
            Add(50, 50, "warp", "<region> [x] [y]", HandleWarp);
            Add(50, 50, "jump", "[x] [y]", HandleJump);
            Add(50, 50, "item", "<id|name> [amount|color1 [color2 [color 3]]]", HandleItem);
            Add(50, 50, "ego", "<item id> <ego name> <ego race> [color1 [color2 [color 3]]]", HandleEgo);
            Add(50, 50, "skill", "<id> [rank]", HandleSkill);
            Add(50, 50, "title", "<id>", HandleTitle);
            Add(50, 50, "speed", "[increase]", HandleSpeed);
            Add(50, 50, "spawn", "<race> [amount [title]]", HandleSpawn);
            Add(50, 50, "ap", "<amount>", HandleAp);
            Add(50, -1, "gmcp", "", HandleGmcp);
            Add(50, 50, "card", "<id>", HandleCard);
            Add(50, 50, "petcard", "<race>", HandleCard);
            Add(50, 50, "heal", "", HandleHeal);
            Add(50, 50, "clean", "", HandleClean);
            Add(50, 50, "condition", "[a] [b] [c] [d] [e]", HandleCondition);
            Add(50, 50, "effect", "<id> [(b|i|s:parameter)|me]", HandleEffect);
            Add(50, 50, "prop", "<id>", HandleProp);
            Add(50, 50, "msg", "<message>", HandleMsg);
            Add(50, 50, "broadcast", "<message>", HandleBroadcast);
            Add(50, 50, "allskills", "", HandleAllSkills);
            Add(50, 50, "alltitles", "", HandleAllTitles);
            Add(50, 50, "gold", "<amount>", HandleGold);
            Add(50, 50, "favor", "<npc name> [amount]", HandleFavor);
            Add(50, 50, "stress", "<npc name> [amount]", HandleStress);
            Add(50, 50, "memory", "<npc name> [amount]", HandleMemory);
            Add(50, 50, "weather", "[0.0~2.0|clear|rain|storm|type1~type12]", HandleWeather);
            Add(50, 50, "telewalk", "", HandleTeleWalk);
            Add(50, 50, "points", "<modificator>", HandlePoints);

            // Admins
            Add(99, 99, "dynamic", "[variant]", HandleDynamic);
            Add(99, 99, "dungeon", "<dungeon name>", HandleDungeon);
            Add(99, -1, "reloaddata", "", HandleReloadData);
            Add(99, -1, "reloadscripts", "", HandleReloadScripts);
            Add(99, -1, "reloadconf", "", HandleReloadConf);
            Add(99, 99, "closenpc", "", HandleCloseNpc);
            Add(99, 99, "shutdown", "<time>", HandleShutdown);

            // Aliases
            AddAlias("item", "drop");
            AddAlias("iteminfo", "ii");
            AddAlias("skillinfo", "si");
            AddAlias("raceinfo", "ri");
            AddAlias("msg", "m");
            AddAlias("broadcast", "bc");
        }

        // ------------------------------------------------------------------

        /// <summary>
        /// Adds new command.
        /// </summary>
        /// <param name="auth"></param>
        /// <param name="name"></param>
        /// <param name="usage"></param>
        /// <param name="func"></param>
        public void Add(int auth, string name, string usage, GmCommandFunc func)
        {
            this.Add(auth, 100, name, usage, func);
        }

        /// <summary>
        /// Adds new command.
        /// </summary>
        /// <param name="auth"></param>
        /// <param name="charAuth"></param>
        /// <param name="name"></param>
        /// <param name="usage"></param>
        /// <param name="func"></param>
        public void Add(int auth, int charAuth, string name, string usage, GmCommandFunc func)
        {
            this.Add(new GmCommand(auth, charAuth, name, usage, func));
        }

        /// <summary>
        /// Adds alias for command.
        /// </summary>
        /// <param name="commandName"></param>
        /// <param name="alias"></param>
        public void AddAlias(string commandName, string alias)
        {
            var command = this.GetCommand(commandName);
            if (command == null)
                throw new Exception("Aliasing: Command '" + commandName + "' not found");

            _commands[alias] = command;
        }

        /// <summary>
        /// Tries to run command, based on message.
        /// Returns false if the message was of no interest to the
        /// command handler.
        /// </summary>
        /// <param name="client"></param>
        /// <param name="creature"></param>
        /// <param name="message"></param>
        /// <returns></returns>
        public bool Process(ChannelClient client, Creature creature, string message)
        {
            if (message.Length < 2 || !message.StartsWith(ChannelServer.Instance.Conf.Commands.Prefix.ToString(CultureInfo.InvariantCulture)))
                return false;

            // Parse arguments
            var args = this.ParseLine(message);
            args[0] = args[0].TrimStart(ChannelServer.Instance.Conf.Commands.Prefix);

            var sender = creature;
            var target = creature;
            var isCharCommand = message.StartsWith(ChannelServer.Instance.Conf.Commands.Prefix2);

            // Handle char commands
            if (isCharCommand)
            {
                // Get target player
                if (args.Count < 2 || (target = ChannelServer.Instance.World.GetPlayer(args[1])) == null)
                {
                    Send.ServerMessage(creature, Localization.Get("Target not found."));
                    return true;
                }

                // Remove target name from the args
                args.RemoveAt(1);
            }

            // Get command
            var command = this.GetCommand(args[0]);
            if (command == null)
            {
                // Don't send invalid command message because it'll interfere with
                // 4chan-greentext style ">lol"

                //Send.ServerMessage(creature, Localization.Get("Unknown command '{0}'."), args[0]);
                return false;
            }

            var commandConf = ChannelServer.Instance.Conf.Commands.GetAuth(command.Name, command.Auth, command.CharAuth);

            // Check auth
            if ((!isCharCommand && client.Account.Authority < commandConf.Auth) || (isCharCommand && client.Account.Authority < commandConf.CharAuth))
            {
                Send.ServerMessage(creature, Localization.Get("You're not authorized to use '{0}'."), args[0]);
                return true;
            }

            if (isCharCommand && commandConf.CharAuth < 0)
            {
                Send.ServerMessage(creature, Localization.Get("Command '{0}' cannot be used on another character."), args[0]);
                return true;
            }

            // Run
            var result = command.Func(client, sender, target, message, args);

            // Handle result
            if (result == CommandResult.InvalidArgument)
            {
                Send.ServerMessage(creature, Localization.Get("Usage: {0} {1}"), command.Name, command.Usage);
                if (command.CharAuth <= client.Account.Authority && command.CharAuth > 0)
                    Send.ServerMessage(creature, Localization.Get("Usage: {0} <target> {1}"), command.Name, command.Usage);

                return true;
            }

            if (result == CommandResult.Fail)
            {
                Send.ServerMessage(creature, "Failed to process command.");
                return true;
            }

            return true;
        }

        // ------------------------------------------------------------------

        private CommandResult HandleWhere(ChannelClient client, Creature sender, Creature target, string message, IList<string> args)
        {
            var pos = target.GetPosition();
            var msg = sender == target
                ? Localization.Get("You're here: Region: {0} @ {1}/{2}, Area: {5}, Dir: {4} (Radian: {6})")
                : Localization.Get("{3} is here: Region: {0} @ {1}/{2}, Area: {5}, Dir: {4} (Radian: {6})");

            var areaId = target.Region.GetAreaId(pos.X, pos.Y);

            Send.ServerMessage(sender, msg, target.RegionId, pos.X, pos.Y, target.Name, target.Direction, areaId, MabiMath.ByteToRadian(target.Direction).ToInvariant("#.###"));

            return CommandResult.Okay;
        }

        private CommandResult HandleWarp(ChannelClient client, Creature sender, Creature target, string message, IList<string> args)
        {
            if (args.Count < 2)
                return CommandResult.InvalidArgument;

            int regionId = 0;
            int x = -1, y = -1;

            // Warp to path
            if (args.Count == 2 && !args[1].Contains("r:") && !Regex.IsMatch(args[1], "^[0-9]+$"))
            {
                try
                {
                    var loc = new Location(args[1]);
                    regionId = loc.RegionId;
                    x = loc.X;
                    y = loc.Y;
                }
                catch (Exception ex)
                {
                    Send.ServerMessage(sender, "Error: {0}", ex.Message);
                    return CommandResult.Fail;
                }
            }
            // Warp/Jump to coordinates
            else
            {
                if (!int.TryParse(args[1].Replace("r:", ""), out regionId))
                {
                    Send.ServerMessage(sender, Localization.Get("Invalid region id."));
                    return CommandResult.InvalidArgument;
                }

                // Check region
                var warpToRegion = ChannelServer.Instance.World.GetRegion(regionId);
                if (warpToRegion == null)
                {
                    Send.ServerMessage(sender, Localization.Get("Region doesn't exist."));
                    return CommandResult.Fail;
                }

                var targetPos = target.GetPosition();

                // Parse X
                if (args.Count > 2)
                {
                    if (args[2].ToLower() == "x")
                        x = targetPos.X;
                    else if (!int.TryParse(args[2].Replace("x:", ""), out x))
                    {
                        Send.ServerMessage(sender, Localization.Get("Invalid X coordinate."));
                        return CommandResult.InvalidArgument;
                    }
                }

                // Parse Y
                if (args.Count > 3)
                {
                    if (args[3].ToLower() == "y")
                        y = targetPos.Y;
                    else if (!int.TryParse(args[3].Replace("y:", ""), out y))
                    {
                        Send.ServerMessage(sender, Localization.Get("Invalid Y coordinate."));
                        return CommandResult.InvalidArgument;
                    }
                }

                // Randomize coordinates
                if (x == -1 || y == -1)
                {
                    var rndc = warpToRegion.Data.RandomCoord(RandomProvider.Get());
                    if (x < 0) x = rndc.X;
                    if (y < 0) y = rndc.Y;
                }
            }

            target.Warp(regionId, x, y);

            Send.ServerMessage(sender, Localization.Get("Warped to {0}@{1}/{2}"), regionId, x, y);
            if (sender != target)
                Send.ServerMessage(target, Localization.Get("You've been warped by '{0}'."), sender.Name);

            return CommandResult.Okay;
        }

        private CommandResult HandleJump(ChannelClient client, Creature sender, Creature target, string message, IList<string> args)
        {
            int regionId = target.RegionId;
            int x = -1, y = -1;

            // Split args like "123,321", our common spawner coord format
            int index = -1;
            if (args.Count == 2 && (index = args[1].IndexOf(',')) != -1)
            {
                args.Add(args[1].Substring(index + 1, args[1].Length - index - 1));
                args[1] = args[1].Substring(0, index);
            }

            // Parse X
            if (args.Count > 1 && !int.TryParse(args[1].Trim(','), out x))
            {
                Send.ServerMessage(sender, Localization.Get("Invalid X coordinate."));
                return CommandResult.InvalidArgument;
            }

            // Parse Y
            if (args.Count > 2 && !int.TryParse(args[2].Trim(','), out y))
            {
                Send.ServerMessage(sender, Localization.Get("Invalid Y coordinate."));
                return CommandResult.InvalidArgument;
            }

            // Get random coordinates in region if none were provided
            if (x == -1 || y == -1)
            {
                var rndc = AuraData.RegionInfoDb.RandomCoord(regionId);
                if (x < 0) x = rndc.X;
                if (y < 0) y = rndc.Y;
            }

            target.Warp(regionId, x, y);

            Send.ServerMessage(sender, Localization.Get("Jumped to {0}/{1}"), x, y);
            if (sender != target)
                Send.ServerMessage(target, Localization.Get("You've been warped by '{0}'."), sender.Name);

            return CommandResult.Okay;
        }

        private CommandResult HandleGo(ChannelClient client, Creature sender, Creature target, string message, IList<string> args)
        {
            if (args.Count < 2)
            {
                Send.ServerMessage(sender,
                    Localization.Get("Destinations:") +
                    " Tir Chonaill, Dugald Isle, Dunbarton, Gairech, Bangor, Emain Macha, Taillteann, Tara, Cobh, Ceo Island, Nekojima, GM Island"
                );
                return CommandResult.InvalidArgument;
            }

            int regionId = -1, x = -1, y = -1;
            var destination = args[1].ToLower();

            // Determine where to warp to
            if (destination.StartsWith("tir")) { regionId = 1; x = 12801; y = 38397; }
            else if (destination.StartsWith("dugald")) { regionId = 16; x = 23017; y = 61244; }
            else if (destination.StartsWith("dun")) { regionId = 14; x = 38001; y = 38802; }
            else if (destination.StartsWith("gairech")) { regionId = 30; x = 39295; y = 53279; }
            else if (destination.StartsWith("bangor")) { regionId = 31; x = 12904; y = 12200; }
            else if (destination.StartsWith("emain")) { regionId = 52; x = 39818; y = 41621; }
            else if (destination.StartsWith("tail")) { regionId = 300; x = 212749; y = 192720; }
            else if (destination.StartsWith("tara")) { regionId = 401; x = 99793; y = 91209; }
            else if (destination.StartsWith("cobh")) { regionId = 23; x = 28559; y = 37693; }
            else if (destination.StartsWith("ceo")) { regionId = 56; x = 8987; y = 9611; }
            else if (destination.StartsWith("neko")) { regionId = 600; x = 114430; y = 79085; }
            else if (destination.StartsWith("gm")) { regionId = 22; x = 2500; y = 2500; }
            else
            {
                Send.ServerMessage(sender, Localization.Get("Unkown destination"), args[1]);
                return CommandResult.InvalidArgument;
            }

            // Shouldn't happen unless someone made a mistake above
            if (regionId == -1 || x == -1 || y == -1)
            {
                Send.ServerMessage(sender, Localization.Get("Error while choosing destination."));
                Log.Error("HandleGo: Incomplete destination '{0}'.", args[1]);
                return CommandResult.Fail;
            }

            target.Warp(regionId, x, y);

            Send.ServerMessage(sender, Localization.Get("Warped to {0}@{1}/{2}"), regionId, x, y);
            if (sender != target)
                Send.ServerMessage(target, Localization.Get("You've been warped by '{0}'."), sender.Name);

            return CommandResult.Okay;
        }

        private CommandResult HandleItem(ChannelClient client, Creature sender, Creature target, string message, IList<string> args)
        {
            if (args.Count < 2)
                return CommandResult.InvalidArgument;

            var drop = (args[0] == "drop");
            int itemId = 0;
            ItemData itemData = null;

            // Get item data
            if (!int.TryParse(args[1], out itemId))
            {
                var all = AuraData.ItemDb.FindAll(args[1]);

                // One or multiple items found
                if (all.Count > 0)
                {
                    // Find best result
                    var score = 10000;
                    foreach (var data in all)
                    {
                        var curScore = data.Name.LevenshteinDistance(args[1], false);
                        if (curScore < score)
                        {
                            score = curScore;
                            itemData = data;
                        }

                        if (score == 0)
                            break;
                    }

                    if (all.Count > 1 && score != 0)
                    {
                        var perc = 100 - (100f / itemData.Name.Length * score);
                        Send.ServerMessage(sender, Localization.Get("No exact match found for '{0}', using best result, '{1}' ({2}%)."), args[1], itemData.Name, perc.ToString("0.0", CultureInfo.InvariantCulture));
                    }
                }
            }
            else
            {
                itemData = AuraData.ItemDb.Find(itemId);
            }

            // Check item data
            if (itemData == null)
            {
                Send.ServerMessage(sender, Localization.Get("Item '{0}' not found in database."), args[1]);
                return CommandResult.Fail;
            }

            // Check for egos
            if (itemData.HasTag("/ego_weapon/"))
            {
                Send.ServerMessage(sender, Localization.Get("Egos can't be created with 'item', use 'ego' instead."));
                return CommandResult.Fail;
            }

            var item = new Item(itemData.Id);

            // Check amount for stackable items
            if (itemData.StackType == StackType.Stackable && args.Count > 2)
            {
                int amount;

                // Get amount
                if (!int.TryParse(args[2], out amount) || amount <= 0)
                {
                    Send.ServerMessage(sender, Localization.Get("Invalid amount."));
                    return CommandResult.Fail;
                }

                item.Amount = amount;
            }
            else if (item.Info.Id == 2004 && args.Count > 2) // Check
            {
                int amount;

                // Get amount
                if (!int.TryParse(args[2], out amount) || amount <= 0)
                {
                    Send.ServerMessage(sender, Localization.Get("Invalid amount."));
                    return CommandResult.Fail;
                }

                item.MetaData1.SetInt("EVALUE", Math.Min(5000000, amount));
            }
            // Parse colors
            else if (itemData.StackType != StackType.Stackable && args.Count > 2)
            {
                uint color1, color2, color3;
                if (!TryParseColorsFromArgs(args, 2, out color1, out color2, out color3))
                {
                    Send.ServerMessage(sender, Localization.Get("Invalid or unknown color."));
                    return CommandResult.InvalidArgument;
                }

                item.Info.Color1 = color1;
                item.Info.Color2 = color2;
                item.Info.Color3 = color3;
            }

            // Create new pockets for bags
            if (item.Data.HasTag("/pouch/bag/") && !drop)
            {
                if (item.Data.BagWidth == 0)
                {
                    Send.ServerMessage(sender, Localization.Get("Beware, shaped bags aren't supported yet."));
                }
                else if (!target.Inventory.AddBagPocket(item))
                {
                    // TODO: Handle somehow? Without linked pocket the bag
                    //   won't open.
                }
            }

            // Spawn item
            var success = true;
            if (!drop)
                success = target.Inventory.Add(item, Pocket.Temporary);
            else
                item.Drop(target.Region, target.GetPosition());

            if (success)
            {
                if (sender != target)
                    Send.ServerMessage(target, Localization.Get("Item '{0}' has been spawned by '{1}'."), itemData.Name, sender.Name);
                Send.ServerMessage(sender, Localization.Get("Item '{0}' has been spawned."), itemData.Name);
                return CommandResult.Okay;
            }
            else
            {
                Send.ServerMessage(sender, Localization.Get("Failed to spawn item."));
                return CommandResult.Fail;
            }
        }

        private CommandResult HandleDynamic(ChannelClient client, Creature sender, Creature target, string message, IList<string> args)
        {
            // Get variant
            var variant = "";
            if (args.Count > 1)
            {
                variant = args[1];

                if (!variant.EndsWith(".xml"))
                    variant += ".xml";
            }

            // Get region info
            var baseRegionId = target.RegionId;
            var regionData = AuraData.RegionDb.Find(baseRegionId);

            if (regionData == null)
                return CommandResult.Fail;

            // Create region
            var region = new DynamicRegion(baseRegionId, variant, RegionMode.Permanent);
            ChannelServer.Instance.World.AddRegion(region);

            //Warp
            target.Warp(region.Id, target.GetPosition());

            Send.ServerMessage(sender, Localization.Get("Created new region based on region {0}, new region's id: {1}"), baseRegionId, region.Id);
            if (sender != target)
                Send.ServerMessage(target, Localization.Get("'{0}' warped you to a new, dynamic region."), sender.Name);

            return CommandResult.Okay;
        }

        private CommandResult HandleItemInfo(ChannelClient client, Creature sender, Creature target, string message, IList<string> args)
        {
            if (args.Count < 2)
                return CommandResult.InvalidArgument;

            var search = message.Substring(message.IndexOf(" ")).Trim();
            var items = AuraData.ItemDb.FindAll(search);
            if (items.Count == 0)
            {
                Send.ServerMessage(target, Localization.Get("No items found for '{0}'."), search);
                return CommandResult.Okay;
            }

            var eItems = items.OrderBy(a => a.Name.LevenshteinDistance(search)).ThenBy(a => a.Id).GetEnumerator();
            var max = 20;
            for (int i = 0; eItems.MoveNext() && i < max; ++i)
            {
                var item = eItems.Current;
                Send.ServerMessage(target, Localization.Get("{0}: {1}, Type: {2}"), item.Id, item.Name, item.Type);
            }

            Send.ServerMessage(target, Localization.Get("Results: {0} (Max. {1} shown)"), items.Count, max);

            return CommandResult.Okay;
        }

        private CommandResult HandleSkillInfo(ChannelClient client, Creature sender, Creature target, string message, IList<string> args)
        {
            if (args.Count < 2)
                return CommandResult.InvalidArgument;

            var search = message.Substring(message.IndexOf(" ")).Trim();
            var items = AuraData.SkillDb.FindAll(search);
            if (items.Count == 0)
            {
                Send.ServerMessage(target, Localization.Get("No skills found for '{0}'."), search);
                return CommandResult.Okay;
            }

            var eItems = items.OrderBy(a => a.Name.LevenshteinDistance(search)).ThenBy(a => a.Id).GetEnumerator();
            var max = 20;
            for (int i = 0; eItems.MoveNext() && i < max; ++i)
            {
                var item = eItems.Current;
                Send.ServerMessage(target, Localization.Get("{0}: {1}"), item.Id, item.Name);
            }

            Send.ServerMessage(target, Localization.Get("Results: {0} (Max. {1} shown)"), items.Count, max);

            return CommandResult.Okay;
        }

        private CommandResult HandleRaceInfo(ChannelClient client, Creature sender, Creature target, string message, IList<string> args)
        {
            if (args.Count < 2)
                return CommandResult.InvalidArgument;

            var search = message.Substring(message.IndexOf(" ")).Trim();
            var items = AuraData.RaceDb.FindAll(search);
            if (items.Count == 0)
            {
                Send.ServerMessage(target, Localization.Get("No races found for '{0}'."), search);
                return CommandResult.Okay;
            }

            var eItems = items.OrderBy(a => a.Name.LevenshteinDistance(search)).ThenBy(a => a.Id).GetEnumerator();
            var max = 20;
            for (int i = 0; eItems.MoveNext() && i < max; ++i)
            {
                var item = eItems.Current;
                Send.ServerMessage(target, Localization.Get("{0}: {1}"), item.Id, item.Name);
            }

            Send.ServerMessage(target, Localization.Get("Results: {0} (Max. {1} shown)"), items.Count, max);

            return CommandResult.Okay;
        }

        private CommandResult HandleSkill(ChannelClient client, Creature sender, Creature target, string message, IList<string> args)
        {
            if (args.Count < 2)
                return CommandResult.InvalidArgument;

            // Get skill id
            int skillId;
            if (!int.TryParse(args[1], out skillId))
            {
                // Try skill id enum if arg wasn't numeric
                SkillId skillIdE;
                if (!Enum.TryParse(args[1], out skillIdE))
                {
                    Send.ServerMessage(target, Localization.Get("Unknown skill id '{0}'."), args[1]);
                    return CommandResult.InvalidArgument;
                }

                skillId = (int)skillIdE;
            }

            // Check skill data
            var skillData = AuraData.SkillDb.Find(skillId);
            if (skillData == null)
            {
                Send.ServerMessage(sender, Localization.Get("Skill '{0}' not found in database."), args[1]);
                return CommandResult.Fail;
            }

            // Get rank
            int rank = 0;
            if (args.Count > 2 && args[2] != "novice" && !int.TryParse(args[2], NumberStyles.HexNumber, null, out rank))
                return CommandResult.InvalidArgument;

            if (rank > 0)
                rank = Math2.Clamp(0, 18, 16 - rank);

            // Check rank data
            var rankData = skillData.GetRankData(rank, target.RaceId);
            if (rankData == null)
            {
                Send.ServerMessage(sender, Localization.Get("Skill '{0}' doesn't have rank '{1}'."), args[1], (SkillRank)rank);
                return CommandResult.Fail;
            }

            // Give skill
            target.Skills.Give((SkillId)skillId, (SkillRank)rank);

            Send.ServerMessage(sender, Localization.Get("Skill '{0}' added."), (SkillId)skillId);
            if (target != sender)
                Send.ServerMessage(sender, Localization.Get("Skill '{0}' added by '{1}'."), (SkillId)skillId, sender.Name);

            return CommandResult.Okay;
        }

        private CommandResult HandleBody(ChannelClient client, Creature sender, Creature target, string message, IList<string> args)
        {
            if (args.Count < 2)
                return CommandResult.InvalidArgument;

            // Get value
            float val;
            if (!float.TryParse(args[1], NumberStyles.Float, CultureInfo.InvariantCulture, out val))
                return CommandResult.InvalidArgument;

            // Change value based on command
            switch (args[0])
            {
                case "height": target.Height = val; val = target.Height; break;
                case "weight": target.Weight = val; val = target.Weight; break;
                case "upper": target.Upper = val; val = target.Upper; break;
                case "lower": target.Lower = val; val = target.Lower; break;
            }

            Send.CreatureBodyUpdate(target);

            Send.ServerMessage(sender, Localization.Get("Change successful, new value: {0}"), val.ToInvariant("0.0"));
            if (sender != target)
                Send.ServerMessage(target, Localization.Get("Your appearance has been changed by {0}."), sender.Name);

            return CommandResult.Okay;
        }

        private CommandResult HandleCp(ChannelClient client, Creature sender, Creature target, string message, IList<string> args)
        {
            if (sender == target)
                Send.ServerMessage(sender, Localization.Get("Your combat power: {0}"), target.CombatPower.ToInvariant("0.0"));
            else
                Send.ServerMessage(sender, Localization.Get("{0}'s combat power: {1}"), target.Name, target.CombatPower.ToInvariant("0.0"));

            return CommandResult.Okay;
        }

        private CommandResult HandleHairColor(ChannelClient client, Creature sender, Creature target, string message, IList<string> args)
        {
            if (args.Count < 2)
                return CommandResult.InvalidArgument;

            uint color;
            // Hex color
            if (args[1].StartsWith("0x"))
            {
                if (!uint.TryParse(args[1].Replace("0x", ""), NumberStyles.HexNumber, null, out color))
                    return CommandResult.InvalidArgument;
            }
            // Mabi color
            else if (uint.TryParse(args[1], out color) && color <= 0xFF)
            {
                color += 0x10000000;
            }
            else
            {
                switch (args[1])
                {
                    case "saiyan": color = 0x60001312; break;
                    default:
                        return CommandResult.InvalidArgument;
                }
            }

            // Get hair item
            var hair = target.Inventory.GetItemAt(Pocket.Hair, 0, 0);
            if (hair == null)
                return CommandResult.Fail;

            // Change color
            hair.Info.Color1 = color;
            Send.EquipmentChanged(target, hair);

            Send.ServerMessage(sender, Localization.Get("Change successful, new value: {0}"), "0x" + color.ToString("X8"));
            if (sender != target)
                Send.ServerMessage(target, Localization.Get("Your appearance has been changed by {0}."), sender.Name);

            return CommandResult.Okay;
        }

        private CommandResult HandleTitle(ChannelClient client, Creature sender, Creature target, string message, IList<string> args)
        {
            if (args.Count < 2)
                return CommandResult.InvalidArgument;

            // Get title id
            ushort titleId;
            if (!ushort.TryParse(args[1], out titleId))
                return CommandResult.InvalidArgument;

            // Enable title
            target.Titles.Enable(titleId);

            Send.ServerMessage(sender, Localization.Get("Added title."));
            if (sender != target)
                Send.ServerMessage(target, Localization.Get("{0} enabled a title for you."), sender.Name);

            return CommandResult.Okay;
        }

        private CommandResult HandleSpeed(ChannelClient client, Creature sender, Creature target, string message, IList<string> args)
        {
            short speed = 0;
            if (args.Count > 1 && !short.TryParse(args[1], out speed))
                return CommandResult.InvalidArgument;

            speed = (short)Math2.Clamp(0, 1000, speed);

            if (speed == 0)
                target.Conditions.Deactivate(ConditionsC.Hurry);
            else
            {
                var extra = new MabiDictionary();
                extra.SetShort("VAL", speed);
                target.Conditions.Activate(ConditionsC.Hurry, extra);
            }

            Send.ServerMessage(sender, Localization.Get("Speed changed to +{0}%."), speed);
            if (sender != target)
                Send.ServerMessage(target, Localization.Get("Your speed has been changed to +{0}% by {1}."), speed, sender.Name);

            return CommandResult.Okay;
        }

        private CommandResult HandleSpawn(ChannelClient client, Creature sender, Creature target, string message, IList<string> args)
        {
            if (args.Count < 2)
                return CommandResult.InvalidArgument;

            // Get race id
            int raceId;
            if (!int.TryParse(args[1], out raceId))
                return CommandResult.InvalidArgument;

            // Check race
            if (!AuraData.RaceDb.Exists(raceId))
            {
                Send.ServerMessage(sender, Localization.Get("Race '{0}' doesn't exist."), raceId);
                return CommandResult.Fail;
            }

            // Get amount
            int amount = 1;
            if (args.Count > 2 && !int.TryParse(args[2], out amount))
                return CommandResult.InvalidArgument;

            // Get title
            ushort titleId = 30011;
            if (args.Count > 3 && !ushort.TryParse(args[3], out titleId))
                return CommandResult.InvalidArgument;

            // Spawn creatures in a spiral
            var targetPos = target.GetPosition();
            for (int i = 0; i < amount; ++i)
            {
                var x = (int)(targetPos.X + Math.Sin(i) * i * 20);
                var y = (int)(targetPos.Y + Math.Cos(i) * i * 20);

                var creature = ChannelServer.Instance.World.SpawnManager.Spawn(raceId, target.RegionId, x, y, true, true);

                if (titleId != 0)
                {
                    creature.Titles.Enable(titleId);
                    creature.Titles.ChangeTitle(titleId, false);
                }
            }

            Send.ServerMessage(sender, Localization.Get("Creatures spawned."));
            if (target != sender)
                Send.ServerMessage(target, Localization.Get("{0} spawned creatures around you."), sender.Name);

            return CommandResult.Okay;
        }

        private CommandResult HandleDie(ChannelClient client, Creature sender, Creature target, string message, IList<string> args)
        {
            target.Kill(sender);

            //Send.PlayDead(target);

            Send.ServerMessage(sender, Localization.Get("Game over."));
            if (target != sender)
                Send.ServerMessage(target, Localization.Get("You've been killed by {0}."), sender.Name);

            return CommandResult.Okay;
        }

        private CommandResult HandleReloadData(ChannelClient client, Creature sender, Creature target, string message, IList<string> args)
        {
            Send.ServerMessage(sender, Localization.Get("Reloading, this might take a moment."));
            ChannelServer.Instance.LoadData(DataLoad.ChannelServer, true);
            Send.ServerMessage(sender, Localization.Get("Reload complete."));

            return CommandResult.Okay;
        }

        private CommandResult HandleReloadScripts(ChannelClient client, Creature sender, Creature target, string message, IList<string> args)
        {
            Send.ServerMessage(sender, Localization.Get("Beware, reloading should only be used during development, it's not guaranteed to be safe."));
            Send.ServerMessage(sender, Localization.Get("Reloading, this might take a moment."));
            ChannelServer.Instance.ScriptManager.Reload();
            Send.ServerMessage(sender, Localization.Get("Reload complete."));

            return CommandResult.Okay;
        }

        private CommandResult HandleReloadConf(ChannelClient client, Creature sender, Creature target, string message, IList<string> args)
        {
            Send.ServerMessage(sender, Localization.Get("Beware, reloading should only be used during development, it's not guaranteed to be safe."));
            Send.ServerMessage(sender, Localization.Get("Reloading, this might take a moment."));
            ChannelServer.Instance.Conf.Load();
            Send.ServerMessage(sender, Localization.Get("Reload complete."));

            return CommandResult.Okay;
        }

        private CommandResult HandleAp(ChannelClient client, Creature sender, Creature target, string message, IList<string> args)
        {
            if (args.Count < 2)
                return CommandResult.InvalidArgument;

            // Get amount
            short amount;
            if (!short.TryParse(args[1], out amount))
                return CommandResult.InvalidArgument;

            // Add ap
            target.GiveAp(amount);

            Send.ServerMessage(sender, Localization.Get("Added {0} AP."), amount);
            if (target != sender)
                Send.ServerMessage(target, Localization.Get("{0} gave you {1} AP."), sender.Name, amount);

            return CommandResult.Okay;
        }

        private CommandResult HandleCloseNpc(ChannelClient client, Creature sender, Creature target, string message, IList<string> args)
        {
            if (!client.NpcSession.IsValid())
                return CommandResult.Fail;

            Send.NpcTalkEndR(client.NpcSession.Script.Player, client.NpcSession.Script.NPC.EntityId, "Ended by closenpc command.");

            return CommandResult.Okay;
        }

        private CommandResult HandleGmcp(ChannelClient client, Creature sender, Creature target, string message, IList<string> args)
        {
            if (client.Account.Authority < ChannelServer.Instance.Conf.World.GmcpMinAuth)
            {
                Send.ServerMessage(sender, Localization.Get("You're not authorized to use the GMCP."));
                return CommandResult.Fail;
            }

            sender.Vars.Perm.GMCP = true;

            Send.GmcpOpen(sender);

            return CommandResult.Okay;
        }

        private CommandResult HandleCard(ChannelClient client, Creature sender, Creature target, string message, IList<string> args)
        {
            if (args.Count < 2)
                return CommandResult.InvalidArgument;

            int type, race;
            if (args[0] == "card")
            {
                race = 0;
                if (!int.TryParse(args[1], out type))
                    return CommandResult.InvalidArgument;
            }
            else
            {
                type = MabiId.PetCardType;
                if (!int.TryParse(args[1], out race))
                    return CommandResult.InvalidArgument;
            }

            ChannelServer.Instance.Database.AddCard(target.Client.Account.Id, type, race);

            Send.ServerMessage(sender, Localization.Get("Added card."));
            if (target != sender)
                Send.ServerMessage(target, Localization.Get("You've received a card from '{0}'."), sender.Name);

            return CommandResult.Okay;
        }

        private CommandResult HandleHeal(ChannelClient client, Creature sender, Creature target, string message, IList<string> args)
        {
            target.FullHeal();

            Send.ServerMessage(sender, Localization.Get("Healed."));
            if (target != sender)
                Send.ServerMessage(target, Localization.Get("You've been healed by '{0}'."), sender.Name);

            return CommandResult.Okay;
        }

        private CommandResult HandleClean(ChannelClient client, Creature sender, Creature target, string message, IList<string> args)
        {
            var items = target.Region.GetAllItems();
            foreach (var item in items)
                item.DisappearTime = DateTime.Now;

            Send.ServerMessage(sender, Localization.Get("Marked all items on the floor to disappear now."));
            if (target != sender)
                Send.ServerMessage(target, Localization.Get("{0} removed all items that were lying on the floor in your region."), sender.Name);

            return CommandResult.Okay;
        }

        private CommandResult HandleCondition(ChannelClient client, Creature sender, Creature target, string message, IList<string> args)
        {
            var conditions = new ulong[6];

            // Read arguments
            for (int i = 1; i < args.Count; ++i)
            {
                if (!ulong.TryParse(args[i].Replace("0x", ""), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out conditions[i - 1]))
                {
                    Send.ServerMessage(sender, Localization.Get("Invalid condition number."));
                    return CommandResult.InvalidArgument;
                }
            }

            // Apply conditions
            target.Conditions.Deactivate(ConditionsA.All); target.Conditions.Activate((ConditionsA)conditions[0]);
            target.Conditions.Deactivate(ConditionsB.All); target.Conditions.Activate((ConditionsB)conditions[1]);
            target.Conditions.Deactivate(ConditionsC.All); target.Conditions.Activate((ConditionsC)conditions[2]);
            target.Conditions.Deactivate(ConditionsD.All); target.Conditions.Activate((ConditionsD)conditions[3]);
            target.Conditions.Deactivate(ConditionsE.All); target.Conditions.Activate((ConditionsE)conditions[4]);
            target.Conditions.Deactivate(ConditionsF.All); target.Conditions.Activate((ConditionsF)conditions[5]);

            if (args.Count > 1)
                Send.ServerMessage(sender, Localization.Get("Applied condition."));
            else
                Send.ServerMessage(sender, Localization.Get("Cleared condition."));

            if (target != sender)
                Send.ServerMessage(target, Localization.Get("Your condition has been changed by {0}."), sender.Name);

            return CommandResult.Okay;
        }

        private CommandResult HandleEffect(ChannelClient client, Creature sender, Creature target, string message, IList<string> args)
        {
            // Requirement: command + effect id
            if (args.Count < 2)
                return CommandResult.InvalidArgument;

            var packet = new Packet(Op.Effect, target.EntityId);

            // Get effect id
            uint effectId;
            if (!uint.TryParse(args[1], out effectId))
                return CommandResult.InvalidArgument;

            packet.PutUInt(effectId);

            // Parse arguments
            for (int i = 2; i < args.Count; ++i)
            {
                // type:value
                var splitted = args[i].Split(':');

                // "me" = target's entity id (long)
                if (splitted[0] == "me")
                {
                    packet.PutLong(target.EntityId);
                    continue;
                }

                // Everything but the above arguments require
                // a type and a value.
                if (splitted.Length < 2)
                    continue;

                splitted[0] = splitted[0].Trim();
                splitted[1] = splitted[1].Trim();

                switch (splitted[0])
                {
                    // Byte
                    case "b":
                        {
                            byte val;
                            if (!byte.TryParse(splitted[1], out val))
                                return CommandResult.InvalidArgument;
                            packet.PutByte(val);
                            break;
                        }
                    // Int
                    case "i":
                        {
                            uint val;
                            if (!uint.TryParse(splitted[1], out val))
                                return CommandResult.InvalidArgument;
                            packet.PutUInt(val);
                            break;
                        }
                    // String
                    case "s":
                        {
                            packet.PutString(splitted[1]);
                            break;
                        }
                }
            }

            // Broadcast effect
            target.Region.Broadcast(packet, target);

            Send.ServerMessage(sender, Localization.Get("Applied effect."));
            if (target != sender)
                Send.ServerMessage(target, Localization.Get("{0} has applied an effect to you."), sender.Name);

            return CommandResult.Okay;
        }

        private CommandResult HandleProp(ChannelClient client, Creature sender, Creature target, string message, IList<string> args)
        {
            if (args.Count < 2)
                return CommandResult.InvalidArgument;

            // Get prop id
            int propId;
            if (!int.TryParse(args[1], out propId))
                return CommandResult.InvalidArgument;

            // Create and spawn prop
            var pos = target.GetPosition();
            var prop = new Prop(propId, target.RegionId, pos.X, pos.Y, MabiMath.ByteToRadian(target.Direction));

            target.Region.AddProp(prop);

            Send.ServerMessage(sender, Localization.Get("Spawned prop."));
            if (target != sender)
                Send.ServerMessage(target, Localization.Get("{0} spawned a prop at your location."), sender.Name);

            return CommandResult.Okay;
        }

        private CommandResult HandleWho(ChannelClient client, Creature sender, Creature target, string message, IList<string> args)
        {
            // Get id of the region to look for players in
            int regionId = 0;
            if (args.Count > 1 && !int.TryParse(args[1], out regionId))
                return CommandResult.InvalidArgument;

            // Create list of players in region or world
            List<Creature> players;
            if (regionId != 0)
            {
                var region = ChannelServer.Instance.World.GetRegion(regionId);
                if (region == null)
                {
                    Send.ServerMessage(sender, Localization.Get("Unknown region."));
                    return CommandResult.Fail;
                }

                players = region.GetAllPlayers();

                Send.ServerMessage(sender, Localization.Get("Players online in region {0} ({1}):"), regionId, players.Count);
            }
            else
            {
                players = ChannelServer.Instance.World.GetAllPlayers();

                Send.ServerMessage(sender, Localization.Get("Players online ({0}):"), players.Count);
            }

            // Send list of players
            Send.ServerMessage(sender,
                players.Count == 0
                ? Localization.Get("None")
                : string.Join(", ", players.Select(a => a.Name))
            );

            return CommandResult.Okay;
        }

        private CommandResult HandleMotion(ChannelClient client, Creature sender, Creature target, string message, IList<string> args)
        {
            if (args.Count < 3)
                return CommandResult.InvalidArgument;

            int category, motion;
            if (!int.TryParse(args[1], out category) || !int.TryParse(args[2], out motion))
                return CommandResult.InvalidArgument;

            Send.UseMotion(target, category, motion, false, true);

            Send.ServerMessage(sender, Localization.Get("Applied motion."));
            if (target != sender)
                Send.ServerMessage(target, Localization.Get("{0} has applied a motion to you."), sender.Name);

            return CommandResult.Okay;
        }

        private CommandResult HandleGesture(ChannelClient client, Creature sender, Creature target, string message, IList<string> args)
        {
            if (args.Count < 2)
                return CommandResult.InvalidArgument;

            // Get given gesture
            var gesture = AuraData.MotionDb.Find(args[1]);
            if (gesture == null)
            {
                Send.ServerMessage(sender, Localization.Get("Unknown gesture."));
                return CommandResult.Fail;
            }

            Send.UseMotion(target, gesture.Category, gesture.Type, gesture.Loop, true);

            Send.ServerMessage(sender, Localization.Get("Gestured '{0}'."), args[1]);
            if (target != sender)
                Send.ServerMessage(target, Localization.Get("{0} made you gesture '{1}'."), sender.Name, args[1]);

            return CommandResult.Okay;
        }

        private CommandResult HandleBroadcast(ChannelClient client, Creature sender, Creature target, string message, IList<string> args)
        {
            if (args.Count < 2)
                return CommandResult.InvalidArgument;

            var notice = sender.Name + ": " + message.Substring(message.IndexOf(" "));

            Send.Internal_Broadcast(notice);

            return CommandResult.Okay;
        }

        private CommandResult HandleMsg(ChannelClient client, Creature sender, Creature target, string message, IList<string> args)
        {
            if (args.Count < 2)
                return CommandResult.InvalidArgument;

            Send.System_Broadcast(target.Name, message.Substring(message.IndexOf(" ")));

            return CommandResult.Okay;
        }

        private CommandResult HandleAllSkills(ChannelClient client, Creature sender, Creature target, string message, IList<string> args)
        {
            // List of "working", normal player skills
            var listOfSkills = new SkillId[]
            {
                SkillId.Counterattack, SkillId.CriticalHit, SkillId.Defense, SkillId.FinalHit, SkillId.MagnumShot, SkillId.RangedAttack,
                SkillId.Smash, SkillId.Windmill,

                SkillId.Campfire, SkillId.FirstAid, SkillId.Fishing, SkillId.Handicraft, SkillId.Herbalism, SkillId.PotionMaking,
                SkillId.ProductionMastery, SkillId.Refining, SkillId.Rest, SkillId.Tailoring, SkillId.Weaving,

                SkillId.Firebolt, SkillId.Healing, SkillId.Icebolt, SkillId.Lightningbolt, SkillId.ManaShield, SkillId.Meditation, SkillId.SilentMove,

                SkillId.Composing, SkillId.PlayingInstrument, SkillId.Song,
            };

            // Add all skills
            foreach (var sid in listOfSkills)
            {
                var skill = AuraData.SkillDb.Find((int)sid);
                if (skill == null) continue;

                target.Skills.Give(sid, (SkillRank)skill.MaxRank);
            }

            // Success
            Send.ServerMessage(sender, Localization.Get("Added all skills the server supports on their max rank."));
            if (target != sender)
                Send.ServerMessage(target, Localization.Get("{0} gave you all skills the server supports on their max rank."), sender.Name);

            return CommandResult.Okay;
        }

        private CommandResult HandleAllTitles(ChannelClient client, Creature sender, Creature target, string message, IList<string> args)
        {
            // Add all titles. Using Enable to send an enable packet for
            // every title crashes the client.
            foreach (var title in AuraData.TitleDb.Entries.Values)
                target.Titles.Add(title.Id, TitleState.Usable);

            // Success
            Send.ServerMessage(sender, Localization.Get("Enabled all available titles, please relog to use them."));
            if (target != sender)
                Send.ServerMessage(target, Localization.Get("{0} enabled all available titles for you, please relog to use them."), sender.Name);

            return CommandResult.Okay;
        }

        private CommandResult HandleDistance(ChannelClient client, Creature sender, Creature target, string message, IList<string> args)
        {
            var distancePos = sender.Vars.Temp.DistanceCommandPos;

            // Save distance or calculate distance if this is the second call
            if (distancePos == null)
            {
                sender.Vars.Temp.DistanceCommandPos = sender.GetPosition();
                Send.ServerMessage(sender, Localization.Get("Position 1 saved, use command again to calculate distance."));
            }
            else
            {
                var pos2 = sender.GetPosition();
                var distance = pos2.GetDistance(distancePos);

                Send.ServerMessage(sender, Localization.Get("Distance between '{0}' and '{1}': {2}"), distancePos, pos2, distance);

                sender.Vars.Temp.DistanceCommandPos = null;
            }

            return CommandResult.Okay;
        }

        private CommandResult HandleGold(ChannelClient client, Creature sender, Creature target, string message, IList<string> args)
        {
            if (args.Count < 2)
                return CommandResult.InvalidArgument;

            // Get amount
            int amount;
            if (!int.TryParse(args[1], out amount))
                return CommandResult.InvalidArgument;

            // Cap amount, client doesn't handle this many entities too well
            if (amount > 1000000)
                amount = 1000000;

            // Create gold stacks until amount is reached
            var rnd = RandomProvider.Get();
            var i = amount;
            while (i > 0)
            {
                var stack = new Item(2000);
                stack.Info.Amount = (ushort)Math.Min(1000, i);
                i -= stack.Info.Amount;

                // Add them to inv or drop them if inv is full
                if (!target.Inventory.Insert(stack, false))
                    stack.Drop(target.Region, target.GetPosition().GetRandomInRange(500, rnd));
            }

            Send.SystemMessage(sender, Localization.Get("Spawned {0:n0}g."), amount);
            if (sender != target)
                Send.SystemMessage(target, Localization.Get("{0} gave you {1:n0}g."), sender.Name, amount);

            return CommandResult.Okay;
        }

        private CommandResult HandleFavor(ChannelClient client, Creature sender, Creature target, string message, IList<string> args)
        {
            if (args.Count < 2)
                return CommandResult.InvalidArgument;

            // Get NPC
            var name = args[1];
            var npc = ChannelServer.Instance.World.GetNpc(name);
            if (npc == null)
            {
                Send.SystemMessage(sender, Localization.Get("NPC '{0}' doesn't exist."), name);
                return CommandResult.Fail;
            }

            // Get favor
            var favor = npc.GetFavor(target);

            // Output favor if no set parameter
            if (args.Count < 3)
            {
                Send.SystemMessage(sender, Localization.Get("Favor of {0}: {1}"), name, favor);
                return CommandResult.Okay;
            }

            // Get amount
            int amount;
            if (!int.TryParse(args[2], out amount))
                return CommandResult.InvalidArgument;

            // Set favor
            favor = npc.SetFavor(target, amount);

            Send.SystemMessage(sender, Localization.Get("Changed favor for {0}, new value: {1}"), name, favor);
            if (sender != target)
                Send.SystemMessage(target, Localization.Get("{2} changed {0}'s favor towards you, new value: {1}"), name, favor, sender.Name);

            return CommandResult.Okay;
        }

        private CommandResult HandleStress(ChannelClient client, Creature sender, Creature target, string message, IList<string> args)
        {
            if (args.Count < 2)
                return CommandResult.InvalidArgument;

            // Get NPC
            var name = args[1];
            var npc = ChannelServer.Instance.World.GetNpc(name);
            if (npc == null)
            {
                Send.SystemMessage(sender, Localization.Get("NPC '{0}' doesn't exist."), name);
                return CommandResult.Fail;
            }

            // Get stress
            var stress = npc.GetStress(target);

            // Output stress if no set parameter
            if (args.Count < 3)
            {
                Send.SystemMessage(sender, Localization.Get("Stress of {0}: {1}"), name, stress);
                return CommandResult.Okay;
            }

            // Get amount
            int amount;
            if (!int.TryParse(args[2], out amount))
                return CommandResult.InvalidArgument;

            // Set stress
            stress = npc.SetStress(target, amount);

            Send.SystemMessage(sender, Localization.Get("Changed stress for {0}, new value: {1}"), name, stress);
            if (sender != target)
                Send.SystemMessage(target, Localization.Get("{2} changed {0}'s stress towards you, new value: {1}"), name, stress, sender.Name);

            return CommandResult.Okay;
        }

        private CommandResult HandleMemory(ChannelClient client, Creature sender, Creature target, string message, IList<string> args)
        {
            if (args.Count < 2)
                return CommandResult.InvalidArgument;

            // Get NPC
            var name = args[1];
            var npc = ChannelServer.Instance.World.GetNpc(name);
            if (npc == null)
            {
                Send.SystemMessage(sender, Localization.Get("NPC '{0}' doesn't exist."), name);
                return CommandResult.Fail;
            }

            // Get memory
            var memory = npc.GetMemory(target);

            // Output memory if not set parameter
            if (args.Count < 3)
            {
                Send.SystemMessage(sender, Localization.Get("Memory of {0}: {1}"), name, memory);
                return CommandResult.Okay;
            }

            // Get amount
            int amount;
            if (!int.TryParse(args[2], out amount))
                return CommandResult.InvalidArgument;

            // Set memory
            memory = npc.SetMemory(target, amount);

            Send.SystemMessage(sender, Localization.Get("Changed memory for {0}, new value: {1}"), name, memory);
            if (sender != target)
                Send.SystemMessage(target, Localization.Get("{2} changed how well {0} remembers you, new value: {1}"), name, memory, sender.Name);

            return CommandResult.Okay;
        }

        private CommandResult HandleEgo(ChannelClient client, Creature sender, Creature target, string message, IList<string> args)
        {
            if (args.Count < 4)
            {
                Send.SystemMessage(sender, Localization.Get("Ego races:"));
                foreach (EgoRace race in Enum.GetValues(typeof(EgoRace)))
                    Send.SystemMessage(sender, "{0}: {1}", (byte)race, race);

                return CommandResult.InvalidArgument;
            }

            // Get item id
            int itemId;
            if (!int.TryParse(args[1], out itemId))
                return CommandResult.InvalidArgument;

            // Get ego race
            EgoRace egoRace;
            if (!EgoRace.TryParse(args[3], out egoRace) || (egoRace <= EgoRace.None || egoRace > EgoRace.CylinderF))
                return CommandResult.InvalidArgument;

            // Check item data
            var itemData = AuraData.ItemDb.Find(itemId);
            if (itemData == null)
            {
                Send.ServerMessage(sender, Localization.Get("Item '{0}' not found in database."), args[1]);
                return CommandResult.Fail;
            }

            // Check for ego weapon
            if (!itemData.HasTag("/ego_weapon/"))
            {
                Send.ServerMessage(sender, Localization.Get("Item doesn't have the 'ego_weapon' tag."));
                return CommandResult.Fail;
            }

            // Create item
            var item = Item.CreateEgo(itemData.Id, egoRace, args[2]);

            // Parse colors
            if (args.Count > 4)
            {
                uint color1, color2, color3;
                if (!TryParseColorsFromArgs(args, 4, out color1, out color2, out color3))
                {
                    Send.ServerMessage(sender, Localization.Get("Invalid or unknown color."));
                    return CommandResult.InvalidArgument;
                }

                item.Info.Color1 = color1;
                item.Info.Color2 = color2;
                item.Info.Color3 = color3;
            }

            // Add item
            if (target.Inventory.Add(item, Pocket.Temporary))
            {
                if (sender != target)
                    Send.ServerMessage(target, Localization.Get("Ego '{0}' has been spawned by '{1}'."), itemData.Name, sender.Name);
                Send.ServerMessage(sender, Localization.Get("Ego '{0}' has been spawned."), itemData.Name);
                return CommandResult.Okay;
            }
            else
            {
                Send.ServerMessage(sender, Localization.Get("Failed to spawn ego."));
                return CommandResult.Fail;
            }
        }

        private static bool TryParseColorsFromArgs(IList<string> args, int offset, out uint color1, out uint color2, out uint color3)
        {
            color1 = 0;
            color2 = 0;
            color3 = 0;

            for (int i = 0; i < 3; ++i)
            {
                if (args.Count < offset + 1 + i)
                    break;

                var sColor = args[offset + i];
                uint color = 0;

                // Hex color
                if (sColor.StartsWith("0x"))
                {
                    if (!uint.TryParse(sColor.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out color))
                        return false;
                }
                else
                {
                    switch (sColor)
                    {
                        case "0":
                        case "black": color = 0x00000000; break;
                        case "f":
                        case "white": color = 0xFFFFFFFF; break;
                        default:
                            return false;
                    }
                }

                switch (i)
                {
                    case 0: color1 = color; break;
                    case 1: color2 = color; break;
                    case 2: color3 = color; break;
                }
            }

            return true;
        }

        private CommandResult HandleWeather(ChannelClient client, Creature sender, Creature target, string message, IList<string> args)
        {
            var weather = ChannelServer.Instance.Weather.GetWeather(target.RegionId);

            // Return weather of current region
            if (args.Count == 1)
            {
                if (weather == null)
                    Send.ServerMessage(sender, Localization.Get("No weather specified for your region."));
                else
                    Send.ServerMessage(sender, Localization.Get("Current weather: {0}"), weather);

                return CommandResult.Okay;
            }

            // Change weather to weather table
            if (args[1].StartsWith("type"))
            {
                Send.ServerMessage(sender, Localization.Get("Changing weather to table (typeX) requires a relog."));

                ChannelServer.Instance.Weather.SetProviderAndUpdate(target.RegionId, new WeatherProviderTable(target.RegionId, args[1]));

                return CommandResult.Okay;
            }

            // Change weather to something specific
            float val;
            if (!float.TryParse(args[1], out val))
            {
                switch (args[1])
                {
                    case "clear": val = 0.5f; break;
                    case "clouds": val = 1.5f; break;
                    case "rain": val = 1.95f; break;
                    case "storm": val = 2.0f; break;

                    default:
                        Send.ServerMessage(sender, Localization.Get("Unknown weather type."));
                        return CommandResult.InvalidArgument;
                }
            }

            // Clamp to min/max 0~2, other values can cause weird looking results
            val = Math2.Clamp(0, 2, val);

            ChannelServer.Instance.Weather.SetProviderAndUpdate(target.RegionId, new WeatherProviderConstant(target.RegionId, val));

            return CommandResult.Okay;
        }

        private CommandResult HandleTeleWalk(ChannelClient client, Creature sender, Creature target, string message, IList<string> args)
        {
            // Toggle telewalk
            if (target.Vars.Temp["telewalk"] == null)
            {
                target.Vars.Temp["telewalk"] = true;

                if (sender != target)
                    Send.ServerMessage(target, Localization.Get("'{0}' has enabled telewalk for you."), sender.Name);
                Send.ServerMessage(sender, Localization.Get("Telewalk enabled."));
            }
            else
            {
                target.Vars.Temp["telewalk"] = null;

                if (sender != target)
                    Send.ServerMessage(target, Localization.Get("'{0}' has disabled telewalk for you."), sender.Name);
                Send.ServerMessage(sender, Localization.Get("Telewalk disabled."));
            }

            return CommandResult.Okay;
        }

        private CommandResult HandleDungeon(ChannelClient client, Creature sender, Creature target, string message, IList<string> args)
        {
            if (args.Count < 2)
                return CommandResult.InvalidArgument;

            var dungeonName = args[1];
            var itemId = 2000; // Gold

            // Check dungeon
            var dungeonData = AuraData.DungeonDb.Find(dungeonName);
            if (dungeonData == null)
            {
                Send.SystemMessage(sender, Localization.Get("Dungeon '{0}' not found in database."), dungeonName);
                return CommandResult.InvalidArgument;
            }

            // Create dungeon and warp in
            if (!ChannelServer.Instance.World.DungeonManager.CreateDungeonAndWarp(dungeonName, itemId, target))
            {
                Send.SystemMessage(sender, Localization.Get("Failed to create dungeon."), dungeonName);
                return CommandResult.Fail;
            }

            Send.ServerMessage(sender, Localization.Get("Warped into {0}."), dungeonData.EngName);
            if (sender != target)
                Send.ServerMessage(target, Localization.Get("{0} warped you into {1}."), sender.Name, dungeonData.EngName);

            return CommandResult.Okay;
        }

        private CommandResult HandleLastTown(ChannelClient client, Creature sender, Creature target, string message, IList<string> args)
        {
            if (string.IsNullOrWhiteSpace(target.LastTown))
                Send.ServerMessage(sender, Localization.Get("No last town found."));
            else if (!target.Warp(target.LastTown))
                Send.ServerMessage(sender, Localization.Get("Warp failed."));

            return CommandResult.Okay;
        }

        private CommandResult HandleCutscene(ChannelClient client, Creature sender, Creature target, string message, IList<string> args)
        {
            if (args.Count < 2)
                return CommandResult.InvalidArgument;

            var cutscene = args[1];

            // Check given cutscene
            if (!AuraData.CutscenesDb.Exists(cutscene))
            {
                Send.ServerMessage(sender, Localization.Get("Cutscene not found."));
                return CommandResult.Okay;
            }

            // Play cutscene
            Cutscene.Play(cutscene, target);

            return CommandResult.Okay;
        }

        private CommandResult HandlePartySize(ChannelClient client, Creature sender, Creature target, string message, IList<string> args)
        {
            if (args.Count < 2)
                return CommandResult.InvalidArgument;

            int size;
            if (!int.TryParse(args[1], out size))
                return CommandResult.InvalidArgument;

            if (!target.IsInParty)
            {
                Send.SystemMessage(sender, Localization.Get("Target creature is not in a party."));
                return CommandResult.Okay;
            }

            target.Party.SetMaxSize(size);

            Send.SystemMessage(sender, Localization.Get("Changed party size to {0}."), target.Party.MaxSize);
            if (sender != target)
                Send.SystemMessage(target, Localization.Get("Party size changed to {0} by {1}."), target.Party.MaxSize, sender.Name);

            return CommandResult.Okay;
        }

        private CommandResult HandleOpenShop(ChannelClient client, Creature sender, Creature target, string message, IList<string> args)
        {
            if (args.Count < 2)
                return CommandResult.InvalidArgument;

            var typeName = args[1];

            // Get shop
            var shop = ChannelServer.Instance.ScriptManager.NpcShopScripts.Get(typeName);
            if (shop == null)
            {
                Send.ServerMessage(sender, Localization.Get("Unable to find shop '{0}'."), typeName);
                return CommandResult.Okay;
            }

            shop.OpenRemotelyFor(target);

            Send.SystemMessage(sender, Localization.Get("Opened shop '{0}'."), typeName);

            return CommandResult.Okay;
        }

        private CommandResult HandleNameChatColor(ChannelClient client, Creature sender, Creature target, string message, IList<string> args)
        {
            var idx = 0;

            if (args.Count > 1)
            {
                if (!int.TryParse(args[1], out idx) || !Math2.Between(idx, 0, 32))
                    return CommandResult.InvalidArgument;
            }

            // Reset colors
            if (idx == 0)
            {
                target.Vars.Perm["NameColorIdx"] = null;
                target.Vars.Perm["NameColorEnd"] = null;
                target.Vars.Perm["ChatColorIdx"] = null;
                target.Vars.Perm["ChatColorEnd"] = null;

                target.Conditions.Deactivate(ConditionsB.NameColorChange);
                target.Conditions.Deactivate(ConditionsB.ChatColorChange);
            }
            // Set colors
            else
            {
                target.Vars.Perm["NameColorIdx"] = idx;
                target.Vars.Perm["ChatColorIdx"] = idx;
                target.Vars.Perm["NameColorEnd"] = null;
                target.Vars.Perm["ChatColorEnd"] = null;

                var extra = new MabiDictionary();
                extra.SetInt("IDX", idx);

                target.Conditions.Activate(ConditionsB.NameColorChange, extra);
                target.Conditions.Activate(ConditionsB.ChatColorChange, extra);
            }

            if (sender == target)
                Send.SystemMessage(target, Localization.Get("Your name/chat color has been changed."));
            else
                Send.SystemMessage(target, Localization.Get("Your name/chat color has been changed by {0}."), sender.Name);

            return CommandResult.Okay;
        }

        private CommandResult HandlePoints(ChannelClient client, Creature sender, Creature target, string message, IList<string> args)
        {
            var oldVal = target.Client.Account.Points;

            // Output current points
            if (args.Count < 2)
            {
                if (sender == target)
                    Send.ServerMessage(sender, Localization.Get("Your Pon: {0}"), oldVal);
                else
                    Send.ServerMessage(sender, Localization.Get("{1}'s Pon: {0}"), oldVal, target.Name);

                return CommandResult.Okay;
            }

            // Get modificator
            int mod;
            if (!int.TryParse(args[1], out mod))
                return CommandResult.InvalidArgument;

            // Modificate
            var newVal = (target.Client.Account.Points += mod);

            // Notice
            Send.ServerMessage(sender, Localization.Get("Pon modificated: {0} -> {1}."), oldVal, target.Client.Account.Points);
            if (sender != target)
                Send.ServerMessage(target, Localization.Get("Your Pon have been modificated by {2}: {0} -> {1}."), oldVal, newVal, sender.Name);

            return CommandResult.Okay;
        }

        private CommandResult HandleShutdown(ChannelClient client, Creature sender, Creature target, string message, IList<string> args)
        {
            int time = 0;

            if (args.Count < 2)
                return CommandResult.InvalidArgument;

            // Get time
            if (!int.TryParse(args[1], out time))
                return CommandResult.InvalidArgument;

            if (ChannelServer.Instance.ShuttingDown)
            {
                Log.Info("Server is already being shut down");
                return CommandResult.Fail;
            }
            else
            {
                ChannelServer.Instance.ShuttingDown = true;
            }

            // Set minimum time to 1 minute (60 seconds)
            if (time < 60)
                time = 60;

            // Cap time to 30 min (1800 seconds)
            if (time > 1800)
                time = 1800;

            // Shutdown warning
            Send.Internal_Broadcast("The server will be brought down for maintenance in " + time + " seconds. Please log out safely before then.");

            // Send MsgBox to all users
            foreach (var user in ChannelServer.Instance.Server.Clients)
            {
                try {
                    Send.MsgBox(user.GetControlledCreatureSafe(), "You will be logged out automatically in {0} seconds.", time);
                }
                catch (Exception e)
                {
                    Log.Info("Failed to send MsgBox to user. " + e.Message);
                }
            }

            Log.Info("Shutting down in {0} seconds...", time);

            // Shutdown timer
            this.StartShutdownTimer(time * 1000);

            return CommandResult.Okay;
        }

        public void StartShutdownTimer(int timeUntilShutdown)
        {
            Timer t = new Timer(new TimerCallback(TimerDone));
            t.Change(timeUntilShutdown, 0);
        }

        private void TimerDone(object timer)
        {
            Timer t = (Timer)timer;
            t.Dispose();

            // Save all client data and kill
            foreach (var user in ChannelServer.Instance.Server.Clients)
            {
                Log.Info("Closing the connection to '{0}'...", user.Account.Id);
                user.CleanUp();
                user.Kill();
            }

            // Close the ChannelServer
            CliUtil.Exit(0, false);
        }

    }

    public class GmCommand : Command<GmCommandFunc>
	{
		public int Auth { get; private set; }
		public int CharAuth { get; private set; }

		public GmCommand(int auth, int charAuth, string name, string usage, GmCommandFunc func)
			: base(name, usage, "", func)
		{
			this.Auth = auth;
			this.CharAuth = charAuth;
		}
	}
	
	public delegate CommandResult GmCommandFunc(ChannelClient client, Creature sender, Creature target, string message, IList<string> args);
}
