using System.IO;
using ProtoBuf;

namespace VirtualSpace.Shared
{
    public static class ProtobufUtility
    {
        public static byte[] Serialize<T>(T message)
        {
            var messageMemoryStream = new MemoryStream();
            Serializer.Serialize(messageMemoryStream, message);
            return messageMemoryStream.ToArray();
        }

        public static T Unserialize<T>(byte[] data)
        {
            var messageMemoryStream = new MemoryStream(data);
            T message = Serializer.Deserialize<T>(messageMemoryStream);
            return message;
        }
    }
}
