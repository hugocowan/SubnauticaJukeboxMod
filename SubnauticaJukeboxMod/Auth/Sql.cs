using System;
using System.Collections.Generic;
using System.Data.SQLite;

namespace JukeboxSpotify
{
    class SQL
    {
        public static SQLiteConnection Conn { get; private set; }

        public SQL()
        {
            Conn = CreateConnection();
            CreateTables();
        }

        public static SQLiteConnection CreateConnection()
        {
            SQLiteConnection conn;

            // Create a new database connection
            conn = new SQLiteConnection(@"URI=file:.\QMods\JukeboxSpotify\JukeboxSpotify.db");

            // Open the connection
            try
            {
                conn.Open();
            }
            catch (Exception e)
            {
                new ErrorHandler(e, "Something went wrong opening an SQLite connection");
            }

            return conn;
        }

        public static void CreateTables()
        {
            SQLiteCommand cmd = Conn.CreateCommand();

            try
            {
                cmd.CommandText = "CREATE TABLE IF NOT EXISTS Auth (id INTEGER PRIMARY KEY, authorization_code VARCHAR(64) NULL, access_token VARCHAR(64) NULL, refresh_token VARCHAR(64) NULL, expires_in INT NULL)";
                cmd.ExecuteNonQuery();
                cmd.CommandText = "CREATE TABLE IF NOT EXISTS Device (id INTEGER PRIMARY KEY, device_id VARCHAR(64) NULL, device_name VARCHAR(64) NULL)";
                cmd.ExecuteNonQuery();
            }
            catch (Exception e)
            {
                new ErrorHandler(e, "Error making tables");
            }


        }

        public static void QueryTable(string query)
        {
            SQLiteCommand cmd = Conn.CreateCommand();

            try
            {
                cmd.CommandText = query;
                cmd.ExecuteNonQuery();
            }
            catch (Exception e)
            {
                new ErrorHandler(e, "Error inserting into tables");
            }

        }

        public static List<string> ReadData(string query, string type = "refreshToken")
        {
            SQLiteCommand cmd = Conn.CreateCommand();
            cmd.CommandText = query;

            List<string> results = null;

            var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                switch (type)
                {
                    case "refreshToken":
                        results = new List<string> { reader.GetString(3) };
                        break;
                    case "device":
                        results = new List<string>() { reader.GetString(1), reader.GetString(2) };
                        break;
                }

            }

            return results;
        }
    }
}
