using System;
using ProtoBuf;

namespace CommonData.Data
{
    [ProtoContract]
    public class AccountData
    {
        [ProtoMember(1)]
        public virtual int ID { get; set; }
        [ProtoMember(2)]
        public virtual string AccountName { get; set; }
        [ProtoMember(3)]
        public virtual string Passworld { get; set; }
        [ProtoMember(4)]
        public virtual int ServerID { get; set; }
        [ProtoMember(5)]
        public virtual DateTime RegisterTime { get; set; }
    }
}
