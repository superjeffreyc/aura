// Copyright (c) Aura development team - Licensed under GNU GPL
// For more information, see license file in the main folder

using Aura.Shared.Util;
using Aura.Shared.Util.Commands;
using System.Collections.Generic;
using System.Linq;
using Aura.Shared.Network;

namespace Aura.Channel.Util
{
	public class ChannelConsoleCommands : ConsoleCommands
	{
		public ChannelConsoleCommands()
		{
			this.Add("shutdown", "[time]", "Shuts the channel down and notifies LoginServer.", HandleShutdown);
		}

		protected override CommandResult HandleStatus(string command, IList<string> args)
		{
			var result = base.HandleStatus(command, args);
			if (result != CommandResult.Okay)
				return result;

			var creatures = ChannelServer.Instance.World.GetAllCreatures();

			Log.Status("Creatures in world: {0}", creatures.Count);
			Log.Status("Players in world: {0}", creatures.Count(a => a.IsPlayer));

			return CommandResult.Okay;
		}

		private CommandResult HandleShutdown(string command, IList<string> args)
		{
			var time = 60;

			if (args.Count < 1)
				int.TryParse(args[1], out time);

			var result = ChannelServer.Instance.Shutdown(time);

			switch (result)
			{
				case ShutdownResult.Success:
					Log.Info("Shutdown request: Success.");
					break;
				case ShutdownResult.AlreadyInProgress:
					Log.Info("Shutdown request: Already in progress.");
					break;
				case ShutdownResult.Fail:
					Log.Info("Shutdown request: Failed.");
					break;
				default:
					Log.Info("Shutdown request: Unknown response.");
					break;
			}
			return CommandResult.Okay;
		}
	}
}
