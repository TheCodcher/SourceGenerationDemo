using System;
using System.Collections.Generic;
using System.Text;
using System.ObjectiveEnum;

namespace CodeSourceGenerationDemo.OOPEnumsDemo
{
    [ObjectiveEnum]
    partial class Role
    {
        public readonly string Title;
        public readonly int Color;
        public readonly string Prefix;

        public Role()
        {
            Title = "None";
            Color = (int)ConsoleColor.White;
            Prefix = "";
        }
        public Role(string name = "", int color = 0, string pref = "")
        {
            Title = name;
            Color = color;
            Prefix = pref;
        }

        static Role()
        {
            Enum.None();
            Enum.User("Default", (int)ConsoleColor.Blue, "")(1 << 0);
            Enum.Moderator("Administrator", (int)ConsoleColor.Yellow, "[moder]")(1 << 1);
            Enum.Admin("Administrator", (int)ConsoleColor.Red, "[admin]")(1 << 2);
            Enum.Owner("Administrator", (int)ConsoleColor.Cyan, "[owner]")(1 << 3);
        }
    }
    class User
    {
        public int Roles;
        public string Name;
    }
}
