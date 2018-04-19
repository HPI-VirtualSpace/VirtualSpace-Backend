using ProtoBuf;

namespace VirtualSpace.Shared
{
    [ProtoInclude(500, typeof(MessageBase))]
    [ProtoContract(ImplicitFields = ImplicitFields.AllFields)]
    public interface IMessageBase
    {

    }
    
    [ProtoInclude(500, typeof(PayloadMessage))]
    [ProtoContract(ImplicitFields = ImplicitFields.AllFields)]
    public class NetworkMessage
    {
        public int sessionId;
    }
    
    [ProtoContract(ImplicitFields = ImplicitFields.AllFields)]
    public class PayloadMessage : NetworkMessage
    {
        public IMessageBase payload;
    }
}
