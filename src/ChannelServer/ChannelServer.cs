﻿// Copyright (c) Aura development team - Licensed under GNU GPL
// For more information, see license file in the main folder

using Aura.Channel.Database;
using Aura.Channel.Network;
using Aura.Channel.Network.Handlers;
using Aura.Channel.Network.Sending;
using Aura.Channel.Scripting;
using Aura.Channel.Skills;
using Aura.Channel.Util;
using Aura.Channel.Util.Configuration;
using Aura.Channel.World;
using Aura.Channel.World.Weather;
using Aura.Mabi.Network;
using Aura.Shared;
using Aura.Shared.Database;
using Aura.Shared.Network;
using Aura.Shared.Network.Crypto;
using Aura.Shared.Util;
using Aura.Shared.Util.Configuration;
using System;
using System.Net.Sockets;
using System.Threading;
using System.Timers;
using Aura.Mabi.Const;

namespace Aura.Channel
{
	public class ChannelServer : ServerMain
	{
		public static readonly ChannelServer Instance = new ChannelServer();

		/// <summary>
		/// Milliseconds between connection tries.
		/// </summary>
		private const int LoginTryTime = 10 * 1000;

		private System.Timers.Timer _shutdownTimer = null;

		private bool _isRunning = false;

		/// <summary>
		/// Used to determine if the server is running
		/// </summary>
		public bool IsRunning
		{
			get { return _isRunning; }
			private set { _isRunning = value; }
		}

		private bool _isInMaintenance = false;

		/// <summary>
		/// Can be used to set the server into maintenance
		/// </summary>
		public bool IsInMaintenance
		{
			get
			{
				return _isInMaintenance;            
			}
			set
			{
				_isInMaintenance = value;
				Send.Internal_ChannelStatus();
			}
		}

		/// <summary>
		/// Instance of the actual server component.
		/// </summary>
		public DefaultServer<ChannelClient> Server { get; protected set; }

		/// <summary>
		/// List of servers and channels.
		/// </summary>
		public ServerInfoManager ServerList { get; private set; }

		/// <summary>
		/// Configuration
		/// </summary>
		public ChannelConf Conf { get; private set; }

		/// <summary>
		/// Database
		/// </summary>
		public ChannelDb Database { get; private set; }

		/// <summary>
		/// Client connecting to the login server.
		/// </summary>
		public InternalClient LoginServer { get; private set; }

		public GmCommandManager CommandProcessor { get; private set; }
		public ChannelConsoleCommands ConsoleCommands { get; private set; }

		public ScriptManager ScriptManager { get; private set; }
		public SkillManager SkillManager { get; private set; }
		public EventManager Events { get; private set; }
		public WeatherManager Weather { get; private set; }
		public PartyManager PartyManager { get; private set; }

		public WorldManager World { get; private set; }

		private ChannelServer()
		{
			AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(CurrentDomain_UnhandledException);

			this.Server = new DefaultServer<ChannelClient>();
			this.Server.Handlers = new ChannelServerHandlers();
			this.Server.Handlers.AutoLoad();
			this.Server.ClientDisconnected += this.OnClientDisconnected;

			this.ServerList = new ServerInfoManager();

			this.CommandProcessor = new GmCommandManager();
			this.ConsoleCommands = new ChannelConsoleCommands();

			this.ScriptManager = new ScriptManager();
			this.SkillManager = new SkillManager();
			this.Events = new EventManager();
			this.Weather = new WeatherManager();
			this.PartyManager = new PartyManager();
		}

		/// <summary>
		/// Loads all necessary components and starts the server.
		/// </summary>
		public void Run()
		{
			if (this.IsRunning)
				throw new Exception("Server is already running.");

			this.IsInMaintenance = true;

			CliUtil.WriteHeader("Channel Server", ConsoleColor.DarkGreen);
			CliUtil.LoadingTitle();

			this.NavigateToRoot();

			// Conf
			this.LoadConf(this.Conf = new ChannelConf());

			// Database
			this.InitDatabase(this.Database = new ChannelDb(), this.Conf);

			// Data
			this.LoadData(DataLoad.ChannelServer, false);

			// Localization
			this.LoadLocalization(this.Conf);

			// World
			this.InitializeWorld();

			// Skills
			this.LoadSkills();

			// Scripts
			this.LoadScripts();

			// Weather
			this.Weather.Initialize();

			// Autoban
			if (this.Conf.Autoban.Enabled)
				this.Events.SecurityViolation += (e) => Autoban.Incident(e.Client, e.Level, e.Report, e.StackReport);

			// Start
			this.Server.Start(this.Conf.Channel.ChannelPort);

			// Inter
			this.ConnectToLogin(true);
			this.StartStatusUpdateTimer();

			CliUtil.RunningTitle();
			this.IsRunning = true;
			this.IsInMaintenance = false;

			// Commands
			this.ConsoleCommands.Wait();
		}

		/// <summary>
		/// Tries to connect to login server, keeps trying every 10 seconds
		/// till there is a success. Blocking.
		/// </summary>
		public void ConnectToLogin(bool firstTime)
		{
			if (this.LoginServer != null && this.LoginServer.State == ClientState.LoggedIn)
				throw new Exception("Channel already connected to login server.");

			Log.WriteLine();

			if (firstTime)
				Log.Info("Trying to connect to login server at {0}:{1}...", ChannelServer.Instance.Conf.Channel.LoginHost, ChannelServer.Instance.Conf.Channel.LoginPort);
			else
			{
				Log.Info("Trying to re-connect to login server in {0} seconds.", LoginTryTime / 1000);
				Thread.Sleep(LoginTryTime);
			}

			var success = false;
			while (!success)
			{
				try
				{
					if (this.LoginServer != null && this.LoginServer.State != ClientState.Dead)
						this.LoginServer.Kill();

					this.LoginServer = new InternalClient();
					this.LoginServer.Socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
					this.LoginServer.Socket.Connect(ChannelServer.Instance.Conf.Channel.LoginHost, ChannelServer.Instance.Conf.Channel.LoginPort);

					var buffer = new byte[255];

					// Recv Seed, send back empty packet to get done with the challenge.
					this.LoginServer.Socket.Receive(buffer);
					this.LoginServer.Crypto = new MabiCrypto(BitConverter.ToUInt32(buffer, 0), false);
					this.LoginServer.Send(Packet.Empty());

					// Challenge end
					this.LoginServer.Socket.Receive(buffer);

					// Inject login server into normal data receiving.
					this.Server.AddReceivingClient(this.LoginServer);

					// Identify
					this.LoginServer.State = ClientState.LoggingIn;

					success = true;

					Send.Internal_ServerIdentify();
				}
				catch (Exception ex)
				{
					Log.Error("Unable to connect to login server. ({0})", ex.Message);
					Log.Info("Trying again in {0} seconds.", LoginTryTime / 1000);
					Thread.Sleep(LoginTryTime);
				}
			}

			Log.Info("Connection to login server at '{0}' established.", this.LoginServer.Address);
			Log.WriteLine();
		}

        /// <summary>
        /// Attempts to start a shutdown sequence for the server
        /// </summary>
        /// <param name="time">The amount of time in seconds the server should wait before shutting down</param>
        /// <returns></returns>
		public ShutdownResult Shutdown(int time)
		{
			var disconnectOffset = 30;

            var channel = this.ServerList.GetChannel(this.Conf.Channel.ChannelServer, this.Conf.Channel.ChannelName);

            if (channel == null)
            {
                Log.Warning("Attempted to shutdown unregistered channel.");
                return ShutdownResult.Fail;
            }

            if (_shutdownTimer != null && _shutdownTimer.Enabled)
				return ShutdownResult.AlreadyInProgress;

            try
            {
                _shutdownTimer = new System.Timers.Timer(time * 1000);
            }
            catch (ArgumentException ex)
            {
                Log.Error(ex.Message);
                return ShutdownResult.Fail;
            }

            _shutdownTimer.Elapsed += this.ShutdownTimerOnElapsed;

			try
			{
				_shutdownTimer.Start();
			}
			catch (ArgumentOutOfRangeException ex)
			{
				Log.Error(ex.Message);
				return ShutdownResult.Fail;
			}

            Send.Notice(NoticeType.TopRed,
                Localization.Get("Channel will shut down in {0} seconds, please log off as soon as possible."), time);

            // Sets ChannelState to Maint and notifies LoginServer
            this.IsInMaintenance = true;
            Log.Info("Switched to maintenance.");

            // Disconnect a little earlier
            Send.RequestClientDisconnect(time - disconnectOffset);

			Log.Info("Shutting down in {0} seconds...", time);

			return ShutdownResult.Success;
		}

		private void ShutdownTimerOnElapsed(object sender, ElapsedEventArgs elapsedEventArgs)
		{   
			Log.Info("Shutting Down...");

			this.Database.SaveVars("Aura System", 0, this.ScriptManager.GlobalVars.Perm);

			lock (this.Server.Clients)
			{
				foreach (var channelClient in this.Server.Clients)
				{
					channelClient.Kill();                 
				}
			}

			CliUtil.Exit(0, false);
		}

		private void OnClientDisconnected(ChannelClient client)
		{
			if (client == this.LoginServer)
				this.ConnectToLogin(false);
		}

		/// <summary>
		/// Handler for unhandled exceptions.
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
		{
			try
			{
				Log.Error("Oh no! Ferghus escaped his memory block and infected the rest of the server!");
				Log.Error("Aura has encountered an unexpected and unrecoverable error. We're going to try to save as much as we can.");
			}
			catch { }
			try
			{
				this.Server.Stop();
			}
			catch { }
			try
			{
				// save the world
			}
			catch { }
			try
			{
				Log.Exception((Exception)e.ExceptionObject);
				Log.Status("Closing server.");
			}
			catch { }

			CliUtil.Exit(1, false);
		}

		private void StartStatusUpdateTimer()
		{
			this.Events.MinutesTimeTick += (_) =>
			{
				if (this.LoginServer == null || this.LoginServer.State != ClientState.LoggedIn)
					return;

				Send.Internal_ChannelStatus();
			};
		}

		private void InitializeWorld()
		{
			Log.Info("Initializing world...");

			this.World = new WorldManager();
			this.World.Initialize();
			this.PartyManager.Initialize();

			Log.Info("  done loading {0} regions.", this.World.Count);
		}

		private void LoadScripts()
		{
			this.ScriptManager.Init();
			this.ScriptManager.Load();
		}

		private void LoadSkills()
		{
			this.SkillManager.AutoLoad();
		}

		public override void InitDatabase(AuraDb db, BaseConf conf)
		{
			base.InitDatabase(db, conf);

			// If items end up with temp ids in the db we'd get entity ids
			// that exist twice, when creating new temps later on.
			if (ChannelServer.Instance.Database.TmpItemsExist())
			{
				Log.Warning("InitDatabase: Found items with temp entity ids.");
				// TODO: clean up dbs
			}
		}
	}
}
