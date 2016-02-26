﻿// Copyright (c) Aura development team - Licensed under GNU GPL
// For more information, see license file in the main folder

using Aura.Shared.Util;
using MySql.Data.MySqlClient;
using System;
using System.Text.RegularExpressions;

namespace Aura.Shared.Database
{
	public class AuraDb
	{
		private string _connectionString;

		private Regex _nameCheckRegex = new Regex(@"^[a-zA-Z][a-z0-9]{2,15}$", RegexOptions.Compiled);

		/// <summary>
		/// Returns a valid connection.
		/// </summary>
		public MySqlConnection Connection
		{
			get
			{
				if (_connectionString == null)
					throw new Exception("AuraDb has not been initialized.");

				var result = new MySqlConnection(_connectionString);
				result.Open();
				return result;
			}
		}

		/// <summary>
		/// Sets connection string and calls TestConnection.
		/// </summary>
		/// <param name="host"></param>
		/// <param name="port"></param>
		/// <param name="user"></param>
		/// <param name="pass"></param>
		/// <param name="db"></param>
		public void Init(string host, int port, string user, string pass, string db)
		{
			_connectionString = string.Format("server={0}; port={1}; database={2}; uid={3}; password={4}; pooling=true; min pool size=0; max pool size=100;", host, port, db, user, pass);
			this.TestConnection();
		}

		/// <summary>
		/// Tests connection, throws on error.
		/// </summary>
		public void TestConnection()
		{
			MySqlConnection conn = null;
			try
			{
				conn = this.Connection;
			}
			finally
			{
				if (conn != null)
					conn.Close();
			}
		}

		// ------------------------------------------------------------------

		/// <summary>
		/// Returns whether the account exists.
		/// </summary>
		/// <param name="accountId"></param>
		/// <returns></returns>
		public bool AccountExists(string accountId)
		{
			using (var conn = this.Connection)
			{
				var mc = new MySqlCommand("SELECT `accountId` FROM `accounts` WHERE `accountId` = @accountId", conn);
				mc.Parameters.AddWithValue("@accountId", accountId);

				using (var reader = mc.ExecuteReader())
					return reader.HasRows;
			}
		}

		/// <summary>
		/// Adds new account to the database.
		/// </summary>
		/// <param name="accountId"></param>
		/// <param name="password"></param>
		/// <param name="points">Starter cash points.</param>
		public void CreateAccount(string accountId, string password, int points)
		{
			password = Password.Hash(password);

			using (var conn = this.Connection)
			using (var cmd = new InsertCommand("INSERT INTO `accounts` {0}", conn))
			{
				cmd.Set("accountId", accountId);
				cmd.Set("password", password);
				cmd.Set("creation", DateTime.Now);
				cmd.Set("points", points);

				cmd.Execute();
			}
		}

		/// <summary>
		/// Adds card to database and returns it as Card.
		/// </summary>
		/// <param name="accountId"></param>
		/// <param name="type"></param>
		/// <param name="race"></param>
		/// <returns></returns>
		public Card AddCard(string accountId, int type, int race)
		{
			using (var conn = this.Connection)
			{
				var mc = new MySqlCommand("INSERT INTO `cards` (`accountId`, `type`, `race`) VALUES (@accountId, @type, @race)", conn);
				mc.Parameters.AddWithValue("@accountId", accountId);
				mc.Parameters.AddWithValue("@type", type);
				mc.Parameters.AddWithValue("@race", race);

				mc.ExecuteNonQuery();

				return new Card(mc.LastInsertedId, type, race);
			}
		}

		/// <summary>
		/// Returns true if the name is valid and available.
		/// </summary>
		/// <param name="name"></param>
		/// <param name="serverName"></param>
		/// <returns></returns>
		public NameCheckResult NameOkay(string name, string serverName)
		{
			if (!_nameCheckRegex.IsMatch(name))
				return NameCheckResult.Invalid;

			using (var conn = this.Connection)
			{
				var mc = new MySqlCommand("SELECT `creatureId` FROM `creatures` WHERE `name` = @name AND `server` = @serverName", conn);
				mc.Parameters.AddWithValue("@name", name);
				mc.Parameters.AddWithValue("@serverName", serverName);

				using (var reader = mc.ExecuteReader())
				{
					if (reader.HasRows)
						return NameCheckResult.Exists;
				}
			}

			return NameCheckResult.Okay;
		}

		/// <summary>
		/// Resets password for account to its name.
		/// </summary>
		/// <param name="accountName"></param>
		/// <param name="password"></param>
		public void SetAccountPassword(string accountName, string password)
		{
			using (var conn = this.Connection)
			using (var mc = new MySqlCommand("UPDATE `accounts` SET `password` = @password WHERE `accountId` = @accountId", conn))
			{
				mc.Parameters.AddWithValue("@accountId", accountName);
				mc.Parameters.AddWithValue("@password", Password.HashRaw(password));

				mc.ExecuteNonQuery();
			}
		}

		/// <summary>
		/// Sets new randomized session key for the account and returns it.
		/// </summary>
		/// <param name="accountId"></param>
		/// <returns></returns>
		public long CreateSession(string accountId)
		{
			using (var conn = this.Connection)
			using (var mc = new MySqlCommand("UPDATE `accounts` SET `sessionKey` = @sessionKey WHERE `accountId` = @accountId", conn))
			{
				var sessionKey = RandomProvider.Get().NextInt64();

				mc.Parameters.AddWithValue("@accountId", accountId);
				mc.Parameters.AddWithValue("@sessionKey", sessionKey);

				mc.ExecuteNonQuery();

				return sessionKey;
			}
		}

		/// <summary>
		/// Returns true if sessionKey is correct for account.
		/// </summary>
		/// <param name="accountId"></param>
		/// <param name="sessionKey"></param>
		/// <returns></returns>
		public bool CheckSession(string accountId, long sessionKey)
		{
			using (var conn = this.Connection)
			using (var mc = new MySqlCommand("SELECT `sessionKey` FROM `accounts` WHERE `accountId` = @accountId AND `sessionKey` = @sessionKey", conn))
			{
				mc.Parameters.AddWithValue("@accountId", accountId);
				mc.Parameters.AddWithValue("@sessionKey", sessionKey);

				using (var reader = mc.ExecuteReader())
					return reader.HasRows;
			}
		}

		/// <summary>
		/// Returns true if account has a character with the given id on the
		/// given server.
		/// </summary>
		/// <param name="accountId"></param>
		/// <param name="entityId"></param>
		/// <param name="server"></param>
		/// <returns></returns>
		public bool AccountHasCharacter(string accountId, long entityId, string server)
		{
			using (var conn = this.Connection)
			using (var mc = new MySqlCommand(
				"SELECT `c`.`entityId` " +
				"FROM `characters` AS `c` " +
				"INNER JOIN `creatures` AS `cr` ON `c`.`creatureId` = `cr`.`creatureId` " +
				"WHERE `accountId` = @accountId AND `entityId` = @entityId AND `server` = @server"
			, conn))
			{
				mc.Parameters.AddWithValue("@accountId", accountId);
				mc.Parameters.AddWithValue("@entityId", entityId);
				mc.Parameters.AddWithValue("@server", server);

				using (var reader = mc.ExecuteReader())
					return reader.HasRows;
			}
		}

		/// <summary>
		/// Returns true if account has a pet with the given id on the
		/// given server.
		/// </summary>
		/// <param name="accountId"></param>
		/// <param name="entityId"></param>
		/// <param name="server"></param>
		/// <returns></returns>
		public bool AccountHasPet(string accountId, long entityId, string server)
		{
			using (var conn = this.Connection)
			using (var mc = new MySqlCommand(
				"SELECT `c`.`entityId` " +
				"FROM `pets` AS `c` " +
				"INNER JOIN `creatures` AS `cr` ON `c`.`creatureId` = `cr`.`creatureId` " +
				"WHERE `accountId` = @accountId AND `entityId` = @entityId AND `server` = @server"
			, conn))
			{
				mc.Parameters.AddWithValue("@accountId", accountId);
				mc.Parameters.AddWithValue("@entityId", entityId);
				mc.Parameters.AddWithValue("@server", server);

				using (var reader = mc.ExecuteReader())
					return reader.HasRows;
			}
		}

		/// <summary>
		/// Changes auth level of account.
		/// </summary>
		/// <param name="accountId"></param>
		/// <param name="level"></param>
		/// <returns></returns>
		public bool ChangeAuth(string accountId, int level)
		{
			using (var conn = this.Connection)
			using (var cmd = new UpdateCommand("UPDATE `accounts` SET {0} WHERE `accountId` = @accountId", conn))
			{
				cmd.AddParameter("@accountId", accountId);
				cmd.Set("authority", level);

				return (cmd.Execute() > 0);
			}
		}

		/// <summary>
		/// Unsets creature's Initialized creature state flag.
		/// </summary>
		/// <param name="creatureId"></param>
		public void UninitializeCreature(long creatureId)
		{
			using (var conn = this.Connection)
			using (var mc = new MySqlCommand("UPDATE `creatures` SET `state` = `state` & ~1 WHERE `creatureId` = @creatureId", conn))
			{
				mc.Parameters.AddWithValue("@creatureId", creatureId);
				mc.ExecuteNonQuery();
			}
		}
	}

	/// <summary>
	/// Extensions for the MySqlDataReader.
	/// </summary>
	public static class MySqlDataReaderExtension
	{
		/// <summary>
		/// Returns true if value at index is null.
		/// </summary>
		/// <param name="reader"></param>
		/// <param name="index"></param>
		/// <returns></returns>
		private static bool IsDBNull(this MySqlDataReader reader, string index)
		{
			return reader.IsDBNull(reader.GetOrdinal(index));
		}

		/// <summary>
		/// Same as GetString, except for a is null check. Returns null if NULL.
		/// </summary>
		/// <param name="reader"></param>
		/// <param name="index"></param>
		/// <returns></returns>
		public static string GetStringSafe(this MySqlDataReader reader, string index)
		{
			if (IsDBNull(reader, index))
				return null;
			else
				return reader.GetString(index);
		}

		/// <summary>
		/// Returns DateTime of the index, or DateTime.MinValue, if value is null.
		/// </summary>
		/// <param name="reader"></param>
		/// <param name="index"></param>
		/// <returns></returns>
		public static DateTime GetDateTimeSafe(this MySqlDataReader reader, string index)
		{
			return reader[index] as DateTime? ?? DateTime.MinValue;
		}
	}

	/// <summary>
	/// Result of NameOkay.
	/// </summary>
	public enum NameCheckResult : byte
	{
		Okay = 0,
		Exists = 1,
		Invalid = 2,
	}
}
