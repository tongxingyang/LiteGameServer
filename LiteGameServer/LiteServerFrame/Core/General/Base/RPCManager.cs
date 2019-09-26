using System.Collections.Generic;
using System.Reflection;
using System.Text;
using DebugerTool;

namespace LiteServerFrame.Core.General.Base
{    
    public class RPCManager
    {
        protected List<object> listenerList;
        protected Dictionary<string, RPCMethodHelper> methodHelpers;

        public void Init()
        {
            listenerList = new List<object>();
            methodHelpers = new Dictionary<string, RPCMethodHelper>();
        }
        
        public void Clean()
        {
            listenerList.Clear();
            foreach (var pair in methodHelpers)
            {
                pair.Value.listener = null;
                pair.Value.method = null;
            }
            methodHelpers.Clear();
        }
        
        
        public void RegisterListener(object listener)
        {
            if (!listenerList.Contains(listener))
            {
                Debuger.Log("{0}", listener.GetType().Name);
                listenerList.Add(listener);
            }
        }

        public void UnRegisterListener(object listener)
        {
            if (listenerList.Contains(listener))
            {
                Debuger.Log("{0}", listener.GetType().Name);
                listenerList.Remove(listener);
            }
        }
        
        public RPCMethodHelper GetMethodHelper(string name)
        {
            var helper = methodHelpers[name];
            if (helper == null)
            {
                MethodInfo methodInfo = null;
                object listener = null;
                foreach (object listen in listenerList)
                {
                    listener = listen;
                    methodInfo = listener.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
                    if (methodInfo != null)
                    {
                        break;
                    }
                }

                if (methodInfo != null)
                {
                    helper = new RPCMethodHelper
                    {
                        listener = listener,
                        method = methodInfo
                    };
                    methodHelpers.Add(name, helper);
                }
            }

            return helper;
        }
        
        public void Dump()
        {
            StringBuilder sb = new StringBuilder();

            foreach (var pair in methodHelpers)
            {
                RPCMethodHelper helper = pair.Value;
                if (helper.method.DeclaringType != null)
                    sb.AppendFormat("\t<name:{0}, \tmethod:{1}.{2}>\n", pair.Key, helper.method.DeclaringType.Name, helper.method.Name);
            }

            Debuger.LogWarning("\nRPC Cached Methods ({0}):\n{1}", methodHelpers.Count, sb);
        }

    }
}