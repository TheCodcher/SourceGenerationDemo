﻿using System;
using System.Collections.Generic;
using System.Text;

namespace CodeSourceGenerationDemo.MockInterfaceDemo
{
    public partial interface ILogger
    {
        ILog[] GetLogs();
        ILog Log { get; }
        void Write(ILog log);
    }
}