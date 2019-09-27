using LiteServerFrame.Core.General.Base;
using ProtoBuf;

namespace LiteServerFrame.Core.General.IPC
{
    [ProtoContract]
    public class IPCMessage
    {
        [ProtoMember(1)] public int srcID;
        [ProtoMember(2)] public RPCMessage rpcMessage;
    }
}