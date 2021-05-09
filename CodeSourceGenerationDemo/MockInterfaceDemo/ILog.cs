using System;
using System.Collections.Generic;
using System.Text;

namespace CodeSourceGenerationDemo.MockInterfaceDemo
{
    public partial interface ILog
    {
        object Sender { get; }
        DateTime LogTime { get; }
        string Message { get; set; }
        bool Error { get; }
        string ToStringA()
        {
            return "dfdfdf";
        }
        string MyProp
        {
            get => "fg";
            set { return; }
        }
    }
}
