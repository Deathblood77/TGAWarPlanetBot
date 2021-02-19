using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;

namespace TGAWarPlanetBot
{	
	public class Faction
	{
		public static int Unknown = 1;

		public int Id { get; set; }
		public string Name { get; set; }
	} 

	public class User
	{
		public static int Default = 1;

		public int Id { get; set; }
	}

	public class Player
	{
		public int Id { get; set; }
		public string Name { get; set; }
		// Game nickname
		public string UserName { get; set; }
		public ulong DiscordId { get; set; }
		public string GameId { get; set; }
		[JsonIgnore]
		public Faction Faction { get; set; }
		[JsonPropertyName("Faction")]
		public string FactionName { get; set; } // Legacy field for reading data
	}

	public class PlayerDatabase
	{
		public ulong Id { get; set; }
		public string Name { get; set; }
		public List<Player> Players { get; set; }
		[JsonIgnore]
		public List<Faction> Factions { get; set; }
		[JsonIgnore]
		public SQLiteConnection Conn { get; set; }

		public PlayerDatabase()
		{
			Players = new List<Player>();
			Factions = new List<Faction>();
			Conn = null;
		}
	}

	public class PlayerDatabaseService
	{
		private readonly string m_databaseDir = "db/player";
		private Dictionary<ulong, PlayerDatabase> m_databases;

		private void CreateDatabase(SQLiteConnection conn)
		{
			using var cmd = new SQLiteCommand(conn);

			// This is just for testing purpose
			cmd.CommandText = "DROP TABLE IF EXISTS user";
			cmd.ExecuteNonQuery();
			cmd.CommandText = "DROP TABLE IF EXISTS base";
			cmd.ExecuteNonQuery();
			// End test block

			// Create user table
			cmd.CommandText = @"CREATE TABLE user(id INTEGER PRIMARY KEY, name TEXT, discord_id INT)";
			cmd.ExecuteNonQuery();

			// Insert a default user
			cmd.CommandText = @"INSERT INTO user(name) VALUES('DefaultUser')";
			cmd.ExecuteNonQuery();

			// Create base table
			cmd.CommandText = @"CREATE TABLE base(id INTEGER PRIMARY KEY, user_id INT, name TEXT, game_id TEXT, is_farm INT, FOREIGN KEY(user_id) REFERENCES user(id))";
			cmd.ExecuteNonQuery();

			// Create faction table
			cmd.CommandText = @"CREATE TABLE faction(id INTEGER PRIMARY KEY, name TEXT)";
			cmd.ExecuteNonQuery();

			// Insert unknown faction to use as default
			cmd.CommandText = @"INSERT INTO faction(name) VALUES('Unknown')";
			cmd.ExecuteNonQuery();

			// Create base_faction table
			cmd.CommandText = @"CREATE TABLE base_faction(id INTEGER PRIMARY KEY, base_id INT, faction_id INT, FOREIGN KEY(base_id) REFERENCES base(id), FOREIGN KEY(faction_id) REFERENCES faction(id))";
			cmd.ExecuteNonQuery();
		}

		public PlayerDatabaseService()
		{
			Console.WriteLine(Directory.GetCurrentDirectory());
			m_databases = new Dictionary<ulong, PlayerDatabase>();

			// If db directory does not exist we create it
			if (!Directory.Exists(m_databaseDir))
			{
				Directory.CreateDirectory(m_databaseDir);
			}

			// Open json files (This is the legacy path)
			var jsonFiles = Directory.EnumerateFiles(m_databaseDir, "*.json", SearchOption.AllDirectories);

			foreach (string currentFile in jsonFiles)
			{
				var jsonString = File.ReadAllText(currentFile);
				PlayerDatabase database = JsonSerializer.Deserialize<PlayerDatabase>(jsonString);

				// If there is a db file we open a Sqlite connection
				string dbFile = m_databaseDir + "/" + database.Id + ".db";
				bool dbExist = File.Exists(dbFile);

				// We always open the connection
				// If the file does not exist this will create it
				string cs = @"URI=file:" + dbFile;
				var conn = new SQLiteConnection(cs);
				conn.Open();
				database.Conn = conn;

				// If the file did not exist we have a new db so we create the tables
				// If it did exist we read some data
				if (!dbExist)
				{
					CreateDatabase(conn);
				}
				else
				{
					string stm = "SELECT * FROM faction";
					using var cmd = new SQLiteCommand(stm, database.Conn);
					using SQLiteDataReader rdr = cmd.ExecuteReader();

					while (rdr.Read())
					{
						Faction faction = new Faction() { Id = rdr.GetInt32(0), Name = rdr.GetString(1) };
						database.Factions.Add(faction);
					}
				}

				m_databases[database.Id] = database;
			}

			// Open sqlite dbs
			// var dbFiles = Directory.EnumerateFiles(m_databaseDir, "*.db", SearchOption.AllDirectories);

			// foreach (string currentFile in dbFiles)
			// {
			// 	string cs = @"URI=file:" + currentFile;

			// 	var conn = new SQLiteConnection(cs);
			// 	conn.Open();

			// 	ulong databaseId = UInt64.Parse(System.IO.Path.GetFileNameWithoutExtension(currentFile));
			// 	m_connections[databaseId] = conn;
			// }
		}

		public PlayerDatabase GetDatabase(SocketGuild guild)
		{
			PlayerDatabase database;
			ulong databaseId = guild.Id;
			if (!m_databases.ContainsKey(databaseId))
			{
				database = new PlayerDatabase();
				database.Id = databaseId;
				database.Name = guild.Name;
				UpdateDatabase(database);

				// Create database
				string dbFile = m_databaseDir + "/" + database.Id + ".db";
				string cs = @"URI=file:" + dbFile;
				var conn = new SQLiteConnection(cs);
				conn.Open();
				database.Conn = conn;

				CreateDatabase(conn);

				m_databases[databaseId] = database;
			}
			else
			{
				database = m_databases[databaseId];
			}

			return database;
		}

		// public SQLiteConnection GetDatabaseConnection(SocketGuild guild)
		// {
		// 	SQLiteConnection conn = null;
		// 	ulong databaseId = guild.Id;
		// 	if (!m_connections.ContainsKey(databaseId))
		// 	{
		// 		string cs = @"URI=file:" + databaseId + ".db";

		// 		conn = new SQLiteConnection(cs);
		// 		conn.Open();
		// 		m_connections[databaseId] = conn;

		// 		// This will create all the tables
		// 		CreateDatabase(conn);
		// 	}
		// 	else
		// 	{
		// 		conn = m_connections[databaseId];
		// 	}

		// 	return conn;
		// }

		private void UpdateDatabase(PlayerDatabase database)
		{
			var options = new JsonSerializerOptions
			{
				WriteIndented = true
			};

			string jsonFile = m_databaseDir + "/" + database.Id + ".json";
			string jsonText = JsonSerializer.Serialize<PlayerDatabase>(database, options);
			File.WriteAllText(jsonFile, jsonText);
		}

		// public Faction FindFaction(PlayerDatabase database, string name)
		// {
		// 	if (name == "")
		// 	{
		// 		return null;
		// 	}

		// 	string stm = "SELECT * FROM faction WHERE name = " + name;

		// 	using var cmd = new SQLiteCommand(stm, database.Conn);
		// 	using SQLiteDataReader rdr = cmd.ExecuteReader();
		// 	if (rdr.Read())
		// 	{
		// 		return new Faction() { Id = rdr.GetInt32(0), Name = rdr.GetString(1) };
		// 	}
		// 	else
		// 	{
		// 		return null;
		// 	}
		// }

		public IEnumerable<Player> GetPlayers(PlayerDatabase database, Faction faction = null)
		{
			string stm = 
				"SELECT " +
					"base.id AS id, " +
					"base.name AS name, " +
					"base.game_id AS game_id, " +
					"user.name AS username, " +
					"base_faction.faction_id AS faction_id " +
				"FROM base " +
				"INNER JOIN user ON user.id = base.user_id " +
				"INNER JOIN base_faction ON base_faction.base_id = base.id";

			using var cmd = new SQLiteCommand(stm, database.Conn);
			using SQLiteDataReader rdr = cmd.ExecuteReader();

			List<Player> players = new List<Player>();
			while (rdr.Read())
			{
				// Fields: id, user_id, name, game_id, is_farm
				Player player = new Player() { Id = rdr.GetInt32(0), Name = rdr.GetString(1), GameId = rdr.GetString(2), UserName = rdr.GetString(3) };
				int factionId = rdr.GetInt32(4);
				player.Faction = database.Factions.First(f => f.Id == factionId);

				if (faction == null || faction.Id == factionId)
				{
					players.Add(player);
				}
			}

			return players;
			//return database.Players.OrderBy(p => p.Name);
		}

		public Faction AddFaction(PlayerDatabase database, string name)
		{
			Faction faction = new Faction() { Name = name };

			var cmd = new SQLiteCommand(database.Conn);
			cmd.CommandText = @"INSERT INTO faction(name) VALUES('Unknown')";
			cmd.ExecuteNonQuery();

			faction.Id = (Int32)database.Conn.LastInsertRowId;
			database.Factions.Add(faction);

			return faction;
		}

		public Faction FindFaction(PlayerDatabase database, string name)
		{
			return database.Factions.FirstOrDefault(f => f.Name == name);
		}

		public Player AddPlayer(PlayerDatabase database, string name, Faction faction, string id)
		{
			if (faction == null)
			{
				faction = database.Factions.First(f => f.Id == Faction.Unknown);
			}

			Player newPlayer = new Player() { Name = name, GameId = id, Faction = faction };
			//database.Players.Add(newPlayer);

			//UpdateDatabase(database);
			using var cmd = new SQLiteCommand(database.Conn);
			cmd.CommandText = String.Format(@"INSERT INTO base(user_id, name, game_id) VALUES({0}, '{1}', '{2}')", User.Default, name, id);
			cmd.ExecuteNonQuery();

			newPlayer.Id = (Int32)database.Conn.LastInsertRowId;

			// Add faction mapping
			cmd.CommandText = String.Format(@"INSERT INTO base_faction(base_id, faction_id) VALUES({0}, {1})", newPlayer.Id, faction.Id);
			cmd.ExecuteNonQuery();

			return newPlayer;
		}

		public void RemovePlayer(PlayerDatabase database, Player player)
		{
			// database.Players.Remove(player);

			// UpdateDatabase(database);

			// Delete the player
			using var cmd = new SQLiteCommand(database.Conn);
			cmd.CommandText = String.Format(@"DELETE FROM base WHERE id = {0})", player.Id);
			cmd.ExecuteNonQuery();

			// Delete faction mappings
			cmd.CommandText = String.Format(@"DELETE FROM base_faction WHERE base_id = {0})", player.Id);
			cmd.ExecuteNonQuery();
		}

		public void UpdatePlayer(PlayerDatabase database, Player player)
		{
			using var cmd = new SQLiteCommand(database.Conn);
			cmd.CommandText = String.Format(@"Update base SET name = '{0}', game_id = '{1}', faction_id = {2} WHERE id = {3})", player.Name, player.GameId, player.Faction.Id, player.Id);
			cmd.ExecuteNonQuery();
		}

		public List<Player> FindPlayers(PlayerDatabase database, string name)
		{
			string stm = "SELECT * FROM base WHERE name = " + name; // Use inner join instead of all the queries below

			using var cmd = new SQLiteCommand(stm, database.Conn);
			using SQLiteDataReader rdr = cmd.ExecuteReader();

			List<Player> players = new List<Player>();
			while (rdr.Read())
			{
				// Fields: id, user_id, name, game_id, is_farm
				Player player = new Player() { Id = rdr.GetInt32(0), Name = rdr.GetString(2), GameId = rdr.GetString(3) };
				
				// Get username
				cmd.CommandText = @"SELECT name FROM user WHERE id = " + rdr.GetInt32(1);
				SQLiteDataReader rdr2 = cmd.ExecuteReader();
				rdr2.Read();
				player.UserName = rdr2.GetString(0);

				// Get faction id
				cmd.CommandText = @"SELECT faction_id FROM base_faction WHERE base_id = " + rdr.GetInt32(0);
				SQLiteDataReader rdr3 = cmd.ExecuteReader();
				rdr3.Read();
				int factionId = rdr3.GetInt32(0);

				player.Faction = database.Factions.First(f => f.Id == factionId);

				players.Add(player);
			}

			return players;
		}

		public Player FindPlayer(PlayerDatabase database, int id)
		{
			string stm = "SELECT * FROM player WHERE id = " + id;
			var cmd = new SQLiteCommand(stm, database.Conn);
			SQLiteDataReader rdr = cmd.ExecuteReader();
			if (rdr.Read())
			{
				// Fields: id, user_id, name, game_id, is_farm
				Player player = new Player() { Id = rdr.GetInt32(0), Name = rdr.GetString(2), GameId = rdr.GetString(3) };
				
				// Get username
				cmd.CommandText = @"SELECT name FROM user WHERE id = " + rdr.GetInt32(1);
				SQLiteDataReader rdr2 = cmd.ExecuteReader();
				rdr2.Read();
				player.UserName = rdr2.GetString(0);

				// Get faction id
				cmd.CommandText = @"SELECT faction_id FROM base_faction WHERE base_id = " + rdr.GetInt32(0);
				SQLiteDataReader rdr3 = cmd.ExecuteReader();
				rdr3.Read();
				int factionId = rdr3.GetInt32(0);

				player.Faction = database.Factions.First(f => f.Id == factionId);
				
				return player;
			}
			else
			{
				return null;
			}
		}

		public List<Player> FindPlayerFromGameId(PlayerDatabase database, string gameId)
		{
			return database.Players.FindAll(x => x.GameId == gameId);
		}

		public bool ConnectPlayer(PlayerDatabase database, Player player, SocketUser user)
		{
			player.DiscordId = user.Id;

			UpdateDatabase(database);
			return true;
		}
	}

	[Group("player")]
	public class PlayerModule : ModuleBase<SocketCommandContext>
	{
		private readonly PlayerDatabaseService m_databaseService;
		public PlayerModule(PlayerDatabaseService databaseService)
		{
			m_databaseService = databaseService;
		}

		// !player
		[Command]
		public async Task DefaultAsync()
		{
			var sb = new System.Text.StringBuilder();
			sb.Append("```");
			sb.Append("usage: !player <command> [<args>]\n\n");
			sb.Append(String.Format("\t{0,-15} {1}\n", "add", "Add player with given name and properties."));
			sb.Append(String.Format("\t{0,-15} {1}\n", "connect", "Connect a player to a discord user."));
			sb.Append(String.Format("\t{0,-15} {1}\n", "set", "Set name, game id and faction for given player."));
			sb.Append(String.Format("\t{0,-15} {1}\n", "setid", "Set the game id for given player."));
			sb.Append(String.Format("\t{0,-15} {1}\n", "setname", "Set the game name for given player."));
			sb.Append(String.Format("\t{0,-15} {1}\n", "setfaction", "Set faction for the given player."));
			sb.Append(String.Format("\t{0,-15} {1}\n", "list", "List all players."));
			sb.Append(String.Format("\t{0,-15} {1}\n", "whois", "Display information for the given player."));
			sb.Append(String.Format("\t{0,-15} {1}\n", "search", "Same as whois."));
			sb.Append("```");
			await ReplyAsync(sb.ToString());
		}

		// !player details
		[Command("details")]
		[Summary("List all players with full details.")]
		public async Task DetailsAsync()
		{
			PlayerDatabase database = m_databaseService.GetDatabase(Context.Guild);
			var players = m_databaseService.GetPlayers(database);

			// We have a limit for message size so we send one message for every 20 players
			for (int i = 0; i < players.Count(); i += 20)
			{
				var sb = new System.Text.StringBuilder();
				sb.Append("```");
				sb.Append(String.Format("{0,-3} {1,-20} {2,-20} {3, -15} {4, -5}\n", "Id", "Name", "User", "GameId", "Faction"));
				sb.Append("-------------------------------------------------------------------\n");
				foreach (var player in players.Skip(i).Take(20))
				{
					string userName = player.UserName != null ? player.UserName : "<N/A>";
					string gameId = player.GameId != null ? player.GameId : "<N/A>";
					sb.Append(String.Format("{0,-3} {1,-20} {2,-20} {3, -15} {4, -5}\n", player.Id, player.Name, userName, gameId, player.Faction.Name));
				}
				sb.Append("```");

				await ReplyAsync(sb.ToString());
			}
		}

		private string GetPlayerList(IEnumerable<Player> players)
		{
			var sb = new System.Text.StringBuilder();
			sb.Append("```");
			sb.Append(String.Format("{0,-20} {1, -5}\n", "Name", "Faction"));
			sb.Append("----------------------------\n");
			foreach (var player in players)
			{
				sb.Append(String.Format("{0,-20} {1, -5}\n", player.Name, player.Faction.Name));
			}
			sb.Append("```");
			return sb.ToString();
		}

		// !player list
		[Command("list")]
		[Summary("List all players.")]
		public async Task ListAsync(string factionName = "")
		{
			PlayerDatabase database = m_databaseService.GetDatabase(Context.Guild);

			Faction faction = null;
			if (factionName.Length > 0)
			{
				faction = database.Factions.FirstOrDefault(f => f.Name == factionName);
				if (faction == null)
				{
					var sb = new System.Text.StringBuilder();
					sb.Append("```");
					sb.Append("Faction with name '" + factionName + "' not found!");
					sb.Append("```");
					await ReplyAsync(sb.ToString());
				}
			}

			var players = m_databaseService.GetPlayers(database, faction);

			// We have a limit for message size so we send one message for every 20 players
			for (int i = 0; i < players.Count(); i += 20)
			{
				var sb = new System.Text.StringBuilder();
				sb.Append("```");
				sb.Append(String.Format("{0,-20} {1, -5}\n", "Name", "Faction"));
				sb.Append("----------------------------\n");
				foreach (var player in players.Skip(i).Take(20))
				{
					sb.Append(String.Format("{0,-20} {1, -5}\n", player.Name, player.Faction.Name));
				}
				sb.Append("```");
				await ReplyAsync(sb.ToString());
			}
		}

		// !player addFaction Name
		[RequireUserPermission(GuildPermission.ManageNicknames)]
		[Command("addFaction")]
		[Summary("Add new faction.")]
		public async Task AddFactionAsync(string name = "")
		{
			if (name.Length == 0 || name.StartsWith("!"))
			{
				var sb = new System.Text.StringBuilder();
				sb.Append("```");
				sb.Append("usage: !player addFaction <name>");
				sb.Append("```");
				await ReplyAsync(sb.ToString());
			}
			else
			{
				PlayerDatabase database = m_databaseService.GetDatabase(Context.Guild);
				// Make sure we do not add players that already exist
				Faction faction = database.Factions.FirstOrDefault(f => f.Name == name);
				if (faction == null)
				{
					m_databaseService.AddFaction(database, name);
					await ReplyAsync("Faction " + name + " created!");
				}
				else
				{
					await ReplyAsync("Faction " + name + " already exist!");
				}
			}
		}

		// !player add Name
		[RequireUserPermission(GuildPermission.ManageNicknames)]
		[Command("add")]
		[Summary("Add new player.")]
		public async Task AddAsync(string name = "", string id = "<N/A>", string factionName = "")
		{
			if (name.Length == 0 || name.StartsWith("!"))
			{
				var sb = new System.Text.StringBuilder();
				sb.Append("```");
				sb.Append("usage: !player add <name> [<game-id>] [<faction>]");
				sb.Append("```");
				await ReplyAsync(sb.ToString());
			}
			else
			{
				PlayerDatabase database = m_databaseService.GetDatabase(Context.Guild);

				Faction faction = null;
				if (factionName.Length > 0)
				{
					faction = m_databaseService.FindFaction(database, factionName);
					if (faction == null)
					{
						var sb = new System.Text.StringBuilder();
						sb.Append("```");
						sb.Append("Failed to find faction " + factionName);
						sb.Append("```");
						await ReplyAsync(sb.ToString());
					}
				}
				m_databaseService.AddPlayer(database, name, faction, id);
				await ReplyAsync("Player " + name + " created!");
			}
		}

		// !player add Name
		[RequireUserPermission(GuildPermission.Administrator)]
		[Command("remove")]
		[Summary("Remove existing player.")]
		public async Task RemoveAsync(int id = -1)
		{
			if (id < 0)
			{
				var sb = new System.Text.StringBuilder();
				sb.Append("```");
				sb.Append("usage: !player remove <id>");
				sb.Append("```");
				await ReplyAsync(sb.ToString());
			}
			else
			{
				PlayerDatabase database = m_databaseService.GetDatabase(Context.Guild);
				Player matchingPlayer = m_databaseService.FindPlayer(database, id);
				if (matchingPlayer != null)
				{
					m_databaseService.RemovePlayer(database, matchingPlayer);
					await ReplyAsync($"Removed player {matchingPlayer.Name} with id {id}");
				}
				else
				{
					await ReplyAsync($"Failed to find player with id {id}");
				}
			}
		}

		// !player connect Name DiscordUser
		[RequireUserPermission(GuildPermission.ManageNicknames)]
		[Command("connect")]
		[Summary("Connect player to discord id.")]
		public async Task ConnectAsync(string name = "", SocketUser user = null)
		{
			if (name.Length == 0 || user == null)
			{
				var sb = new System.Text.StringBuilder();
				sb.Append("```");
				sb.Append("usage: !player connect <name> <@discord-user>");
				sb.Append("```");
				await ReplyAsync(sb.ToString());
				return;
			}

			PlayerDatabase database = m_databaseService.GetDatabase(Context.Guild);
			List<Player> matchingPlayers = m_databaseService.FindPlayers(database, name);
			if (matchingPlayers.Count > 0)
			{
				m_databaseService.ConnectPlayer(database, matchingPlayers[0], user);
				await ReplyAsync($"Connected {name} -> {user.Username}#{user.Discriminator}");
			}
			else
			{
				await ReplyAsync($"Failed to find player with name {name}");
			}
		}

		// !player set Name GameId Faction
		[RequireUserPermission(GuildPermission.ManageNicknames)]
		[Command("set")]
		[Summary("Set game id and faction for player.")]
		public async Task SetAsync(int id = -1, string name = "", string gameId = "", string factionName = "")
		{
			if (id < 0 || name.Length == 0 || gameId.Length == 0 || factionName.Length == 0)
			{
				var sb = new System.Text.StringBuilder();
				sb.Append("```");
				sb.Append("usage: !player set <id> <name> <game-id> <faction>");
				sb.Append("```");
				await ReplyAsync(sb.ToString());
				return;
			}

			PlayerDatabase database = m_databaseService.GetDatabase(Context.Guild);
			Player matchingPlayer = m_databaseService.FindPlayer(database, id);
			if (matchingPlayer != null)
			{
				Faction faction = m_databaseService.FindFaction(database, factionName);
				if (faction == null)
				{
					var sb = new System.Text.StringBuilder();
					sb.Append("```");
					sb.Append("Failed to find faction " + factionName);
					sb.Append("```");
					await ReplyAsync(sb.ToString());
				}

				matchingPlayer.Name = name;
				matchingPlayer.GameId = gameId;
				matchingPlayer.Faction = faction;
				//m_databaseService.UpdateDatabase(database);
				m_databaseService.UpdatePlayer(database, matchingPlayer);
				await ReplyAsync($"Updated player {matchingPlayer.Name} with id {id}");
			}
			else
			{
				await ReplyAsync($"Failed to find player with id {id}");
			}
		}

		// !player setid Name GameId
		[RequireUserPermission(GuildPermission.ManageNicknames)]
		[Command("setid")]
		[Summary("Set game id for player.")]
		public async Task SetIdAsync(int id = -1, string gameId = "")
		{
			if (id < 0 || gameId.Length == 0)
			{
				var sb = new System.Text.StringBuilder();
				sb.Append("```");
				sb.Append("usage: !player setid <id> <game-id>");
				sb.Append("```");
				await ReplyAsync(sb.ToString());
				return;
			}

			PlayerDatabase database = m_databaseService.GetDatabase(Context.Guild);
			Player matchingPlayer = m_databaseService.FindPlayer(database, id);
			if (matchingPlayer == null)
			{
				matchingPlayer.GameId = gameId;
				m_databaseService.UpdatePlayer(database, matchingPlayer);
				//m_databaseService.UpdateDatabase(database);
				await ReplyAsync($"Set game id of {id} -> {gameId}");
			}
			else
			{
				await ReplyAsync($"Failed to find player with id {id}");
			}
		}

		// !player setname Name GameName
		[RequireUserPermission(GuildPermission.ManageNicknames)]
		[Command("setname")]
		[Summary("Set name for player.")]
		public async Task SetNameAsync(int id = -1, string name = "")
		{
			if (id < 0 || name.Length == 0)
			{
				var sb = new System.Text.StringBuilder();
				sb.Append("```");
				sb.Append("usage: !player setname <id> <name>");
				sb.Append("```");
				await ReplyAsync(sb.ToString());
				return;
			}

			PlayerDatabase database = m_databaseService.GetDatabase(Context.Guild);
			Player matchingPlayer = m_databaseService.FindPlayer(database, id);
			if (matchingPlayer != null)
			{
				matchingPlayer.Name = name;
				//m_databaseService.UpdateDatabase(database);
				m_databaseService.UpdatePlayer(database, matchingPlayer);
				await ReplyAsync($"Set name of {id} -> {name}");
			}
			else
			{
				await ReplyAsync($"Failed to find player with id {id}");
			}
		}

		// !player setfaction Name Faction
		[RequireUserPermission(GuildPermission.ManageNicknames)]
		[Command("setfaction")]
		[Summary("Set faction for player.")]
		public async Task SetFactionAsync(int id = -1, string factionName = "")
		{
			if (id < 0 || factionName.Length == 0)
			{
				var sb = new System.Text.StringBuilder();
				sb.Append("```");
				sb.Append("usage: !player setfaction <id> <faction>");
				sb.Append("```");
				await ReplyAsync(sb.ToString());
				return;
			}

			PlayerDatabase database = m_databaseService.GetDatabase(Context.Guild);
			Player matchingPlayer = m_databaseService.FindPlayer(database, id);
			if (matchingPlayer != null)
			{
				Faction faction = m_databaseService.FindFaction(database, factionName);
				if (faction == null)
				{
					var sb = new System.Text.StringBuilder();
					sb.Append("```");
					sb.Append("Failed to find faction " + factionName);
					sb.Append("```");
					await ReplyAsync(sb.ToString());
				}

				matchingPlayer.Faction = faction;
				//m_databaseService.UpdateDatabase(database);
				m_databaseService.UpdatePlayer(database, matchingPlayer);
				await ReplyAsync($"Set faction of {id} -> {faction}");
			}
			else
			{
				await ReplyAsync($"Failed to find player with id {id}");
			}
		}

		// !player whois Name/GameId
		[Command("whois")]
		[Alias("search")]
		[Summary("Get player matching given name or it.")]
		public async Task WhoIsAsync(string nameOrId = "")
		{
			if (nameOrId.Length == 0)
			{
				var sb = new System.Text.StringBuilder();
				sb.Append("```");
				sb.Append("usage: !player whois <name>\n");
				sb.Append("usage: !player whois <game-id>\n");
				sb.Append("usage: !player search <name>\n");
				sb.Append("usage: !player search <game-id>");
				sb.Append("```");
				await ReplyAsync(sb.ToString());
				return;
			}

			PlayerDatabase database = m_databaseService.GetDatabase(Context.Guild);

			// Try to find player with name and if that fails from game id
			List<Player> matchingPlayers = m_databaseService.FindPlayers(database, nameOrId);
			if (matchingPlayers.Count == 0)
			{
				matchingPlayers = m_databaseService.FindPlayerFromGameId(database, nameOrId);
			}

			if (matchingPlayers.Count > 0)
			{
				bool first = true;
				var sb = new System.Text.StringBuilder();
				sb.Append("```");
				foreach (var player in matchingPlayers)
				{
					if (first)
					{
						first = false;
					}
					else
					{
						sb.Append("-----------------------\n");
					}
					sb.Append(String.Format("{0,-15} {1,-20}\n", "Name:", player.Name));
					string userName = player.UserName != null ? player.UserName : "<N/A>";
					sb.Append(String.Format("{0,-15} {1,-20}\n", "User:", userName));
					string gameId = player.GameId != null ? player.GameId : "<N/A>";
					sb.Append(String.Format("{0,-15} {1,-20}\n", "GameId:", gameId));
					sb.Append(String.Format("{0,-15} {1,-20}\n", "Faction:", player.Faction.Name));
				}
				sb.Append("```");
				await ReplyAsync(sb.ToString());
			}
			else
			{
				await ReplyAsync($"Failed to find player matching {nameOrId}");
			}
		}
	}
}