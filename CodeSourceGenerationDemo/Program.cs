using System;
using System.Reflection.Emit;
using CodeSourceGenerationDemo.MockInterfaceDemo;
using CodeSourceGenerationDemo.OOPEnumsDemo;
using System.Reflection;
using System.Threading.Tasks;
using System.Threading;
using System.ObjectiveEnum;
using System.Linq;
using System.Collections.Generic;

namespace CodeSourceGenerationDemo
{
    class Program
    {
        static void Main(string[] args)
        {
            Example1();
            Console.WriteLine();
            Example2();
            Console.WriteLine();
            Example3();
        }

        private static void Example1()
        {
            var someData = new[]
            {
                new SomeResponse { HttpCode = 200, Response = "hellow word!" },
                new SomeResponse { HttpCode = 405, Response = "wtf" },
                new SomeResponse { HttpCode = 302, Response = "stackoverflow.com" }
            };

            foreach (var data in someData)
            {
                DataHandler(data);
            }

            void DataHandler(SomeResponse data)
            {
                if (ObjectiveEnum.TryGetValue<NetMessageHandler>(data.HttpCode, out var messageHandler))
                {
                    messageHandler.Handle(data.Response);
                }
                else
                {
                    NetMessageHandler.Default.Handle(data.Response);
                }
            }
        }
        private static void Example3()
        {
            try
            {
                var exempleExp = ExceptionUtil.ObjectVeryNotFound;
                exempleExp.Throw();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }
        private static void Example2()
        {
            var someData = new[]
            {
                new User { Name = "Roma", Roles = Role.Owner },
                new User { Name = "Lime", Roles = Role.Moderator|Role.Admin },
                new User { Name = "Can you ask a question?", Roles = Role.User }
            };

            foreach (var data in someData)
            {
                LogModeratorAccess(data);
            }

            void LogModeratorAccess(User user)
            {
                var max = (Role)ObjectiveEnum.GetFlags<Role>(user.Roles).Max(v => v.Ordinal);
                Console.ForegroundColor = (ConsoleColor)max.Color;
                if (user.Roles >= Role.Moderator)
                {
                    Console.WriteLine($"{max.Prefix} {user.Name} has access");
                }
                else
                {
                    Console.WriteLine($"{max.Prefix} {user.Name} has not access");
                }
                Console.ForegroundColor = ConsoleColor.White;
            }
        }
    }
}
