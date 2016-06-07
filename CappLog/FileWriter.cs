using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using System.IO;
using System.Threading;

namespace CappLog
{
    public class FileWriter : BaseWriter, IWriter
    {

        public FileWriter(string path)
        {
            workingFolder = path;
            if (Directory.Exists(workingFolder) == false)
            {
                Directory.CreateDirectory(workingFolder);
            }
            queue = new List<LogData>();
            logEvent = new ManualResetEvent(false);
            Thread t = new Thread(new ThreadStart(this.Writer));
            t.Name = this.GetType().FullName + ".Writer";
            t.Start();
        }
        public override void Write(LogData Data)
        {
            queue.Add(Data);
            if (sleeping == true)
            {
                logEvent.Set();
            }
        }

        public override void Writer()
        {
            started = true;
            finished = false;
            StringBuilder vStrBuilder = new StringBuilder();
            do
            {
                while (queue.Count > 0)
                {
                    vStrBuilder.Remove(0, vStrBuilder.Length);
                    vStrBuilder.AppendLine("");
                    vStrBuilder.Append(String.Format("{0:yyyy-MM-dd HH:mm:ss}", queue[0].DateTime) + " ");
                    //   "Time", DbType.DateTime
                    vStrBuilder.Append(queue[0].LogType + " ");
                    //       "Category", DbType.String))
                    vStrBuilder.Append(queue[0].InClass + ".");
                    //         "Class", DbType.String
                    vStrBuilder.AppendLine(queue[0].Method);
                    //        "Function", DbType.String
                    vStrBuilder.AppendLine("\t" + queue[0].Description);
                    //   "Description", DbType.String
                    //vStrBuilder.AppendLine("Sent=0") '                 "Sent", DbType.Int32

                    foreach (KeyValuePair<DataColumn, object> vKeyValuePair in queue[0].StaticData)
                    {
                        vStrBuilder.AppendLine("\t" + vKeyValuePair.Key.ColumnName.ToString() + "=" + vKeyValuePair.Value.ToString());
                    }

                    string vFileName = null;

                    if (Log.SeparateFileForEachTypeOfRecord == true)
                    {
                        vFileName = string.Format("{0}\\{1}_{2}.Log", workingFolder, queue[0].LogType, String.Format("{0:" + Log.FileNameDateFormat + "}", queue[0].DateTime));
                    }
                    else
                    {
                        vFileName = string.Format("{0}\\{1}.Log", workingFolder, String.Format("{0:" + Log.FileNameDateFormat + "}", queue[0].DateTime));
                    }

                    //End If
                    StreamWriter vStreamWriter = null;
                    //Loop if someone else has got exclusive access to file
                    do
                    {
                        try
                        {
                            vStreamWriter = new System.IO.StreamWriter(vFileName, true, System.Text.Encoding.UTF8);
                        }
                        catch
                        {
                            vStreamWriter = null;
                        }
                    } while (vStreamWriter == null);
                    vStreamWriter.Write(vStrBuilder.ToString());
                    vStreamWriter.Close();
                    vStreamWriter.Dispose();
                    vStreamWriter = null;
                    queue.RemoveAt(0);
                }
                if (started == true)
                {
                    logEvent.Reset();
                    sleeping = true;
                    logEvent.WaitOne();
                    sleeping = false;
                }
            } while (started == true | queue.Count > 0);
            finished = true;
        }

        public override int Shrink(int recordsToSave)
        {
            return -1;
        }

        public override DataTable GetDataToSync(bool allLogs, DateTime BeginDate, DateTime EndDate)
        {
            return null;
        }

        public override int SetDataSync(ref DataTable LogTable)
        {
            throw new NotImplementedException();
        }

        public override void Close()
        {
            started = false;
            //If _Sleeping = True Then
            //    Call _LogEvent.Set()
            //End If
            while (finished == false)
            {
                logEvent.Set();
                Thread.Sleep(100);
            }
        }
    }
}
