// Copyright (c) Aura development team - Licensed under GNU GPL
// For more information, see license file in the main folder

using Aura.Shared.Network;
using Aura.Mabi;
using Aura.Shared.Util;
using System;
using Aura.Login.Database;
using Aura.Mabi.Network;

namespace Aura.Login.Network.Handlers
{
	public partial class LoginServerHandlers : PacketHandlerManager<LoginClient>
	{
		/// <summary>
		/// Sent after connecting, to verify identity.
		/// </summary>
		[PacketHandler(Op.Internal.ServerIdentify)]
		public void Internal_ServerIdentify(LoginClient client, Packet packet)
		{
			var passwordHash = packet.GetString();

			if (!Password.Check(LoginServer.Instance.Conf.Internal.Password, passwordHash))
			{
				Send.Internal_ServerIdentifyR(client, false);

				Log.Warning("Invalid internal password from '{0}'.", client.Address);
				client.Kill();
				return;
			}

			client.State = ClientState.LoggedIn;

			lock (LoginServer.Instance.ChannelClients)
				LoginServer.Instance.ChannelClients.Add(client);

			Send.Internal_ServerIdentifyR(client, true);
		}

		/// <summary>
		/// Regularly sent by channels, to update some numbers.
		/// </summary>
		[PacketHandler(Op.Internal.ChannelStatus)]
		public void Internal_ChannelStatus(LoginClient client, Packet packet)
		{
			if (client.State != ClientState.LoggedIn)
				return;

			var serverName = packet.GetString();
			var channelName = packet.GetString();
			var host = packet.GetString();
			var port = packet.GetInt();
			var users = packet.GetInt();
			var maxUsers = packet.GetInt();
			var state = (ChannelState)packet.GetInt();

			var server = LoginServer.Instance.ServerList.Add(serverName);

			ChannelInfo channel;
			server.Channels.TryGetValue(channelName, out channel);
			if (channel == null)
			{
				channel = new ChannelInfo(channelName, serverName, host, port);
				server.Channels.Add(channelName, channel);

				Log.Info("New channel registered: {0}", channel.FullName);
			}

			// A way to identify the channel of this client
			if (client.Account == null)
			{
				client.Account = new Account();
				client.Account.Name = channel.FullName;
			}

			if (channel.State != state)
			{
				Log.Status("Channel '{0}' is now in '{1}' mode.", channel.FullName, state);
			}

			channel.Host = host;
			channel.Port = port;
			channel.Users = users;
			channel.MaxUsers = maxUsers;
			channel.LastUpdate = DateTime.Now;
			channel.State = state;

			Send.ChannelStatus(LoginServer.Instance.ServerList.List);
			Send.Internal_ChannelStatus(LoginServer.Instance.ServerList.List);
		}

		[PacketHandler(Op.Internal.ChannelShutdownR)]
		public void Internal_ChannelShutdownR(LoginClient client, Packet packet)
		{
			var result = (ShutdownResult) packet.GetByte();

			switch (result)
			{
				case ShutdownResult.Success:
					Log.Info("{0}\r\n\tShutdown request: Success.", client.Account.Name);
					break;
				case ShutdownResult.AlreadyInProgress:
					Log.Info("{0}\r\n\t Shutdown request: Already in progress.", client.Account.Name);
					break;
				case ShutdownResult.Fail:
					Log.Info("{0}\r\n\t Shutdown request: Failed.", client.Account.Name);
					break;
				default:
					Log.Info("{0}\r\n\t Shutdown request: Unknown response.", client.Account.Name);
					break;
			}
		}

		/// <summary>
		/// Sent from channels to forward it to all others,
		/// message to broadcast
		/// </summary>
		/// <example>
		/// 001 [................] String : test
		/// </example>
		[PacketHandler(Op.Internal.BroadcastNotice)]
		public void Broadcast(LoginClient client, Packet packet)
		{
			LoginServer.Instance.BroadcastChannels(packet);
		}
	}
}
