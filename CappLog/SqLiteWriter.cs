using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AppSql;
using System.Data.Common;
using System.Threading;
using System.Data;
using System.IO;

namespace CappLog
{
    public class SqLiteWriter : BaseSQLWriter, IWriter
    {
        private string fullName;

        public SqLiteWriter(Log ObjLog, string newFullname)
        {
            Log = ObjLog;
            loggingTableName = Log.LoggingTableName;
            fullName = newFullname;
            workingFolder = Path.GetDirectoryName(newFullname);
            PauseEvent = new ManualResetEvent(false);
            Init();
        }

        protected override void Init()
        {
            initialized = false;
            string vFileName = Path.GetFileName(fullName);
            if (FileWriter == null)
            {
                FileWriter = new FileWriter(workingFolder);
            }
            try
            {
                Sql = new SQL(SQLClientType.SQLite);
                if (File.Exists(fullName) == false)
                {
                    //_Sql.CreateDB(_FullName)
                    Sql.ConnectionString = "Data Source = " + fullName + ";";
                    Sql.Connection.Open();
                    //CreateLogTable(_Sql, _LoggingTableName, _Log.StaticDataColumns)
                }
                else
                {
                    Sql.ConnectionString = "Data Source = " + fullName + ";";
                    //_Sql.Close()
                    //_Sql = Nothing
                    //_Sql = New SQL(SQLClientType.SQLite)
                }

                //Check database for errors and recreate Db if get reoubles with connection
                if (Sql.DbObjectExist(enmDbObjectType.Table, loggingTableName) == false)
                {
                    CreateLogTable(Sql, loggingTableName, Log.StaticDataColumns);
                }
                Action<SqlException> vCurrentErrorHandler = Sql.ErrorHandler;
                Sql.ErrorHandler = null;
                try
                {
                    string vStrSqlSelect = string.Format("SELECT COUNT(*) FROM [{0}] LIMIT 1;", loggingTableName);
                    Sql.Value(vStrSqlSelect);
                }
                catch
                {
                    Sql.Connection.Close();
                    //Build Broken file name
                    string vBrokenDbFileName = string.Format("{0}\\Broken_{1}_{2}", Path.GetDirectoryName(fullName), String.Format("'{0:yyyy-MM-dd}'", DateTime.Now), Path.GetFileName(fullName));
                    //  Delete existing broken file!
                    //  If file does not exist this command 
                    //will not cause exception so is safe to call without checking!
                    File.Delete(vBrokenDbFileName);
                    //  Rename Log.Db to Broken file name!
                    File.Move(fullName, vBrokenDbFileName);
                    //  Creating Log.Db file
                    Sql.CreateDB(fullName);
                    //Another try to connect to Log.Db
                    Sql.ConnectionString = "Data Source = " + fullName + ";";
                    CreateLogTable(Sql, loggingTableName, Log.StaticDataColumns);
                }
                finally
                {
                    Sql.ErrorHandler = vCurrentErrorHandler;
                }


                if (InsertCmd == null)
                {
                    InsertCmd = GetInsertCommand(Sql, Log.StaticDataColumns);
                }

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
            if (InsertCmd != null)
            {
                InsertCmd.Dispose();
                InsertCmd = null;
            }
            if (Sql != null)
            {
                Sql.Close();
                Sql = null;
            }
            if (FileWriter != null)
            {
                FileWriter.Close();
                FileWriter = null;
            }
            Th = null;
            initialized = false;
        }

        protected override void CreateLogTable(SQL SqlClient, string tableName, DataColumn[] DataColumns)
        {
            string vStrSQL = "" + "CREATE TABLE IF NOT EXISTS [" + tableName + "]" + " ([UID] CHAR(36) PRIMARY KEY ASC NOT NULL," + " [Time] DATETIME NOT NULL," + " [Category] VARCHAR(20)," + " [Class] VARCHAR(50)," + " [Function] VARCHAR(50)," + " [Description] VARCHAR(4000)," + " [Sent] INTEGER";

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

        public override void Split(string newFileName)
        {
            newFileName = System.IO.Path.GetFileName(newFileName);
            newFileName = workingFolder + "\\" + newFileName;
            if (System.IO.File.Exists(newFileName) == false)
            {
                Close();
                System.Threading.Thread.Sleep(300);
                System.IO.File.Move(fullName, newFileName);
                Init();
            };
        }
    }
}
