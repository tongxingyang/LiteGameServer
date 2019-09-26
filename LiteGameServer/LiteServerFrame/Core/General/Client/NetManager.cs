using System;
using System.Reflection;
using DebugerTool;
using LiteServerFrame.Core.General.Base;
using LiteServerFrame.Utility;

namespace LiteServerFrame.Core.General.Client
{
    public class NetManager
    {
        class ListenerHelper
        {
            public uint cmd;
            public uint index;
            public Type typeMsg;
            public Delegate successHandle;
            public Delegate errorHandle;
            public float timeout;
            public float timestamp;
        }
        
        private IConnection connection;
        private uint uid;
        private RPCManager rpcManager;
        private string currInvokingName;

        public void Init(int connID, int bindPort)
        {
            Debuger.Log("connId:{0}, bindPort:{1}", connID, bindPort);
            connection = new KCPConnection();
            connection.Init(connID, bindPort);
            rpcManager = new RPCManager();
            rpcManager.Init();
        }

        public void Clean()
        {
            connection?.Clean();
            connection = null;
        }

        public void SetUserID(uint id)
        {
            this.uid = id;
        }

        public void Close()
        {
            connection?.Close();
        }

        public void Connect(string ip, int port)
        {
            if (connection.Connected)
            {
                Debuger.Log("当前已有连接，先关闭旧的连接");
                return;
            }
            connection.Connect(ip, port);
            connection.onReceive = OnReceive;
        }

        public void Tick()
        {
            connection?.Tick();
            CheckTimeOut();
        }
        
        private void OnReceive(byte[] bytes, int len)
        {
            NetMessage msg = new NetMessage();
            msg.Deserialize(bytes, len);
            if (msg.Head.cmd == 0)
            {
                RPCMessage rpcmsg = ProtoBuffUtility.Deserialize<RPCMessage>(msg.content);
                HandleRPCMessage(rpcmsg);
            }
            else
            {
                HandlePrptoMessage(msg);
            }
        }
        
        //--------------------------------------------------------------------------
        
        public void RegisterRPCListener(object listener)
        {
            rpcManager.RegisterListener(listener);
        }

        public void UnRegisterRPCListener(object listener)
        {
            rpcManager.UnRegisterListener(listener);
        }

        private void HandleRPCMessage(RPCMessage rpcmsg)
        {
            Debuger.Log("Connection[{0}]-> {1}({2})", connection.ID, rpcmsg.name, rpcmsg.args);
            var helper = rpcManager.GetMethodHelper(rpcmsg.name);
            if (helper != null)
            {
                object[] args = rpcmsg.args;
                var rawargs = rpcmsg.rawargs;
                var paramInfo = helper.method.GetParameters();

                if (rawargs.Count == paramInfo.Length)
                {
                    for (int i = 0; i < rawargs.Count; i++)
                    {
                        if (rawargs[i].type == enRPCArgType.PBObject)
                        {
                            var type = paramInfo[i].ParameterType;
                            object arg = ProtoBuffUtility.Deserialize(type, rawargs[i].rawValue);
                            args[i] = arg;
                        }
                    }
                    currInvokingName = rpcmsg.name;
                    try
                    {
                        helper.method.Invoke(helper.listener, BindingFlags.NonPublic, null, args, null);
                    }
                    catch (Exception e)
                    {
                        Debuger.LogError("RPC调用出错：{0}: {1}\n{2}", rpcmsg.name, e.Message, e.StackTrace);
                    }
                    currInvokingName = null;
                }
                else
                {
                    Debuger.LogWarning("参数数量不一致！{0}",rpcmsg.name);
                }
                
            }
            else
            {
                Debuger.LogWarning("RPC不存在！{0} ",rpcmsg.name);
            }
        }
        
        public void Invoke(string name, params object[] args)
        {

            RPCMessage rpcmsg = new RPCMessage
            {
                name = name,
                args = args
            };
            byte[] buffer = ProtoBuffUtility.Serialize(rpcmsg);

            NetMessage msg = new NetMessage
            {
                Head = new ProtocolHead
                {
                    uid = uid,
                    dataSize = (ushort) buffer.Length
                },
                content = buffer
            };
            byte[] tmp = null;
            int len = msg.Serialize(out tmp);
            connection.Send(tmp, len);
        }

        public void Return(params object[] args)
        {
            if (connection != null)
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
                    Head = new ProtocolHead
                    {
                        uid = uid,
                        dataSize = (ushort) buffer.Length
                    },
                    content = buffer
                };

                byte[] tmp = null;
                int len = msg.Serialize(out tmp);
                connection.Send(tmp, len);
            }
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
            
            byte[] tmp = null;
            int len = msg.Serialize(out tmp);
            connection.Send(tmp, len);
        }
        
        //--------------------------------------------------------------------------
    }
}