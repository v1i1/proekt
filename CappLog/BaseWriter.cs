using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Data.Sql;
using System.Data;
using AppSql;
using System.Data.Common;

namespace CappLog
{
    public abstract class BaseWriter : IWriter
    {
        protected List<LogData> queue;
        protected ManualResetEvent logEvent;
        protected bool sleeping;
        protected bool started;
        protected bool finished;
        protected string workingFolder;

        // izkarvash obsite private poleta
        // i obsite metodi ot writerite

        public string WorkingFolder
        {
            get { return workingFolder; }
        }//

        public abstract void Write(LogData Data);//

        public abstract void Writer();//

        public abstract int Shrink(int recordsToSave);//

        public abstract DataTable GetDataToSync(bool allLogs, DateTime BeginDate, DateTime EndDate);//

        public abstract int SetDataSync(ref DataTable LogTable);//

        public virtual void Split(string newFileName)
        {
            throw new appLogException(this.GetType().FullName, "Split", "This class does not support Split functionality!");
        }//

        public abstract void Close();//

    }
}
