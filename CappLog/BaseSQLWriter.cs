using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AppSql;
using System.Data.Common;
using System.Threading;
using System.Data;

namespace CappLog
{
    public abstract class BaseSQLWriter : BaseWriter, IWriter
    {
        protected string loggingTableName;
        protected bool initialized;
        protected Log Log;
        protected SQL Sql;
        protected FileWriter FileWriter;
        protected DbCommand InsertCmd;
        protected Thread Th;
        protected ManualResetEvent PauseEvent;

        public override void Write(LogData Data)
        {
            if (initialized == true)
            {
                queue.Add(Data);
                if (sleeping == true)
                {
                    logEvent.Set();
                }
            }
            else
            {
                if (Data.LogTypeCode == System.Convert.ToInt32(enmLogType.Error))
                {
                    FileWriter.Write(Data);
                }
            }
        }

        public override void Writer()
        {
            started = true;
            finished = false;
            try
            {
                do
                {
                    while (queue.Count > 0)
                    {
                        var _with1 = InsertCmd;
                        _with1.Parameters[0].Value = Guid.NewGuid().ToString();
                        //     ("UID", DbType.String
                        _with1.Parameters[1].Value = queue[0].DateTime;
                        //   "Time", DbType.DateTime
                        _with1.Parameters[2].Value = queue[0].LogType;
                        //       "Category", DbType.String))
                        _with1.Parameters[3].Value = queue[0].InClass;
                        //         "Class", DbType.String
                        _with1.Parameters[4].Value = queue[0].Method;
                        //        "Function", DbType.String
                        _with1.Parameters[5].Value = queue[0].Description;
                        //   "Description", DbType.String
                        _with1.Parameters[6].Value = System.Convert.ToInt32(0);
                        //                 "Sent", DbType.Int32
                        //Dim vParameterIdx As Integer = 7
                        foreach (KeyValuePair<System.Data.DataColumn, object> vRecord in queue[0].StaticData)
                        {
                            _with1.Parameters[vRecord.Key.ColumnName].Value = vRecord.Value;
                            //vParameterIdx += 1
                        }
                        //RemoveBadCharacters(_InsertCmd)
                        try
                        {
                            _with1.ExecuteNonQuery();
                        }
                        catch (Exception ex)
                        {
                            //_FileWriter.Write(_Queue(0))
                            if (queue[0].LogTypeCode == System.Convert.ToInt32(enmLogType.Error))
                            {
                                FileWriter.Write(queue[0]);
                            }
                            FileWriter.Write(new SysLogData(this.GetType().FullName, "Writer", ex));
                        }
                        finally
                        {
                            queue.RemoveAt(0);
                        }
                    }
                    if (started == true)
                    {
                        logEvent.Reset();
                        sleeping = true;
                        logEvent.WaitOne();
                        sleeping = false;
                    }
                } while (started == true | queue.Count > 0);
            }
            catch (Exception Ex)
            {
                FileWriter.Write(new SysLogData(this.GetType().Name, "Writer", Ex));
            }
            finished = true;
        }

        public override int Shrink(int RecordsToSave)
        {
            int Result = System.Convert.ToInt32(Sql.Value(string.Format("SELECT COUNT([UID]) AS TotalRecords FROM [{0}];", loggingTableName))) - RecordsToSave;
            if (Result > 0)
            {
                Result = Sql.Exec(string.Format("DELETE FROM [{0}] WHERE [UID] IN (SELECT [UID] FROM [{0}] LIMIT {1});", loggingTableName, Result.ToString()));
            }
            return Result;
        }

        public override DataTable GetDataToSync(bool AllLogs, DateTime BeginDate, DateTime EndDate)
        {
            System.Data.Common.DbParameter[] vPar = {
			Sql.NewParameter("BeginDate", DbType.Date, BeginDate),
			Sql.NewParameter("EndDate", DbType.Date, EndDate)
		    };
            string vSQLText = null;
            if (AllLogs == true)
            {
                vSQLText = string.Format("SELECT * FROM [{0}] WHERE ([Sent] = 0 AND [Time] BETWEEN @BeginDate AND @EndDate);", loggingTableName);
            }
            else
            {
                vSQLText = string.Format("SELECT * FROM [{0}] WHERE ([Sent]=0 AND [Time] BETWEEN @BeginDate AND @EndDate AND Category='Error');", loggingTableName);
            }
            System.Data.DataTable vResult = Sql.Select(vSQLText, vPar);
            vResult.TableName = loggingTableName;
            return vResult;
        }

        public override int SetDataSync(ref DataTable LogTable)
        {
            int vResult = 0;
            string vSQLText = string.Format("UPDATE [{0}] SET [Sent]=@Sended WHERE [UID]=@UID;", loggingTableName);
            System.Data.Common.DbParameter[] vPar = {
			Sql.NewParameter("Sended", DbType.Int32, System.Convert.ToInt32(1)),
			Sql.NewParameter("UID", DbType.String)
		};
            foreach (System.Data.DataRow vRow in LogTable.Rows)
            {
                vPar[1].Value = (string)vRow["UID"];
                vResult += Sql.Exec(vSQLText, vPar[0], vPar[1]);
            }
            return vResult;
        }

        protected abstract void Init();

        public abstract void Close();

        protected abstract void CreateLogTable(SQL SqlClient, string tableName, DataColumn[] DataColumns);

        protected DbCommand GetInsertCommand(SQL SqlClient, DataColumn[] DataColumns)
        {

            string vStrStaticFieldNames = GetInsertFieldNames(DataColumns);
            if (vStrStaticFieldNames == null || vStrStaticFieldNames.Length < 1)
            {
                vStrStaticFieldNames = " ";
            }

            List<DbParameter> vParList = GetInsertParameters(SqlClient, DataColumns);

            DbCommand vInsertCmd = SqlClient.NewCommand("INSERT INTO " + loggingTableName + " (" + "[UID], " + "[Time], " + "[Category], " + "[Class], " + "[Function], " + "[Description], " + "[Sent]" + vStrStaticFieldNames.Replace("@", "") + ") VALUES (" + "@UID, " + "@Time, " + "@Category, " + "@Class, " + "@Function, " + "@Description, " + "@Sent" + vStrStaticFieldNames.Replace("[", "").Replace("]", "") + ");", vParList);
            return vInsertCmd;
        }

        protected string GetInsertFieldNames(DataColumn[] DataColumns)
        {
            string vStrStaticFiledNames = "";
            foreach (DataColumn vStaticColumn in DataColumns)
            {
                vStrStaticFiledNames += ", [@" + vStaticColumn.ColumnName + "] ";
            }
            return vStrStaticFiledNames;
        }

        protected List<DbParameter> GetInsertParameters(SQL SqlClient, DataColumn[] DataColumns)
        {

            List<DbParameter> vParList = new List<DbParameter>();
            vParList.Add(SqlClient.NewParameter("UID", DbType.String));
            vParList.Add(SqlClient.NewParameter("Time", DbType.DateTime));
            vParList.Add(SqlClient.NewParameter("Category", DbType.String));
            vParList.Add(SqlClient.NewParameter("Class", DbType.String));
            vParList.Add(SqlClient.NewParameter("Function", DbType.String));
            vParList.Add(SqlClient.NewParameter("Description", DbType.String));
            vParList.Add(SqlClient.NewParameter("Sent", DbType.Int32));

            if (DataColumns != null)
            {
                foreach (DataColumn vStaticColumn in DataColumns)
                {
                    switch (vStaticColumn.DataType.Name.ToUpper())
                    {
                        case "STRING":
                            vParList.Add(SqlClient.NewParameter(vStaticColumn.ColumnName, DbType.String));
                            break;
                        case "BYTE":
                        case "INTEGER":
                        case "INT16":
                        case "INT32":
                        case "INT64":
                            vParList.Add(SqlClient.NewParameter(vStaticColumn.ColumnName, DbType.Int64));
                            break;
                        case "DATE":
                        case "DATETIME":
                            vParList.Add(SqlClient.NewParameter(vStaticColumn.ColumnName, DbType.DateTime));
                            break;
                        case "SINGLE,DOUBLE,DECIMAL":
                            vParList.Add(SqlClient.NewParameter(vStaticColumn.ColumnName, DbType.Decimal));
                            break;
                        default:
                            vParList.Add(SqlClient.NewParameter(vStaticColumn.ColumnName, DbType.String));
                            break;
                    }
                }
            }
            return vParList;
        }

        protected void RemoveBadCharacters(ref DbCommand Command)
        {
            const char FirstChar = ' ';
            if (Command != null)
            {
                foreach (DbParameter vPar in Command.Parameters)
                {
                    if (vPar.DbType == DbType.String && vPar.Value is string && (vPar.Value != null))
                    {
                        string vValue = vPar.Value.ToString();
                        int vIdx = 0;
                        StringBuilder vStrBuilder = new StringBuilder();
                        while (vIdx < vValue.Length)
                        {
                            if (vValue[vIdx] < FirstChar)
                            {
                                vStrBuilder.Append(FirstChar);
                            }
                            else
                            {
                                vStrBuilder.Append(vValue[vIdx]);
                            }
                            vIdx += 1;
                        }
                        vPar.Value = vStrBuilder.ToString();
                    }
                }
            }
        }
    }
}
