using System;
using System.Collections.Generic;
using System.Text;

namespace CodeSourceGenerationDemo.OOPEnumsDemo
{
    [System.ObjectiveEnum.ObjectiveEnumSlim]
    public partial class ExceptionUtil : Exception
    {
        private ExceptionUtil() { }
        private ExceptionUtil(string exception) : base(exception) { }
        private ExceptionUtil(string exception, Exception innerExcp) : base(exception, innerExcp) { }

        public void Throw()
        {
            throw this;
        }

        static ExceptionUtil()
        {
            Enum.ObjectNotFound("object s*cks");
            Enum.NetException("wtf from network");
            Enum.VeryExpensiveSubscription("netflix cost 749p");
            Enum.StackOverflow("https://stackoverflow.com", new StackOverflowException());
            Enum.Empty();
            Enum.ObjectVeryNotFound("so s*ck", ObjectNotFound);
        }
    }
}
