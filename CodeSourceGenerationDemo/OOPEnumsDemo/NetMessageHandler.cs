using System;
using System.Collections.Generic;
using System.Text;
using System.ObjectiveEnum;

namespace CodeSourceGenerationDemo.OOPEnumsDemo
{
    [ObjectiveEnum]
    partial class NetMessageHandler
    {
        Action<string> method;
        private NetMessageHandler()
        {
            method = Console.WriteLine;
        }
        private NetMessageHandler(Action<string> actualMethod)
        {
            method = actualMethod;
        }
        static NetMessageHandler()
        {
            Enum.Default();
            Enum.NotFound(s => Console.WriteLine($"error response: {s}"))(404);
            Enum.Ok(s => Console.WriteLine($"done: {s}"))(200);
            Enum.ServerException(s => Console.WriteLine($"server exception: {s}"))(500);
            Enum.Redirect(s => Console.WriteLine($"redirect to {s}"))(302);
        }

        public void Handle(string message)
        {
            method?.Invoke(message);
        }
    }
    class SomeResponse
    {
        public int HttpCode { get; set; }
        public string Response { get; set; }
    }
}
