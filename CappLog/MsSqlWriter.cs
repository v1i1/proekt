using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AppSql;
using System.Data.Common;
using System.Threading;
using System.Data;
using System.IO;
using System.Reflection;

namespace CappLog
{
    public class MsSqlWriter : BaseSQLWriter, IWriter
    {
        private string connectionString;

        public MsSqlWriter(Log ObjLog, string connString)
        {
            if (Environment.OSVersion.Platform == PlatformID.WinCE)
            {
                throw new appLogException(this.GetType().FullName, "New(appLog.Log, String)", "Unsupported platform!", connString);
            }
            Log = ObjLog;
            loggingTableName = Log.LoggingTableName;
            connectionString = connString;
            workingFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().GetName().CodeBase);
            if (workingFolder.ToUpper().StartsWith("FILE:/") == true || workingFolder.ToUpper().StartsWith("FILE:\\") == true)
                workingFolder = workingFolder.Substring(6);
            PauseEvent = new ManualResetEvent(false);
            //_Status = New Status
            Init();
        }

        protected override void Init()
        {
            initialized = false;
            if (FileWriter == null)
            {
                FileWriter = new FileWriter(workingFolder);
            }
            try
            {
                Sql = new SQL(SQLClientType.MSSQL);
                Sql.ConnectionString = connectionString;

                //Check database for errors and recreate Db if get reoubles with connection
                if (Sql.DbObjectExist(enmDbObjectType.Table, loggingTableName) == false)
                {
                    CreateLogTable(Sql, loggingTableName, Log.StaticDataColumns);
                }
                Action<SqlException> vCurrentErrorHandler = Sql.ErrorHandler;
                Sql.ErrorHandler = null;
                try
                {
                    string vStrSqlSelect = string.Format("SELECT COUNT(*) FROM [{0}] ;", loggingTableName);
                    Sql.Value(vStrSqlSelect);
                }
                catch
                {
                    Sql.Connection.Close();
                    Sql.ConnectionString = connectionString;
                    CreateLogTable(Sql, loggingTableName, Log.StaticDataColumns);
                }
                finally
                {
                    Sql.ErrorHandler = vCurrentErrorHandler;
                }

                if (InsertCmd != null)
                {
                    InsertCmd.Dispose();
                    InsertCmd = null;
                }
                
                InsertCmd = GetInsertCommand(Sql, Log.StaticDataColumns);

                if (queue == null)
                {
                    queue = new List<LogData>();
                }
                if (logEvent == null)
                {
                    logEvent = new ManualResetEvent(false);
                }
                if (Th == null)
                {
                    Th = new Thread(new ThreadStart(this.Writer));
                    Th.Name = this.GetType().FullName + ".Writer";
                    Th.Start();
                }
                initialized = true;
            }
            catch (Exception Ex)
            {
                FileWriter.Write(new SysLogData(this.GetType().FullName, "New", Ex));
                initialized = false;
            }
        }

        public override void Close()
        {
            started = false;
            while (finished == false)
            {
                if (sleeping == true)
                {
                    started = false;
                    logEvent.Set();
                }
                Thread.Sleep(100);
            }
            InsertCmd.Dispose();
            InsertCmd = null;
            Sql.Close();
            FileWriter.Close();
            FileWriter = null;
            Th = null;
            initialized = false;
        }

        protected override void CreateLogTable(SQL SqlClient, string tableName, DataColumn[] DataColumns)
        {
            string vStrSQL = "" + "IF  NOT EXISTS (SELECT * FROM [" + tableName + "])" + " CREATE TABLE [" + tableName + "]" + " ([UID] CHAR(36) PRIMARY KEY NOT NULL," + " [Time] DATETIME NOT NULL," + " [Category] VARCHAR(20)," + " [Class] VARCHAR(50)," + " [Function] VARCHAR(50)," + " [Description] VARCHAR(4000)," + " [Sent] INTEGER";

            foreach (DataColumn vStaticColumn in DataColumns)
            {
                vStrSQL += ", [" + vStaticColumn.ColumnName + "] ";
                switch (vStaticColumn.DataType.Name.ToUpper())
                {
                    case "STRING":
                        vStrSQL += "VARCHAR(" + vStaticColumn.MaxLength.ToString() + ")";
                        break;
                    case "BYTE":
                    case "INTEGER":
                    case "INT16":
                    case "INT32":
                    case "INT64":
                        vStrSQL += "INTEGER";
                        break;
                    case "DATE":
                    case "DATETIME":
                        vStrSQL += "DATETIME";
                        break;
                    case "SINGLE,DOUBLE,DECIMAL":
                        vStrSQL += "DECIMAL(18,2)";
                        break;
                    default:
                        vStrSQL += "VARCHAR(4000)";
                        break;
                }
            }
            vStrSQL += ");";
            Sql.Exec(vStrSQL);
        }

    }
}
