using CappLog;
using System;
using System.Data;

    public interface IWriter
    {
        string WorkingFolder { get; }

        void Write(LogData Data);

        void Writer();

        int Shrink(int recordsToSave);

        DataTable GetDataToSync(bool allLogs, DateTime BeginDate, DateTime EndDate);

        int SetDataSync(ref DataTable LogTable);

        void Split(string newFileName);

        void Close();
    }
