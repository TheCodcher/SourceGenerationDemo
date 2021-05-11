using System;
using System.Reflection.Emit;
using CodeSourceGenerationDemo.MockInterfaceDemo;

namespace CodeSourceGenerationDemo
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");
            Console.WriteLine(ILog.Empty.MyProp);
            HelloWorldGenerated.HelloWorld.SayHello();
            Console.ReadKey();
        }
    }
}
