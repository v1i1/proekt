
using Microsoft.VisualBasic;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;

namespace CappLog
{
    public enum enmLogType
    {
        UserAction = 1,
        Action = 2,
        Warning = 10,
        Error = 20,
        Debug = 30,
        None = 999
    }
}