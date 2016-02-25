﻿// Copyright (c) Aura development team - Licensed under GNU GPL
// For more information, see license file in the main folder

using Aura.Shared;
using Aura.Shared.Database;
using Aura.Shared.Util;
using Aura.Shared.Util.Commands;
using Aura.Web.Controllers;
using Aura.Web.Scripting;
using Aura.Web.Util;
using SharpExpress;
using SharpExpress.Engines;
using System;
using System.Net;

namespace Aura.Web
{
	public class WebServer : ServerMain
	{
		public static readonly WebServer Instance = new WebServer();

		private bool _running = false;

		/// <summary>
		/// Actual web server
		/// </summary>
		public WebApplication App { get; private set; }

		/// <summary>
		/// Database
		/// </summary>
		public AuraDb Database { get; private set; }

		/// <summary>
		/// Configuration
		/// </summary>
		public WebConf Conf { get; private set; }

		/// <summary>
		/// Script manager
		/// </summary>
		public ScriptManager ScriptManager { get; private set; }

		/// <summary>
		/// Initializes fields and properties
		/// </summary>
		private WebServer()
		{
			this.ScriptManager = new ScriptManager();
		}

		/// <summary>
		/// Loads all necessary components and starts the server.
		/// </summary>
		public void Run()
		{
			if (_running)
				throw new Exception("Server is already running.");

			CliUtil.WriteHeader("Web Server", ConsoleColor.DarkRed);
			CliUtil.LoadingTitle();

			this.NavigateToRoot();

			// Conf
			this.LoadConf(this.Conf = new WebConf());

			// Database
			this.InitDatabase(this.Database = new AuraDb(), this.Conf);

			// Localization
			this.LoadLocalization(this.Conf);

			// Server
			this.StartWebServer();

			// Scripts (after web server)
			this.LoadScripts();

			CliUtil.RunningTitle();
			_running = true;

			// Commands
			var commands = new ConsoleCommands();
			commands.Wait();
		}

		/// <summary>
		/// Sets up default controllers and starts web server
		/// </summary>
		public void StartWebServer()
		{
			Log.Info("Starting web server...");

			this.App = new WebApplication();

			this.App.Engine("htm", new HandlebarsEngine());

			this.App.Get("/favicon.ico", new StaticController(this.Conf.Web.Favicon));

			this.App.Static("user/save/");
			this.App.Static("user/resources/");
			this.App.Static("system/web/public/");
			this.App.Static("user/web/public/");

			this.App.Get("/", new MainController());
			this.App.Post("/ui", new UiStorageController());
			this.App.Post("/visual-chat", new VisualChatController());
			this.App.Post("/avatar-upload", new AvatarUploadController());

			try
			{
				this.App.Listen(this.Conf.Web.Port);

				Log.Status("Server ready, listening on 0.0.0.0:{0}.", this.Conf.Web.Port);
			}
			catch (NHttp.NHttpException)
			{
				Log.Error("Failed to start web server.");
				Log.Info("Port {0} might already be in use, make sure no other application, like other web servers or Skype, are using it or set a different port in web.conf.", this.Conf.Web.Port);
				CliUtil.Exit(1);
			}
		}

		/// <summary>
		/// Loads web scripts
		/// </summary>
		private void LoadScripts()
		{
			this.ScriptManager.LoadScripts("system/scripts/scripts_web.txt");
		}
	}
}
