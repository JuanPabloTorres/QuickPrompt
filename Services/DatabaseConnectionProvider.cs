using SQLite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuickPrompt.Services
{
    public class DatabaseConnectionProvider
    {
        private  SQLiteAsyncConnection _instance;

        private const string DatabaseName = "QuickPrompt.db3";

        public  SQLiteAsyncConnection GetConnection()
        {
            if (_instance == null)
            {
                string dbPath = Path.Combine(FileSystem.AppDataDirectory, DatabaseName);

                _instance = new SQLiteAsyncConnection(dbPath);
            }

            return _instance;
        }
    }
}
