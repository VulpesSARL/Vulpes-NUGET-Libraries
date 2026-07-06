using System;
using System.Collections.Generic;
#if !SQLLIB_USE_LEGACY_NAMESPACE
using Microsoft.Data.SqlClient;
#else
using System.Data.SqlClient;
#endif
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vulpes.Library
{
    public partial class SQLLib
    {
        public bool ExecSQLSP(string Procedure, params SQLParam[] Parameters)
        {
            if (Connection == null)
                return (false);
            MyDebugWriteLine("[SP] SQL--: " + MyDebugOutputParameters(Parameters) + Procedure);
#if !DEBUGDB
            try
            {
#endif
                SqlCommand command = new SqlCommand(Procedure);
                command.CommandTimeout = SqlCommandTimeout;
                command.Connection = Connection;
                command.CommandType = System.Data.CommandType.StoredProcedure;
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
                MyDebugWriteLine(ee.ToString());
                if (SEHError == true)
                    throw;
                return (false);
            }
#endif
        }

        public SqlDataReader ExecSQLReaderSP(string Procedure, params SQLParam[] Parameters)
        {
            if (Connection == null)
                return (null);
            MyDebugWriteLine("[SP] SQLDR: " + MyDebugOutputParameters(Parameters) + Procedure);
#if !DEBUGDB
            try
            {
#endif
                SqlCommand command = new SqlCommand(Procedure);
                command.CommandTimeout = SqlCommandTimeout;
                command.Connection = Connection;
                command.CommandType = System.Data.CommandType.StoredProcedure;
                if (SQLTransaction == true)
                    command.Transaction = trans;
                foreach (SQLParam p in Parameters)
                {
                    command.Parameters.Add(new SqlParameter(p.Variable, p.Content));
                }
                return (command.ExecuteReader());
#if !DEBUGDB
            }
            catch (Exception ee)
            {
                MyDebugWriteLine(ee.ToString());
                if (SEHError == true)
                    throw;
                return (null);
            }
#endif
        }

        public int ExecSQLNQSP(string Procedure, params SQLParam[] Parameters)
        {
            if (Connection == null)
                return (0);
            MyDebugWriteLine("[SP] SQLNQ: " + MyDebugOutputParameters(Parameters) + Procedure);
#if !DEBUGDB
            try
            {
#endif
                SqlCommand command = new SqlCommand(Procedure);
                command.CommandTimeout = SqlCommandTimeout;
                command.Connection = Connection;
                command.CommandType = System.Data.CommandType.StoredProcedure;
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
                MyDebugWriteLine(ee.ToString());
                if (SEHError == true)
                    throw;
                return (0);
            }
#endif
        }

        public object ExecSQLScalarSP(string Procedure, params SQLParam[] Parameters)
        {
            if (Connection == null)
                return (null);
            MyDebugWriteLine("[SP] SQLSC: " + MyDebugOutputParameters(Parameters) + Procedure);
#if !DEBUGDB
            try
            {
#endif
                SqlCommand command = new SqlCommand(Procedure);
                command.CommandTimeout = SqlCommandTimeout;
                command.Connection = Connection;
                command.CommandType = System.Data.CommandType.StoredProcedure;
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
                MyDebugWriteLine(ee.ToString());
                if (SEHError == true)
                    throw;
                return (null);
            }
#endif
        }
    }
}
