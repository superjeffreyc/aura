// Copyright (c) Aura development team - Licensed under GNU GPL
// For more information, see license file in the main folder

using System.Collections.Generic;
using System.Linq;
using Aura.Login.Database;
using Aura.Login.Network;
using Aura.Shared.Util;
using Aura.Shared.Util.Commands;

namespace Aura.Login.Util
{
	public class LoginConsoleCommands : ConsoleCommands
	{
		public LoginConsoleCommands()
		{
			this.Add("shutdown", "<serverFullName|*> [timeInSeconds]", "Shuts down a single or all channels", HandleShutDown);
			this.Add("auth", "<account> <level>", "Changes authority level of account", HandleAuth);
			this.Add("passwd", "<account> <password>", "Changes password of account", HandlePasswd);
		}

		private CommandResult HandleAuth(string command, IList<string> args)
		{
			if (args.Count < 3)
				return CommandResult.InvalidArgument;

			int level;
			if (!int.TryParse(args[2], out level))
				return CommandResult.InvalidArgument;

			if (!LoginServer.Instance.Database.ChangeAuth(args[1], level))
			{
				Log.Error("Failed to change auth. (Does the account exist?)");
				return CommandResult.Okay;
			}

			Log.Info("Changed auth successfully.");

			return CommandResult.Okay;
		}

		private CommandResult HandlePasswd(string command, IList<string> args)
		{
			if (args.Count < 3)
			{
				return CommandResult.InvalidArgument;
			}

			var accountName = args[1];
			var password = args[2];

			if (!LoginServer.Instance.Database.AccountExists(accountName))
			{
				Log.Error("Please specify an existing account.");
				return CommandResult.Fail;
			}

			LoginServer.Instance.Database.SetAccountPassword(accountName, password);

			Log.Info("Password change for {0} complete.", accountName);

			return CommandResult.Okay;
		}

		private CommandResult HandleShutDown(string command, IList<string> args)
		{
			if (args.Count < 3)
				return CommandResult.InvalidArgument;

			var time = 60;

			if (args.Count == 3)
			{
				int.TryParse(args[2], out time);
			}
				
			var channelFullName = args[1];

			if (channelFullName == "*")
			{
				foreach (var channelClient in LoginServer.Instance.ChannelClients)
				{
					Send.Internal_ChannelShutdown(channelClient, time);
				}

				return CommandResult.Okay;
			}

			var channel = LoginServer.Instance.ChannelClients.FirstOrDefault(c => c.Account.Name == channelFullName);
			if (channel == null)
			{
				Log.Info("channel {0}@{1} does not exist.");
				return CommandResult.InvalidArgument;
			}

			Send.Internal_ChannelShutdown(channel, time);
			return CommandResult.Okay;
		}
	}
}
