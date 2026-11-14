using System;
using System.Data;
using System.Data.OleDb;
using System.IO;
using System.Security.Cryptography;

namespace Activity3
{
    public static class Database
    {
        public static readonly string DBPATH = ResolveDatabasePath();

        private static string ResolveDatabasePath()
        {
            string projectDatabase = Path.GetFullPath(
                Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "sampledb.accdb"));

            if (File.Exists(projectDatabase))
                return projectDatabase;

            string besideApp = Path.Combine(AppContext.BaseDirectory, "sampledb.accdb");
            if (File.Exists(besideApp))
                return besideApp;

            throw new FileNotFoundException("The Access database was not found.", projectDatabase);
        }

        private static string ConnectionString =>
            $@"Provider=Microsoft.ACE.OLEDB.12.0;Data Source={DBPATH};Persist Security Info=False;";

        public static OleDbConnection GetConnection() => new OleDbConnection(ConnectionString);

        public static void Initialize()
        {
            string? folder = Path.GetDirectoryName(DBPATH);
            if (!string.IsNullOrEmpty(folder))
                Directory.CreateDirectory(folder);

            if (!File.Exists(DBPATH))
                throw new FileNotFoundException(
                    "The Access database was not found. Place sampledb.accdb in the project folder.", DBPATH);

            using OleDbConnection connection = GetConnection();
            connection.Open();

            if (!TableExists(connection, "Users"))
            {
                using OleDbCommand create = connection.CreateCommand();
                create.CommandText =
                    "CREATE TABLE Users (" +
                    "ID COUNTER CONSTRAINT PK_Users PRIMARY KEY, " +
                    "Username TEXT(50) NOT NULL, " +
                    "PasswordHash TEXT(255) NOT NULL, " +
                    "FullName TEXT(100) NOT NULL, " +
                    "UserRole TEXT(30), " +
                    "isActive YESNO)";
                create.ExecuteNonQuery();
            }

            // Creates the inventory table automatically if it does not exist yet.
            if (!TableExists(connection, "Inventory"))
            {
                using OleDbCommand create = connection.CreateCommand();
                create.CommandText =
                    "CREATE TABLE Inventory (" +
                    "ID COUNTER CONSTRAINT PK_Inventory PRIMARY KEY, " +
                    "ProductName TEXT(100) NOT NULL, " +
                    "Category TEXT(50), " +
                    "UnitPrice CURRENCY, " +
                    "Description TEXT(255), " +
                    "Quantity LONG NOT NULL, " +
                    "ReorderLevel LONG NOT NULL, " +
                    "CreatedAt DATETIME)";
                create.ExecuteNonQuery();
            }

            // Add CreatedAt to databases created by earlier versions of this project.
            if (!ColumnExists(connection, "Inventory", "CreatedAt"))
            {
                using OleDbCommand alter = connection.CreateCommand();
                alter.CommandText = "ALTER TABLE Inventory ADD COLUMN CreatedAt DATETIME";
                alter.ExecuteNonQuery();
            }

            // Give older inventory records a timestamp so the new column is not blank.
            using (OleDbCommand backfill = connection.CreateCommand())
            {
                backfill.CommandText = "UPDATE Inventory SET CreatedAt = Now() WHERE CreatedAt IS NULL";
                backfill.ExecuteNonQuery();
            }
        }

        private static bool ColumnExists(OleDbConnection connection, string tableName, string columnName)
        {
            DataTable columns = connection.GetSchema("Columns", new string?[] { null, null, tableName, columnName });
            return columns.Rows.Count > 0;
        }

        private static bool TableExists(OleDbConnection connection, string tableName)
        {
            DataTable tables = connection.GetSchema("Tables");
            foreach (DataRow row in tables.Rows)
            {
                if (string.Equals(Convert.ToString(row["TABLE_NAME"]), tableName,
                    StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        public static DataTable GetInventory(string search = "")
        {
            using OleDbConnection connection = GetConnection();
            connection.Open();
            using OleDbCommand command = connection.CreateCommand();

            if (string.IsNullOrWhiteSpace(search))
            {
                command.CommandText = "SELECT ID, ProductName, Category, UnitPrice, Description, Quantity, ReorderLevel, CreatedAt FROM Inventory ORDER BY ID DESC";
            }
            else
            {
                command.CommandText = "SELECT ID, ProductName, Category, UnitPrice, Description, Quantity, ReorderLevel, CreatedAt " +
                                      "FROM Inventory WHERE ProductName LIKE ? OR Category LIKE ? ORDER BY ID DESC";
                string value = "%" + search.Trim() + "%";
                command.Parameters.Add("?", OleDbType.VarWChar, 100).Value = value;
                command.Parameters.Add("?", OleDbType.VarWChar, 50).Value = value;
            }

            using OleDbDataAdapter adapter = new OleDbDataAdapter(command);
            DataTable table = new DataTable();
            adapter.Fill(table);
            return table;
        }

        public static void AddInventory(string productName, string category, decimal unitPrice,
            string description, int quantity, int reorderLevel)
        {
            using OleDbConnection connection = GetConnection();
            connection.Open();
            using OleDbCommand command = connection.CreateCommand();
            command.CommandText =
                "INSERT INTO Inventory (ProductName, Category, UnitPrice, Description, Quantity, ReorderLevel, CreatedAt) " +
                "VALUES (?, ?, ?, ?, ?, ?, Now())";
            AddInventoryParameters(command, productName, category, unitPrice, description, quantity, reorderLevel);
            command.ExecuteNonQuery();
        }

        public static void UpdateInventory(int id, string productName, string category, decimal unitPrice,
            string description, int quantity, int reorderLevel)
        {
            using OleDbConnection connection = GetConnection();
            connection.Open();
            using OleDbCommand command = connection.CreateCommand();
            command.CommandText =
                "UPDATE Inventory SET ProductName=?, Category=?, UnitPrice=?, Description=?, Quantity=?, ReorderLevel=? " +
                "WHERE ID=?";
            AddInventoryParameters(command, productName, category, unitPrice, description, quantity, reorderLevel);
            command.Parameters.Add("?", OleDbType.Integer).Value = id;
            command.ExecuteNonQuery();
        }

        private static void AddInventoryParameters(OleDbCommand command, string productName, string category,
            decimal unitPrice, string description, int quantity, int reorderLevel)
        {
            command.Parameters.Add("?", OleDbType.VarWChar, 100).Value = productName.Trim();
            command.Parameters.Add("?", OleDbType.VarWChar, 50).Value = category.Trim();
            command.Parameters.Add("?", OleDbType.Currency).Value = unitPrice;
            command.Parameters.Add("?", OleDbType.VarWChar, 255).Value = description.Trim();
            command.Parameters.Add("?", OleDbType.Integer).Value = quantity;
            command.Parameters.Add("?", OleDbType.Integer).Value = reorderLevel;
        }

        public static void DeleteInventory(int id)
        {
            using OleDbConnection connection = GetConnection();
            connection.Open();
            using OleDbCommand command = connection.CreateCommand();
            command.CommandText = "DELETE FROM Inventory WHERE ID=?";
            command.Parameters.Add("?", OleDbType.Integer).Value = id;
            command.ExecuteNonQuery();
        }

        public static void GetInventoryCounts(out int total, out int inStock, out int lowStock, out int outOfStock)
        {
            using OleDbConnection connection = GetConnection();
            connection.Open();
            using OleDbCommand command = connection.CreateCommand();
            command.CommandText =
                "SELECT Count(*) AS TotalItems, " +
                "Sum(IIf(Quantity > ReorderLevel, 1, 0)) AS InStock, " +
                "Sum(IIf(Quantity > 0 AND Quantity <= ReorderLevel, 1, 0)) AS LowStock, " +
                "Sum(IIf(Quantity = 0, 1, 0)) AS OutOfStock " +
                "FROM Inventory";

            using OleDbDataReader reader = command.ExecuteReader();
            if (reader.Read())
            {
                total = ToInt(reader["TotalItems"]);
                inStock = ToInt(reader["InStock"]);
                lowStock = ToInt(reader["LowStock"]);
                outOfStock = ToInt(reader["OutOfStock"]);
            }
            else
            {
                total = inStock = lowStock = outOfStock = 0;
            }
        }

        private static int ToInt(object value) => value == DBNull.Value ? 0 : Convert.ToInt32(value);

        public static bool UsernameExists(string username)
        {
            using OleDbConnection connection = GetConnection();
            connection.Open();
            using OleDbCommand command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM Users WHERE Username = ?";
            command.Parameters.Add("?", OleDbType.VarWChar, 50).Value = username.Trim();
            return Convert.ToInt32(command.ExecuteScalar()) > 0;
        }

        public static void RegisterUser(string fullName, string username, string password)
        {
            string passwordHash = HashPassword(password);
            using OleDbConnection connection = GetConnection();
            connection.Open();
            using OleDbCommand command = connection.CreateCommand();
            command.CommandText =
                "INSERT INTO Users (Username, PasswordHash, FullName, UserRole, isActive) VALUES (?, ?, ?, ?, ?)";
            command.Parameters.Add("?", OleDbType.VarWChar, 50).Value = username.Trim();
            command.Parameters.Add("?", OleDbType.VarWChar, 255).Value = passwordHash;
            command.Parameters.Add("?", OleDbType.VarWChar, 100).Value = fullName.Trim();
            command.Parameters.Add("?", OleDbType.VarWChar, 30).Value = "User";
            command.Parameters.Add("?", OleDbType.Boolean).Value = true;
            int rows = command.ExecuteNonQuery();
            if (rows != 1)
                throw new InvalidOperationException("The account was not inserted into the Users table.");
        }

        public static bool ValidateLogin(string username, string password)
        {
            using OleDbConnection connection = GetConnection();
            connection.Open();
            using OleDbCommand command = connection.CreateCommand();
            command.CommandText = "SELECT PasswordHash, isActive FROM Users WHERE Username = ?";
            command.Parameters.Add("?", OleDbType.VarWChar, 50).Value = username.Trim();
            using OleDbDataReader reader = command.ExecuteReader();
            if (!reader.Read()) return false;
            bool active = reader["isActive"] != DBNull.Value && Convert.ToBoolean(reader["isActive"]);
            if (!active) return false;
            string storedHash = Convert.ToString(reader["PasswordHash"]) ?? "";
            return VerifyPassword(password, storedHash);
        }

        private static string HashPassword(string password)
        {
            byte[] salt = RandomNumberGenerator.GetBytes(16);
            byte[] hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, 100_000, HashAlgorithmName.SHA256, 32);
            return Convert.ToBase64String(salt) + "." + Convert.ToBase64String(hash);
        }

        private static bool VerifyPassword(string password, string storedValue)
        {
            try
            {
                string[] parts = storedValue.Split('.');
                if (parts.Length != 2) return false;
                byte[] salt = Convert.FromBase64String(parts[0]);
                byte[] expected = Convert.FromBase64String(parts[1]);
                byte[] actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, 100_000, HashAlgorithmName.SHA256, expected.Length);
                return CryptographicOperations.FixedTimeEquals(actual, expected);
            }
            catch (FormatException) { return false; }
        }
    }
}
