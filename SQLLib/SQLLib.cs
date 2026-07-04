using System;
using System.Collections.Generic;
using System.Data;
#if !SQLLIB_USE_LEGACY_NAMESPACE
using Microsoft.Data.SqlClient;
#else
using System.Data.SqlClient;
#endif
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Globalization;
using System.Text;
using System.IO;

namespace Vulpes.Library
{
    #region DataReader Exception

    [Serializable]
    public class VulpesSqlDataReaderConflictException : Exception
    {
        public StackTrace LastStackTrace;
        public SqlDataReader CurrentOpenDR;
        public override string ToString()
        {
            return (Message + "\r\nLast OP stack: " + LastStackTrace.ToString());
        }

        public VulpesSqlDataReaderConflictException(string message) : base(message) { }
    }

    #endregion

    public partial class SQLLib : IDisposable
    {
        private SqlConnection Connection = null;
        private SqlDataReader RunningDR = null;
        private StackTrace RunningDRStack = null;
        private SqlTransaction trans = null;
        public bool SEHError = true;
        public bool ConnectionPooling = true;
        private bool SQLTransaction = false;
        public int ConnectRetryCount = 5;
        public int ConnectRetryInterval = 30;
        public int ConnectTimeout = 180;
        public int SqlCommandTimeout = 600;
        public string ApplicationName
        {
            get { return (appname); }
            set
            {
                appname = value.Trim();
            }
        }
        private string appname = "";
        public bool isSQLTransactional
        {
            get
            {
                return (SQLTransaction);
            }
        }

        public void Dispose()
        {
            try
            {
                if (Connection != null)
                    Connection.Close();
            }
            catch (Exception ee)
            {
                Debug.WriteLine(ee.ToString());
            }
        }

        public void CloseConnection()
        {
            Debug.WriteLine("SQLCONNET: CLOSE ===============");
            try
            {
                Connection.Close();
            }
            catch
            {
            }
        }

        public SqlConnection connection
        {
            get
            {
                return (Connection);
            }
        }

        public void BeginTransaction()
        {
            trans = Connection.BeginTransaction();
            SQLTransaction = true;
        }

        public void CommitTransaction()
        {
            if (SQLTransaction == true)
                trans.Commit();
            SQLTransaction = false;
        }

        public void RollBackTransaction()
        {
            if (SQLTransaction == true)
                trans.Rollback();
            SQLTransaction = false;
        }


        public bool ConnectLocalDatabaseBlank()
        {
            Debug.WriteLine("SQLCONNECT (LOCAL blank)");
#if !DEBUGDB
            try
            {
#endif
                SqlConnectionStringBuilder conn = new SqlConnectionStringBuilder();
                if (string.IsNullOrWhiteSpace(appname) == false)
                    conn["Application Name"] = appname.Trim();
                conn["Connection Timeout"] = ConnectTimeout;
                conn["Data Source"] = "(LocalDB)\\v11.0";
                conn["Integrated Security"] = "SSPI";
                conn["Pooling"] = ConnectionPooling == true ? "true" : "false";
                conn["ConnectRetryCount"] = ConnectRetryCount;
                conn["ConnectRetryInterval"] = ConnectRetryInterval;
                Connection = new SqlConnection(conn.ConnectionString);
                Connection.Open();
#if !DEBUGDB
            }
            catch (Exception ee)
            {
                Debug.WriteLine(ee.ToString());
                if (SEHError == true)
                    throw;
                return (false);
            }
#endif
            return (true);
        }

        public bool ConnectLocalDatabase(string Filename)
        {
            Debug.WriteLine("SQLCONNECT (LOCAL): " + Filename);
#if !DEBUGDB
            try
            {
#endif
                if (Filename == "")
                    throw new Exception("Filename missing");
                SqlConnectionStringBuilder conn = new SqlConnectionStringBuilder();
                if (string.IsNullOrWhiteSpace(appname) == false)
                    conn["Application Name"] = appname.Trim();
                conn["Connection Timeout"] = ConnectTimeout;
                conn["Data Source"] = "(LocalDB)\\v11.0";
                conn["AttachDbFileName"] = Filename;
                conn["Integrated Security"] = "SSPI";
                conn["Pooling"] = ConnectionPooling == true ? "true" : "false";
                conn["ConnectRetryCount"] = ConnectRetryCount;
                conn["ConnectRetryInterval"] = ConnectRetryInterval;
                Connection = new SqlConnection(conn.ConnectionString);
                Connection.Open();
#if !DEBUGDB
            }
            catch (Exception ee)
            {
                Debug.WriteLine(ee.ToString());
                if (SEHError == true)
                    throw;
                return (false);
            }
#endif
            return (true);
        }

        public string OutputParameters(SQLParam[] parameters)
        {
#if DEBUG
            StringBuilder sb = new StringBuilder();
            if (parameters == null)
                return ("");
            foreach (SQLParam p in parameters)
            {
                string VType = "";
                string VValue = "";
                Type t = p.Content.GetType();
                if (t == typeof(DateTime))
                {
                    VType = "datetime";
                    if (t == null)
                        VValue = "null";
                    else
                        VValue = "'" + Convert.ToDateTime(p.Content).ToString("yyyy-MM-dd HH:mm:ss") + "'";
                }
                if (t == typeof(Int32))
                {
                    VType = "int";
                    if (t == null)
                        VValue = "null";
                    else
                        VValue = Convert.ToInt32(p.Content).ToString();
                }
                if (t == typeof(Int64))
                {
                    VType = "bigint";
                    if (t == null)
                        VValue = "null";
                    else
                        VValue = Convert.ToInt64(p.Content).ToString();
                }
                if (t == typeof(string))
                {
                    VType = "nvarchar(max)";
                    if (t == null)
                        VValue = "null";
                    else
                        VValue = "'" + Convert.ToString(p.Content).Replace("'", "''") + "'";
                }
                if (t == typeof(decimal))
                {
                    VType = "decimal(28,10)";
                    if (t == null)
                        VValue = "null";
                    else
                        VValue = Convert.ToDecimal(p.Content).ToString(new CultureInfo("en-US"));
                }
                if (string.IsNullOrWhiteSpace(VType) == true)
                {
                    VType = "UNKNWON /* " + t.ToString() + " */";
                }
                sb.AppendLine("DECLARE " + p.Variable + " " + VType + " = " + VValue);
            }
            sb.AppendLine();
            sb.AppendLine();
            return (sb.ToString());
#else
            return ("");
#endif
        }

        public bool ConnectDatabase(string server, string database)
        {
            return (ConnectDatabase(server, database, "", "", false));
        }

        public bool ConnectDatabase(string server, string database, bool UseWinAuth, bool UseEncryption, string Username, string Password)
        {
            if (UseWinAuth == true)
                return (ConnectDatabase(server, database, true, UseEncryption));
            else
                return (ConnectDatabase(server, database, Username, Password, UseEncryption));
        }

        public bool ConnectDatabase(string DirectConnectionString)
        {
            Debug.WriteLine("SQLCONNECT: " + DirectConnectionString);
#if !DEBUGDB
            try
            {
#endif
                //Patches conn-string
                SqlConnectionStringBuilder conn = new SqlConnectionStringBuilder(DirectConnectionString);
                if (string.IsNullOrWhiteSpace(appname) == false)
                    conn["Application Name"] = appname.Trim();
                conn["ConnectRetryCount"] = ConnectRetryCount;
                conn["ConnectRetryInterval"] = ConnectRetryInterval;
                conn["Connection Timeout"] = ConnectTimeout;

                Connection = new SqlConnection(conn.ConnectionString);
                Connection.Open();
#if !DEBUGDB
            }
            catch (Exception ee)
            {
                Debug.WriteLine(ee.ToString());
                if (SEHError == true)
                    throw;
                return (false);
            }
#endif
            return (true);
        }

        public bool ConnectDatabase(string server, string database, bool UseWinAuth, bool UseEncryption)
        {
            if (UseWinAuth == false)
                return (ConnectDatabase(server, database, "", "", UseEncryption));
            Debug.WriteLine("SQLCONNECT: " + database + "@" + server);
#if !DEBUGDB
            try
            {
#endif
                if (server == "")
                    server = "localhost";
                SqlConnectionStringBuilder conn = new SqlConnectionStringBuilder();
                if (string.IsNullOrWhiteSpace(appname) == false)
                    conn["Application Name"] = appname.Trim();
                conn["Connection Timeout"] = ConnectTimeout;
                conn["Server"] = server;
                conn["Database"] = database;
                conn["Integrated Security"] = "SSPI";
                conn["ConnectRetryCount"] = ConnectRetryCount;
                conn["ConnectRetryInterval"] = ConnectRetryInterval;
                conn["Encrypt"] = UseEncryption;
                Connection = new SqlConnection(conn.ConnectionString);
                Connection.Open();
#if !DEBUGDB
            }
            catch (Exception ee)
            {
                Debug.WriteLine(ee.ToString());
                if (SEHError == true)
                    throw;
                return (false);
            }
#endif
            return (true);
        }

        public bool ConnectDatabase(string server, string database, string Username, string Password, bool UseEncryption)
        {
            Debug.WriteLine("SQLCONNECT: " + database + "@" + server);
#if !DEBUGDB
            try
            {
#endif
                if (server == "")
                    server = "localhost";
                SqlConnectionStringBuilder conn = new SqlConnectionStringBuilder();
                if (string.IsNullOrWhiteSpace(appname) == false)
                    conn["Application Name"] = appname.Trim();
                conn["Connection Timeout"] = ConnectTimeout;
                conn["Server"] = server;
                conn["Database"] = database;
                if (Username.Trim() != "")
                    conn["User ID"] = Username;
                if (Password != "")
                    conn["Password"] = Password;
                conn["ConnectRetryCount"] = ConnectRetryCount;
                conn["ConnectRetryInterval"] = ConnectRetryInterval;
                conn["Encrypt"] = UseEncryption;
                Connection = new SqlConnection(conn.ConnectionString);
                Connection.Open();
#if !DEBUGDB
            }
            catch (Exception ee)
            {
                Debug.WriteLine(ee.ToString());
                if (SEHError == true)
                    throw;
                return (false);
            }
#endif
            return (true);
        }

        public bool ExecSQL(string Query, params SQLParam[] Parameters)
        {
            if (Connection == null)
                return (false);
            Debug.WriteLine("SQL--: " + OutputParameters(Parameters) + Query);
#if !DEBUGDB
            try
            {
#endif
                SqlCommand command = new SqlCommand(Query);
                command.CommandTimeout = SqlCommandTimeout;
                command.Connection = Connection;
                command.CommandType = System.Data.CommandType.Text;
                if (SQLTransaction == true)
                    command.Transaction = trans;
                foreach (SQLParam p in Parameters)
                {
                    command.Parameters.Add(new SqlParameter(p.Variable, p.Content));
                }
                command.ExecuteNonQuery();
                return (true);
#if !DEBUGDB
            }
            catch (Exception ee)
            {
                Debug.WriteLine(ee.ToString());
                if (SEHError == true)
                    throw;
                return (false);
            }
#endif
        }

        public bool ExecSQL_EnterpriseOnly(string Query, params SQLParam[] Parameters)
        {
            if (Connection == null)
                return (false);

            string SQLEdition = Convert.ToString(ExecSQLScalar("SELECT SERVERPROPERTY('edition')")).Trim();
            if (SQLEdition.ToLower().StartsWith("enterprise edition") == false && SQLEdition.ToLower() != "sql azure")
            {
                Debug.WriteLine("NOT EXECUTING SQL--(EE): " + Query);
                return (false);
            }
            Debug.WriteLine("SQL--(EE): " + OutputParameters(Parameters) + Query);

#if !DEBUGDB
            try
            {
#endif
                SqlCommand command = new SqlCommand(Query);
                command.CommandTimeout = SqlCommandTimeout;
                command.Connection = Connection;
                command.CommandType = System.Data.CommandType.Text;
                if (SQLTransaction == true)
                    command.Transaction = trans;
                foreach (SQLParam p in Parameters)
                {
                    command.Parameters.Add(new SqlParameter(p.Variable, p.Content));
                }
                command.ExecuteNonQuery();
                return (true);
#if !DEBUGDB
            }
            catch (Exception ee)
            {
                Debug.WriteLine(ee.ToString());
                if (SEHError == true)
                    throw;
                return (false);
            }
#endif
        }

        public int ExecSQLNQ(string Query, params SQLParam[] Parameters)
        {
            if (Connection == null)
                return (0);
            Debug.WriteLine("SQLNQ: " + OutputParameters(Parameters) + Query);
#if !DEBUGDB
            try
            {
#endif
                SqlCommand command = new SqlCommand(Query);
                command.CommandTimeout = SqlCommandTimeout;
                command.Connection = Connection;
                command.CommandType = System.Data.CommandType.Text;
                if (SQLTransaction == true)
                    command.Transaction = trans;
                foreach (SQLParam p in Parameters)
                {
                    command.Parameters.Add(new SqlParameter(p.Variable, p.Content));
                }
                return (command.ExecuteNonQuery());
#if !DEBUGDB
            }
            catch (Exception ee)
            {
                Debug.WriteLine(ee.ToString());
                if (SEHError == true)
                    throw;
                return (0);
            }
#endif
        }

        public object ExecSQLScalar(string Query, params SQLParam[] Parameters)
        {
            if (Connection == null)
                return (null);
            Debug.WriteLine("SQLSC: " + OutputParameters(Parameters) + Query);
#if !DEBUGDB
            try
            {
#endif
                SqlCommand command = new SqlCommand(Query);
                command.CommandTimeout = SqlCommandTimeout;
                command.Connection = Connection;
                command.CommandType = System.Data.CommandType.Text;
                if (SQLTransaction == true)
                    command.Transaction = trans;
                foreach (SQLParam p in Parameters)
                {
                    command.Parameters.Add(new SqlParameter(p.Variable, p.Content));
                }
                return (command.ExecuteScalar());
#if !DEBUGDB
            }
            catch (Exception ee)
            {
                Debug.WriteLine(ee.ToString());
                if (SEHError == true)
                    throw;
                return (null);
            }
#endif
        }

        public SqlDataReader ExecSQLReader(string Query, params SQLParam[] Parameters)
        {
            if (Connection == null)
                return (null);
            Debug.WriteLine("SQLDR: " + OutputParameters(Parameters) + Query);

            if (RunningDR != null)
            {
                if (RunningDR.IsClosed == false)
                {
                    if (SEHError == true)
                    {
                        throw new VulpesSqlDataReaderConflictException("There's already an open SqlDataReader") { CurrentOpenDR = RunningDR, LastStackTrace = RunningDRStack };
                    }
                    else
                    {
                        return (null);
                    }
                }
            }

            RunningDR = null;
            RunningDRStack = null;

#if !DEBUGDB
            try
            {
#endif
                SqlCommand command = new SqlCommand(Query);
                command.CommandTimeout = SqlCommandTimeout;
                command.Connection = Connection;
                command.CommandType = System.Data.CommandType.Text;
                if (SQLTransaction == true)
                    command.Transaction = trans;
                foreach (SQLParam p in Parameters)
                {
                    command.Parameters.Add(new SqlParameter(p.Variable, p.Content));
                }

                SqlDataReader dr = command.ExecuteReader();

                RunningDR = dr;
                RunningDRStack = new StackTrace(skipFrames: 1, fNeedFileInfo: true);

                return (dr);
#if !DEBUGDB
            }
            catch (Exception ee)
            {
                Debug.WriteLine(ee.ToString());
                if (SEHError == true)
                    throw;
                return (null);
            }
#endif
        }

        public DataSet ExecSQLDataSet(string Query, params SQLParam[] Parameters)
        {
            if (Connection == null)
                return (null);
            Debug.WriteLine("SQLDS: " + OutputParameters(Parameters) + Query);
#if !DEBUGDB
            try
            {
#endif
                DataSet ds = new DataSet();
                SqlDataAdapter ad = new SqlDataAdapter();
                SqlCommand command = new SqlCommand(Query);
                command.CommandTimeout = SqlCommandTimeout;
                command.Connection = Connection;
                command.CommandType = System.Data.CommandType.Text;
                if (SQLTransaction == true)
                    command.Transaction = trans;
                foreach (SQLParam p in Parameters)
                {
                    command.Parameters.Add(new SqlParameter(p.Variable, p.Content));
                }
                ad.SelectCommand = command;
                ad.MissingSchemaAction = MissingSchemaAction.AddWithKey;
                ad.Fill(ds);
                return (ds);
#if !DEBUGDB
            }
            catch (Exception ee)
            {
                Debug.WriteLine(ee.ToString());
                if (SEHError == true)
                    throw;
                return (null);
            }
#endif
        }

        public DataTable ExecSQLDataTable(string Table, params SQLParam[] Parameters)
        {
            if (Connection == null)
                return (null);
#if !DEBUGDB
            try
            {
#endif
                SqlDataAdapter ad = new SqlDataAdapter();
                SqlCommand command = new SqlCommand("SELECT * FROM " + Table);
                command.CommandTimeout = SqlCommandTimeout;
                command.Connection = Connection;
                command.CommandType = System.Data.CommandType.Text;
                if (SQLTransaction == true)
                    command.Transaction = trans;
                foreach (SQLParam p in Parameters)
                {
                    command.Parameters.Add(new SqlParameter(p.Variable, p.Content));
                }
                DataTable dt = new DataTable(Table);
                ad.SelectCommand = command;
                ad.MissingSchemaAction = MissingSchemaAction.AddWithKey;
                ad.Fill(dt);
                return (dt);
#if !DEBUGDB
            }
            catch (Exception ee)
            {
                Debug.WriteLine(ee.ToString());
                if (SEHError == true)
                    throw;
                return (null);
            }
#endif
        }

        public bool ExistTable(string database, string Table)
        {
            if (Connection == null)
                return (false);
#if !DEBUGDB
            if (SQLTransaction == true)
                throw new Exception("Cannot execute in transaction-mode");
            try
            {
#endif
                object result = ExecSQLScalar("IF EXISTS(SELECT name FROM " + database + ".dbo.sysobjects WHERE id = OBJECT_ID(@table)) SELECT 1 AS Result ELSE SELECT 0 AS Result",
                    new SQLParam("@table", Table));
                if (Convert.ToInt32(result) == 1)
                    return (true);
                else
                    return (false);
#if !DEBUGDB
            }
            catch (Exception ee)
            {
                Debug.WriteLine(ee.ToString());
                if (SEHError == true)
                    throw;
                return (false);
            }
#endif
        }

        public bool ExistTable(string Table)
        {
            if (Connection == null)
                return (false);
#if !DEBUGDB
            if (SQLTransaction == true)
                throw new Exception("Cannot execute in transaction-mode");
            try
            {
#endif
                object result = ExecSQLScalar("IF EXISTS(SELECT * FROM sys.tables WHERE name=@table) SELECT 1 AS Result ELSE SELECT 0 AS Result",
                    new SQLParam("@table", Table));
                if (Convert.ToInt32(result) == 1)
                    return (true);
                else
                    return (false);
#if !DEBUGDB
            }
            catch (Exception ee)
            {
                Debug.WriteLine(ee.ToString());
                if (SEHError == true)
                    throw;
                return (false);
            }
#endif
        }

        public bool ExistDatabase(string Database)
        {
            if (Connection == null)
                return (false);
#if !DEBUGDB
            if (SQLTransaction == true)
                throw new Exception("Cannot execute in transaction-mode");
            try
            {
#endif
                object result = ExecSQLScalar("IF EXISTS (SELECT name FROM master.dbo.sysdatabases WHERE name = @database) SELECT 1 AS Result ELSE SELECT 0 AS Result",
                    new SQLParam("@database", Database));
                if (Convert.ToInt32(result) == 1)
                    return (true);
                else
                    return (false);
#if !DEBUGDB
            }
            catch (Exception ee)
            {
                Debug.WriteLine(ee.ToString());
                if (SEHError == true)
                    throw;
                return (false);
            }
#endif
        }

        public bool InsertMultiData(string Table, params SQLData[] data)
        {
            List<SQLData> l = new List<SQLData>();
            foreach (SQLData p in data)
                l.Add(p);
            return (InsertMultiData(Table, l));
        }

        public bool InsertMultiData(string Table, List<SQLData> data)
        {
            if (connection == null)
                return (false);
            if (data.Count == 0)
            {
                if (SEHError == true)
                    throw new Exception("data.Count is zero!");
                return (false);
            }
            int Count = 1;
            string Query = "INSERT INTO " + (Table.EndsWith("]") == true && Table.StartsWith("[") == true ? Table : "[" + Table + "]") + " (";
            string Variables = "";
            foreach (SQLData d in data)
            {
                Query += "[" + d.Column + "],";
                d.VariableName = "@variable" + Count.ToString();
                if (d.CompressData == false)
                    Variables += d.VariableName + ",";
                else
                    Variables += "COMPRESS(" + d.VariableName + "),";
                d.Count = Count;
                Count++;
            }

            if (Query.EndsWith(",") == true)
                Query = Query.Substring(0, Query.Length - 1);
            if (Variables.EndsWith(",") == true)
                Variables = Variables.Substring(0, Variables.Length - 1);

            Query += ") VALUES (" + Variables + ")";

#if !DEBUGDB
            try
            {
#endif
                SqlCommand command = new SqlCommand(Query);
                command.CommandTimeout = SqlCommandTimeout;
                command.Connection = Connection;
                command.CommandType = System.Data.CommandType.Text;
                Debug.WriteLine("SQLIM: " + Query);
                if (SQLTransaction == true)
                    command.Transaction = trans;
                foreach (SQLData d in data)
                {
                    command.Parameters.Add(new SqlParameter(d.VariableName, d.Data));
                }
                command.ExecuteNonQuery();
                return (true);
#if !DEBUGDB
            }
            catch (Exception ee)
            {
                Debug.WriteLine(ee.ToString());
                if (SEHError == true)
                    throw;
                return (false);
            }
#endif
        }

        public Int64? InsertMultiDataID(string Table, params SQLData[] data)
        {
            List<SQLData> l = new List<SQLData>();
            foreach (SQLData p in data)
                l.Add(p);
            return (InsertMultiDataID(Table, l));
        }

        public Int64? InsertMultiDataID(string Table, List<SQLData> data)
        {
            if (connection == null)
                return (null);
            if (data.Count == 0)
            {
                if (SEHError == true)
                    throw new Exception("data.Count is zero!");
                return (null);
            }
            int Count = 1;
            string Query = "DECLARE @tabl table(ID bigint); ";
            Query += "INSERT INTO " + (Table.EndsWith("]") == true && Table.StartsWith("[") == true ? Table : "[" + Table + "]") + " (";
            string Variables = "";
            foreach (SQLData d in data)
            {
                Query += "[" + d.Column + "],";
                d.VariableName = "@variable" + Count.ToString();
                if (d.CompressData == false)
                    Variables += d.VariableName + ",";
                else
                    Variables += "COMPRESS(" + d.VariableName + "),";
                d.Count = Count;
                Count++;
            }

            if (Query.EndsWith(",") == true)
                Query = Query.Substring(0, Query.Length - 1);
            if (Variables.EndsWith(",") == true)
                Variables = Variables.Substring(0, Variables.Length - 1);

            Query += ") OUTPUT Inserted.ID INTO @tabl VALUES (" + Variables + "); SELECT * FROM @tabl";

#if !DEBUGDB
            try
            {
#endif
                SqlCommand command = new SqlCommand(Query);
                command.CommandTimeout = SqlCommandTimeout;
                command.Connection = Connection;
                command.CommandType = System.Data.CommandType.Text;
                Debug.WriteLine("SQLIMID: " + Query);
                if (SQLTransaction == true)
                    command.Transaction = trans;
                foreach (SQLData d in data)
                {
                    command.Parameters.Add(new SqlParameter(d.VariableName, d.Data));
                }
                return (Convert.ToInt64(command.ExecuteScalar()));
#if !DEBUGDB
            }
            catch (Exception ee)
            {
                Debug.WriteLine(ee.ToString());
                if (SEHError == true)
                    throw;
                return (null);
            }
#endif
        }

        public bool LoadIntoClass(SqlDataReader dr, object obj)
        {
            FieldInfo[] fields = obj.GetType().GetFields();
            try
            {
                foreach (FieldInfo f in fields)
                {
                    string column = f.Name;
                    IEnumerable<Attribute> attrs = f.GetCustomAttributes();
                    foreach (Attribute a in attrs)
                    {
                        if (a is SQLLibColumn)
                        {
                            SQLLibColumn cl = (SQLLibColumn)a;
                            column = cl.col;
                            break;
                        }
                    }

                    if (column.Trim() == "")
                        continue;
                    if (DrHasColumn(dr, column) == false)
                        continue;

                    if (f.FieldType.IsGenericType && f.FieldType.GetGenericTypeDefinition() == typeof(Nullable<>))
                    {
                        if (dr[column] is DBNull)
                            f.SetValue(obj, null);
                        else
                            f.SetValue(obj, Convert.ChangeType(dr[column], f.FieldType.GetGenericArguments()[0]));
                    }
                    else
                    {
                        if (!(dr[column] is DBNull))
                            f.SetValue(obj, Convert.ChangeType(dr[column], f.FieldType));
                    }
                }
            }
            catch (Exception ee)
            {
                Debug.WriteLine(ee.ToString());
                if (SEHError == true)
                    throw;
                return (false);
            }
            return (true);
        }

        public bool DrHasColumn(SqlDataReader dr, string columnName)
        {
            for (int i = 0; i < dr.FieldCount; i++)
            {
                if (dr.GetName(i).Equals(columnName, StringComparison.InvariantCultureIgnoreCase))
                    return (true);
            }
            return (false);
        }

        public bool LoadIntoClassProp(SqlDataReader dr, object obj)
        {
            PropertyInfo[] fields = obj.GetType().GetProperties();
            try
            {
                foreach (PropertyInfo f in fields)
                {
                    string column = f.Name;
                    IEnumerable<Attribute> attrs = f.GetCustomAttributes();
                    foreach (Attribute a in attrs)
                    {
                        if (a is SQLLibColumn)
                        {
                            SQLLibColumn cl = (SQLLibColumn)a;
                            column = cl.col;
                            break;
                        }
                    }

                    if (column.Trim() == "")
                        continue;
                    if (DrHasColumn(dr, column) == false)
                        continue;

                    if (f.PropertyType.IsGenericType && f.PropertyType.GetGenericTypeDefinition() == typeof(Nullable<>))
                    {
                        if (dr[column] is DBNull)
                            f.SetValue(obj, null);
                        else
                            f.SetValue(obj, Convert.ChangeType(dr[column], f.PropertyType.GetGenericArguments()[0]));
                    }
                    else
                    {
                        if (!(dr[column] is DBNull))
                            f.SetValue(obj, Convert.ChangeType(dr[column], f.PropertyType));
                    }
                }
            }
            catch (Exception ee)
            {
                Debug.WriteLine(ee.ToString());
                if (SEHError == true)
                    throw;
                return (false);
            }
            return (true);
        }

        public List<SQLData> InsertFromClassPrep(object obj)
        {
            List<SQLData> Data = new List<SQLData>();
            FieldInfo[] fields = obj.GetType().GetFields();
            try
            {
                foreach (FieldInfo f in fields)
                {
                    string column = f.Name;
                    IEnumerable<Attribute> attrs = f.GetCustomAttributes();
                    foreach (Attribute a in attrs)
                    {
                        if (a is SQLLibColumn)
                        {
                            SQLLibColumn cl = (SQLLibColumn)a;
                            column = cl.col;
                            break;
                        }
                    }

                    if (column.Trim() == "")
                        continue;

                    SQLData sqldata = new SQLData(column, f.GetValue(obj));
                    sqldata.DataType = f.FieldType;
                    Data.Add(sqldata);
                }
            }
            catch (Exception ee)
            {
                Debug.WriteLine(ee.ToString());
                if (SEHError == true)
                    throw;
                return (null);
            }

            return (Data);
        }

        public List<SQLData> InsertFromClassPrepProp(object obj)
        {
            List<SQLData> Data = new List<SQLData>();
            PropertyInfo[] fields = obj.GetType().GetProperties();
            try
            {
                foreach (PropertyInfo f in fields)
                {
                    string column = f.Name;
                    IEnumerable<Attribute> attrs = f.GetCustomAttributes();
                    foreach (Attribute a in attrs)
                    {
                        if (a is SQLLibColumn)
                        {
                            SQLLibColumn cl = (SQLLibColumn)a;
                            column = cl.col;
                            break;
                        }
                    }

                    if (column.Trim() == "")
                        continue;

                    SQLData sqldata = new SQLData(column, f.GetValue(obj));
                    sqldata.DataType = f.PropertyType;
                    Data.Add(sqldata);
                }
            }
            catch (Exception ee)
            {
                Debug.WriteLine(ee.ToString());
                if (SEHError == true)
                    throw;
                return (null);
            }

            return (Data);
        }

        public bool InsertFromClass(string Table, object obj)
        {
            List<SQLData> Data = InsertFromClassPrep(obj);
            if (Data == null)
                return (false);
            return (InsertMultiData(Table, Data));
        }

        public string GetConstraint(string Table, string Column)
        {
            return (Convert.ToString(this.ExecSQLScalar(@"SELECT df.name as CN, t.name as TN, c.NAME as CN
                FROM sys.default_constraints as df
                INNER JOIN sys.tables as t ON df.parent_object_id = t.object_id
                INNER JOIN sys.columns as c ON df.parent_object_id = c.object_id AND df.parent_column_id = c.column_id
                where t.name=@table and c.name=@column",
                    new SQLParam("@table", Table),
                    new SQLParam("@column", Column))));
        }

        public bool BulkInsertMultiData(string Table, List<List<SQLData>> datas)
        {
            try
            {
                if (SQLTransaction == true)
                    throw new Exception("Cannot run in Transaction mode");

                if (datas.Count == 0)
                    return (true);

                DataTable table = new DataTable(Table);

                foreach (SQLData d in datas[0])
                {
                    DataColumn col = new DataColumn();
                    col.ColumnName = d.Column;

                    if (d.DataType.IsGenericType && d.DataType.GetGenericTypeDefinition() == typeof(Nullable<>))
                        col.DataType = d.DataType.GetGenericArguments()[0];
                    else
                        col.DataType = d.DataType;

                    table.Columns.Add(col);
                }

                foreach (List<SQLData> data in datas)
                {
                    DataRow row = table.NewRow();

                    foreach (SQLData d in data)
                    {
                        row[d.Column] = d.Data == null ? (object)DBNull.Value : d.Data;
                    }

                    table.Rows.Add(row);
                }
                table.AcceptChanges();

                SqlBulkCopy bulk = new SqlBulkCopy(connection, SqlBulkCopyOptions.CheckConstraints, null);
                bulk.DestinationTableName = Table;
                bulk.BulkCopyTimeout = SqlCommandTimeout;
                Debug.WriteLine("SQLBULKI: " + Table);
                bulk.WriteToServer(table);
            }
            catch (Exception ee)
            {
                Debug.WriteLine(ee.ToString());
                if (SEHError == true)
                    throw;
                return (false);
            }

            return (true);
        }

        public bool ColumnExists(string Table, string Column)
        {
            if (Convert.ToInt32(ExecSQLScalar("select count(*) from sys.all_columns where object_id=OBJECT_ID(@table) and Name=@column",
                new SQLParam("@table", Table),
                new SQLParam("@column", Column))) == 0)
                return (false);
            return (true);
        }

        public List<T> GetSimpleDisctinctList<T>(string Table, string Column)
        {
            if (ExistTable(Table) == false)
                return (null);
            if (ColumnExists(Table, Column) == false)
                return (null);

            List<T> result = new List<T>();
            SqlDataReader dr = ExecSQLReader("SELECT DISTINCT [" + Column + "] FROM [" + Table + "] ORDER BY [" + Column + "]");
            while (dr.Read())
            {
#if !SQLLIB_USE_LEGACY_NAMESPACE
                result.Add((T)Convert.ChangeType(dr[dr.GetName(0)], typeof(T)));
#else
                result.Add((T)Convert.ChangeType(dr[0], typeof(T)));
#endif
            }
            dr.Close();
            return (result);
        }

        public void ExportAsSQLTable(string Table, TextWriter txtw, string SupplementWhereClause, params SQLParam[] Parameters)
        {
            CultureInfo en_us = new CultureInfo("en-us");

            txtw.WriteLine("-- Table: " + Table);
            txtw.WriteLine("--");
            bool TableHasIdentity = Convert.ToInt32(ExecSQLScalar(@"select sum(cast(sys.columns.is_identity as int)) as IDEN
                                from sys.columns inner join sys.tables on sys.tables.object_id = sys.columns.object_id
                                where sys.tables.name=@table",
                new SQLParam("@table", Table))) > 0 ? true : false;
            List<string> ComputedColumns = new List<string>();
            SqlDataReader dr = ExecSQLReader(@"select sys.columns.name
                                from sys.columns inner join sys.tables on sys.tables.object_id = sys.columns.object_id
                                where sys.tables.name=@table and sys.columns.is_computed=1",
                new SQLParam("@table", Table));
            while (dr.Read())
            {
                ComputedColumns.Add(Convert.ToString(dr["Name"]));
            }
            dr.Close();

            if (TableHasIdentity == true)
                txtw.WriteLine("SET IDENTITY_INSERT [" + Table + "] ON");
            int Counter = 0;
            dr = ExecSQLReader("select * from [" + Table + "] " + (string.IsNullOrWhiteSpace(SupplementWhereClause) == true ? "" : " WHERE " + SupplementWhereClause.Trim()), Parameters);
            while (dr.Read())
            {
                txtw.Write("INSERT INTO [" + Table + "] (");
                int j = 0;
                for (int i = 0; i < dr.FieldCount; i++)
                {
                    if (ComputedColumns.Contains(dr.GetName(i)) == true)
                        continue;
                    if (j > 0)
                        txtw.Write(",");
                    txtw.Write("[" + dr.GetName(i) + "]");
                    j++;
                }
                txtw.Write(") VALUES (");
                j = 0;
                for (int i = 0; i < dr.FieldCount; i++)
                {
                    if (ComputedColumns.Contains(dr.GetName(i)) == true)
                        continue;
                    if (j > 0)
                        txtw.Write(",");
                    if (dr.IsDBNull(i) == true)
                    {
                        txtw.Write("NULL");
                        j++;
                        continue;
                    }
                    switch (dr.GetDataTypeName(i).ToLower())
                    {
                        case "varchar":
                            txtw.Write("'" + Convert.ToString(dr.GetValue(i)).Replace("'", "''") + "'");
                            break;
                        case "nvarchar":
                            txtw.Write("N'" + Convert.ToString(dr.GetValue(i)).Replace("'", "''") + "'");
                            break;
                        case "int":
                        case "bigint":
                        case "bit":
                            txtw.Write(Convert.ToInt64(dr.GetValue(i)));
                            break;
                        case "decimal":
                            txtw.Write(Convert.ToDecimal(dr.GetValue(i)).ToString(en_us));
                            break;
                        case "datetime":
                            txtw.Write("'" + Convert.ToDateTime(dr.GetValue(i)).ToString("yyyy-MM-dd HH:mm:ss") + "'");
                            break;
                        case "date":
                            txtw.Write("'" + Convert.ToDateTime(dr.GetValue(i)).ToString("yyyy-MM-dd") + "'");
                            break;
                        case "varbinary":
                            txtw.Write("0x" + BitConverter.ToString((byte[])dr.GetValue(i)));
                            break;
                        default:
                            Debug.WriteLine("UNK SQL TYPE: " + dr.GetDataTypeName(i));
                            txtw.Write("???");
                            break;
                    }
                    j++;
                }
                txtw.WriteLine(")");

                Counter++;
                if (Counter % 50 == 0)
                {
                    txtw.WriteLine("GO");
                }
            }
            dr.Close();
            if (TableHasIdentity == true)
                txtw.WriteLine("SET IDENTITY_INSERT [" + Table + "] OFF");
            txtw.WriteLine("GO");
            txtw.WriteLine("--");
            txtw.WriteLine("--");
        }
    }

    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
    public class SQLLibColumn : Attribute
    {
        public string col;
        public SQLLibColumn(string Column)
        {
            col = Column;
        }
    }

    public class SQLParam
    {
        public string Variable;
        public object Content;

        public SQLParam(string Var, object Cont)
        {
            this.Variable = Var;
            this.Content = Cont == null ? DBNull.Value : Cont;
        }
    }

    public class SQLData
    {
        public string Column;
        public object Data;

        public string VariableName;
        public int Count;

        public Type DataType;
        public bool CompressData;

        public SQLData(string Column, object data)
        {
            this.Column = Column;
            this.Data = data == null ? DBNull.Value : data;
        }

        public SQLData(string Column, object data, bool CompressData)
        {
            this.Column = Column;
            this.CompressData = CompressData;
            this.Data = data == null ? DBNull.Value : data;
        }
    }
}
