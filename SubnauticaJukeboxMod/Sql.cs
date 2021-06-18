using System;
using QModManager.Utility;
using System.Data.SQLite;

namespace JukeboxSpotify
{
    public class SQL
    {
        public static SQLiteConnection _conn { get; private set; }

        public SQL()
        {
            _conn = CreateConnection();
            CreateTable();
        }

        public static SQLiteConnection CreateConnection()
        {
            SQLiteConnection conn;

            // Create a new database connection
            conn = new SQLiteConnection(@"URI=file:.\QMods\JukeboxSpotify\jukeboxSpotify.db");

            // Open the connection
            try
            {
                conn.Open();
            } catch(Exception e)
            {
                new ErrorHandler(e, "Something went wrong opening an SQLite connection");
            }

            return conn;
        }

        public static void CreateTable()
        {
            SQLiteCommand cmd = _conn.CreateCommand();

            try
            {
                cmd.CommandText = "CREATE TABLE IF NOT EXISTS Auth (id INTEGER PRIMARY KEY, authorization_code VARCHAR(64) NULL, access_token VARCHAR(64) NULL, refresh_token VARCHAR(64) NULL, expires_in INT NULL)";
                cmd.ExecuteNonQuery();
            } catch(Exception e)
            {
                new ErrorHandler(e, "Error making tables");
            }
            

        }

        public static void queryTable(string query)
        {
            SQLiteCommand cmd = _conn.CreateCommand();

            try
            {
                cmd.CommandText = query;
                cmd.ExecuteNonQuery();
            }
            catch(Exception e)
            {
                new ErrorHandler(e, "Error inserting into tables");
            }
            
        }

        public static string ReadData(string query)
        {
            SQLiteCommand cmd = _conn.CreateCommand();
            cmd.CommandText = query;

            string refreshToken = null;

            var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                refreshToken = reader.GetString(3);
            }

            return refreshToken;
        }
    }
}
