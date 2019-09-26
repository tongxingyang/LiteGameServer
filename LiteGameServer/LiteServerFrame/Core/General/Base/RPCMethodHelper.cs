using System;
using System.Reflection;

namespace LiteServerFrame.Core.General.Base
{
    public class RPCAttribute : Attribute
    {

    }

    public class RPCInvokeAttribute : Attribute
    {

    }
    
    public class RPCMethodHelper
    {
        public object listener;
        public MethodInfo method;
    }
}