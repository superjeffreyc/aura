﻿// Copyright (c) Aura development team - Licensed under GNU GPL
// For more information, see license file in the main folder

using System.Collections.Generic;
using Aura.Login.Database;
using Aura.Shared.Util;
using Aura.Shared.Util.Commands;
using System;
using System.Diagnostics;
using Aura.Login.Network;
using Aura.Shared.Network;
using Aura.Mabi.Network;
using Aura.Mabi.Const;
using System.Threading;

namespace Aura.Login.Util
{
	public class LoginConsoleCommands : ConsoleCommands
	{
		public LoginConsoleCommands()
		{
			this.Add("shutdown", "<seconds>", "Orders all servers to shut down", HandleShutDown);
			this.Add("auth", "<account> <level>", "Changes authority level of account", HandleAuth);
			this.Add("passwd", "<account> <password>", "Changes password of account", HandlePasswd);
		}

		private CommandResult HandleShutDown(string command, IList<string> args)
		{
				int time = 0;
	
				if (args.Count < 2)
					return CommandResult.InvalidArgument;
	
				// Get time
				if (!int.TryParse(args[1], out time))
					return CommandResult.InvalidArgument;
	
				// TODO: (Enhancement) If there is no ChannelServer running, refuse shutdown command
				
				// Set minimum time to 1 minute (60 seconds)
				if (time < 60)
					time = 60;
	
				// Cap time to 30 min (1800 seconds)
				if (time > 1800)
					time = 1800;
	
				// Shutdown preparation
				LoginServer.Instance.BroadcastChannels(new Packet(Op.RequestClientDisconnect, MabiId.Channel).PutInt(0).PutInt(time));
			 
				return CommandResult.Okay;
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
	}
}
