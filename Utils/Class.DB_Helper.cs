using System;
using System.Data;
using System.Data.SqlClient;
using System.Reflection;

namespace CDR
{
    static class DB_Helper
    {
        private const string MSSQLCONNECTION_STRING = "Server={0};Initial Catalog={1};User ID={2};Password={3};Application Name={4};Connection Timeout=15;";

#if DEBUG
        public static string mssqlServer = "";
        public static string mssqlDB = "";
        public static string mssqlUser = "";
        public static string mssqlPassword = "";
#else
        public static string mssqlServer = "";
        public static string mssqlDB = "";
        private static string mssqlUser = "";
        private static string mssqlPassword = "";
#endif

        private static SqlConnection msSqlConnection;

        public static SqlConnection MSSQLConnection
        {
            get
            {
                if (msSqlConnection == null || msSqlConnection.State != ConnectionState.Open)
                {
                    msSqlConnection = NewMSSQLConnection();
                }

                return msSqlConnection;
            }
        }


        // De nieuwe Image database
        public static SqlConnection NewMSSQLConnection()
        {
            SqlConnection connection = new SqlConnection();
            try
            {
                if (mssqlServer.Length > 0 && mssqlDB.Length > 0 && mssqlUser.Length > 0)
                {
                    string appName = Assembly.GetExecutingAssembly().FullName.Split(',')[0];
                    Version version = Assembly.GetExecutingAssembly().GetName().Version;
                    string connectionString = string.Format(MSSQLCONNECTION_STRING, mssqlServer, mssqlDB, mssqlUser, mssqlPassword, String.Format("{0} {1:0}.{2:00}.{3:0000}.{4:0000}", appName, version.Major, version.Minor, version.Build, version.Revision));

                    connection.ConnectionString = connectionString;
                    connection.Open();
                }
            }
            catch { }

            if (connection.State != ConnectionState.Open)
            {
                connection = null;
            }

            return connection;
        }
    }
}
