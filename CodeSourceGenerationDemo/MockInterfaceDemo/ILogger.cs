using System;
using System.Collections.Generic;
using System.Text;

namespace CodeSourceGenerationDemo.MockInterfaceDemo
{
    [GeneratedAttributes.EmptyImplementationAttribute]
    public partial interface ILogger
    {
        ILog[] GetLogs();
        ILog GetLog(string key);
        void Register(ILog log);
    }
}
