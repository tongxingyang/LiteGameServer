using System;
using System.Reflection;

namespace LiteServerFrame.Core.General.Base
{
    
    public class RPCRequestAttribute : Attribute
    {

    }

    public class RPCResponseAttribute : Attribute
    {

    }

    public class RPCNotifyAttribute : Attribute
    {
        
    }
    
    public class RPCMethodHelper
    {
        public object listener;
        public MethodInfo method;
    }
}