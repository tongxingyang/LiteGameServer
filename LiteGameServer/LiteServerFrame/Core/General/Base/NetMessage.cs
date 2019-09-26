namespace LiteServerFrame.Core.General.Base
{
    public class NetMessage
    {
        private static readonly NetBuffer defaultWriter = new NetBuffer(4096);
        private static readonly NetBuffer defaultReader = new NetBuffer(4096);
        public ProtocolHead Head = new ProtocolHead();
        public byte[] content;
        
        public NetMessage Deserialize(NetBuffer buffer)
        {
            Head.Deserialize(buffer);
            content = new byte[Head.dataSize];
            buffer.ReadBytes(content, 0, Head.dataSize);
            return this;
        }

        public NetBuffer Serialize(NetBuffer buffer)
        {
            Head.Serialize(buffer);
            buffer.WriteBytes(content, 0, Head.dataSize);
            return buffer;
        }
        
        public NetMessage Deserialize(byte[] buffer, int size)
        {
            lock (defaultReader)
            {
                defaultReader.Attach(buffer, size);
                return Deserialize(defaultReader);
            }
        }

        public int Serialize(out byte[] tempBuffer)
        {
            lock (defaultWriter)
            {
                defaultWriter.Clear();
                this.Serialize(defaultWriter);
                tempBuffer = defaultWriter.GetBytes();
                return defaultWriter.Length;
            }

        }

    }
}