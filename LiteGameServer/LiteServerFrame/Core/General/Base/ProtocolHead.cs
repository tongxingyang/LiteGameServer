namespace LiteServerFrame.Core.General.Base
{
    public class ProtocolHead
    {
        public uint uid = 0;
        public uint cmd = 0;
        public uint index = 0;
        public ushort dataSize = 0;
        public ushort checkSum = 0;
        
        public ProtocolHead Deserialize(NetBuffer buffer)
        {
            ProtocolHead head = this;
            head.uid = buffer.ReadUInt();
            head.cmd = buffer.ReadUInt();
            head.index = buffer.ReadUInt();
            head.dataSize = buffer.ReadUShort();
            head.checkSum = buffer.ReadUShort();
            return head;
        }
        
        public NetBuffer Serialize(NetBuffer buffer)
        {
            buffer.WriteUInt(uid);
            buffer.WriteUInt(cmd);
            buffer.WriteUInt(index);
            buffer.WriteUShort(dataSize);
            buffer.WriteUShort(checkSum);
            return buffer;
        }
    }
}