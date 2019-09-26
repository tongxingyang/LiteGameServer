using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using DebugerTool;
using LiteServerFrame.Core.General.Base;
using LiteServerFrame.Utility;

namespace LiteServerFrame.Core.General.Server
{
    public class NetManager : ISessionListener
    {
        class ListenerHelper
        {
            public Type typeMsg;
            public Delegate handleMsg;
        }
        private Dictionary<uint, ListenerHelper> listenerHelpers = new Dictionary<uint, ListenerHelper>();
        
        private GateWay gateWay;
        private RPCManager rpcManager;
        private uint authCmd = 0;
        private ISession currInvokingSession;
        private string currInvokingName;

        public void Init(int port)
        {
            gateWay = new GateWay();
            gateWay.Init(port, this);
            rpcManager = new RPCManager();
            rpcManager.Init();
        }

        public void Clean()
        {
            gateWay?.Clean();
            gateWay = null;
            rpcManager?.Clean();
            rpcManager = null;
            listenerHelpers.Clear();
        }
        
        public void SetAuthCmd(uint cmd)
        {
            authCmd = cmd;
        }
        
        public void OnReceive(ISession session, byte[] bytes, int len)
        {
            NetMessage msg = new NetMessage();
            msg.Deserialize(bytes, len);
            if (session.IsAuth())
            {
                if (msg.Head.cmd == 0) //RPC
                {
                    RPCMessage rpcMessage = ProtoBuffUtility.Deserialize<RPCMessage>(msg.content);
                    HandleRPCMessage(session, rpcMessage);
                }
                else //Proto
                {
                    HandlePrptoMessage(session, msg);
                }
            }
            else
            {
                if (msg.Head.cmd == authCmd)
                {
                    HandlePrptoMessage(session, msg);
                } 
                else
                {
                    Debuger.LogWarning("收到未授权的消息! cmd:{0}", msg.Head.cmd);
                }
            }
        }
        
        //----------------------------------------------------------------------------
        
        public void RegisterRPCListener(object listener)
        {
            rpcManager.RegisterListener(listener);
        }

        public void UnRegisterRPCListener(object listener)
        {
            rpcManager.UnRegisterListener(listener);
        }

        private void HandleRPCMessage(ISession session, RPCMessage rpcMessage)
        {
            RPCMethodHelper helper = rpcManager.GetMethodHelper(rpcMessage.name);
            if (helper != null)
            {
                object[] args = new object[rpcMessage.rawargs.Count + 1];
                var rawargs = rpcMessage.rawargs;
                var paramInfo = helper.method.GetParameters();
                args[0] = session;
                if (args.Length == paramInfo.Length)
                {
                    for (int i = 0; i < rawargs.Count; i++)
                    {
                        if (rawargs[i].type == enRPCArgType.PBObject)
                        {
                            args[i + 1] = ProtoBuffUtility.Deserialize(paramInfo[i + 1].ParameterType,rawargs[i].rawValue);
                        }
                        else
                        {
                            args[i + 1] = rawargs[i].value;
                        }
                    }
                    
                    currInvokingName = rpcMessage.name;
                    currInvokingSession = session;
                    
                    try
                    {    
                        helper.method.Invoke(helper.listener, BindingFlags.NonPublic, null, args, null);
                    }
                    catch (Exception e)
                    {
                        Debuger.LogError("RPC调用出错：{0} : {1}\n{2}", rpcMessage.name, e.Message, e.StackTrace);
                    }
                    currInvokingName = null;
                    currInvokingSession = null;
                }
                else
                {
                    Debuger.LogWarning("参数数量不一致！{0}",rpcMessage.name);
                }
            }
            else
            {
                Debuger.LogWarning("RPC不存在！{0}",rpcMessage.name);
            }
        }

        public void Return(params object[] args)
        {
            var name = "On" + currInvokingName;
            RPCMessage rpcmsg = new RPCMessage
            {
                name = name,
                args = args
            };
            byte[] buffer = ProtoBuffUtility.Serialize(rpcmsg);

            NetMessage msg = new NetMessage
            {
                Head = new ProtocolHead {dataSize = (ushort) buffer.Length},
                content = buffer
            };
            
            byte[] tmpBytes = null;
            int len = msg.Serialize(out tmpBytes);
            currInvokingSession.Send(tmpBytes, len);
        }
        
        public void ReturnError(string errinfo, int errcode = 1)
        {
            var name = "On" + currInvokingName + "Error";
            RPCMessage rpcmsg = new RPCMessage
            {
                name = name,
                args = new object[]{errinfo,errcode}
            };
            byte[] buffer = ProtoBuffUtility.Serialize(rpcmsg);

            NetMessage msg = new NetMessage
            {
                Head = new ProtocolHead {dataSize = (ushort) buffer.Length},
                content = buffer
            };
            
            byte[] tmpBytes = null;
            int len = msg.Serialize(out tmpBytes);
            currInvokingSession.Send(tmpBytes, len);
        }
        
        public void Invoke(ISession session, string name, params object[] args)
        {
            RPCMessage rpcmsg = new RPCMessage
            {
                name = name,
                args = args
            };
            byte[] buffer = ProtoBuffUtility.Serialize(rpcmsg);

            NetMessage msg = new NetMessage
            {
                Head = new ProtocolHead {dataSize = (ushort) buffer.Length},
                content = buffer
            };

            byte[] tmp = null;
            int len = msg.Serialize(out tmp);
            session.Send(tmp, len);
        }
        
        public void Invoke(ISession[] listSession, string name, params object[] args)
        {
            RPCMessage rpcmsg = new RPCMessage
            {
                name = name,
                args = args
            };
            byte[] buffer = ProtoBuffUtility.Serialize(rpcmsg);

            NetMessage msg = new NetMessage
            {
                Head = new ProtocolHead {dataSize = (ushort) buffer.Length},
                content = buffer
            };

            byte[] tmp = null;
            int len = msg.Serialize(out tmp);
            foreach (ISession session in listSession)
            {
                session.Send(tmp, len);
            }
        }
        
        //----------------------------------------------------------------------------
        
        
        
        //----------------------------------------------------------------------------
        
        private void HandlePrptoMessage(ISession session, NetMessage msg)
        {
            var helper = listenerHelpers[msg.Head.cmd];
            if (helper != null)
            {
                object obj = ProtoBuffUtility.Deserialize(helper.typeMsg, msg.content);
                if (obj != null)
                {
                    helper.handleMsg.DynamicInvoke(session, msg.Head.index, obj);
                }
            }
            else
            {
                Debuger.LogWarning("未找到对应的监听者! cmd:{0}", msg.Head.cmd);
            }
        }
        
        public void Send<MsgType>(ISession session, uint index, uint cmd, MsgType msg)
        {
            NetMessage msgobj = new NetMessage
            {
                Head =
                {
                    index = index,
                    cmd = cmd,
                    uid = session.uid
                },
                content = ProtoBuffUtility.Serialize(msg)
            };
            msgobj.Head.dataSize = (ushort)msgobj.content.Length;

            byte[] tmp;
            int len = msgobj.Serialize(out tmp);
            session.Send(tmp, len);
        }
        
        public void AddListener<TMsg>(uint cmd, Action<ISession, uint, TMsg> onMsg)
        {

            ListenerHelper helper = new ListenerHelper()
            {
                typeMsg = typeof(TMsg),
                handleMsg = onMsg
            };

            helper.handleMsg = onMsg;
            if (listenerHelpers.ContainsKey(cmd))
            {
                listenerHelpers.Remove(cmd);
            }
            listenerHelpers.Add(cmd, helper);
        }
        
        //----------------------------------------------------------------------------
        
        public void Tick()
        {
            gateWay.Tick();
        }
        
        public void Dump()
        {
            gateWay.Dump();
            StringBuilder sb = new StringBuilder();
            foreach (var pair in listenerHelpers)
            {
                ListenerHelper helper = pair.Value;
                if (helper.handleMsg.Method.DeclaringType != null)
                    sb.AppendFormat("\t<cmd:{0}, msg:{1}, \tlistener:{2}.{3}>\n", pair.Key, helper.typeMsg.Name,
                        helper.handleMsg.Method.DeclaringType.Name, helper.handleMsg.Method.Name);
            }
            Debuger.LogWarning("\nNet Listeners ({0}):\n{1}", listenerHelpers.Count, sb);
            rpcManager.Dump();
        }

    }
}