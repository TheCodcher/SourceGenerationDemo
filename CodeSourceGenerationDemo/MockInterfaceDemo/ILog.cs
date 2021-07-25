using System;
using System.Collections.Generic;
using System.Text;

namespace CodeSourceGenerationDemo.MockInterfaceDemo
{
    [GeneratedAttributes.EmptyImplementation]
    public partial interface ILog
    {
        object Sender { get; }
        DateTime LogTime { get; }
        string Message { get; set; }
        bool Error { get; }
        string ExempleRealization()
        {
            return "exempl";
        }
        string MyProp
        {
            get => "naice";
            set { return; }
        }
    }
}
