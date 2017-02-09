using System.IO;

namespace EventStore.Core.TransactionLog.Chunks.TFChunk
{
    public struct PosMap
    {
        public const byte CurrentPosMapVersion = PosMapVersion.PosMapV3;

        public const int FullSize = sizeof(long) + sizeof(int) + sizeof(int);
        public const int Old12ByteSize = sizeof(long) + sizeof(int);
        public const int DeprecatedSize = sizeof(long) + sizeof(int);

        public readonly long LogPos;
        public readonly int ActualPos;
        public readonly int LengthOffset;

        public PosMap(long logPos, int actualPos, int lengthOffset = 0)
        {
            LogPos = logPos;
            ActualPos = actualPos;
            LengthOffset = lengthOffset;
        }

        public static PosMap FromNewFormat(BinaryReader reader)
        {
            var actualPos = reader.ReadInt32();
            var logPos = reader.ReadInt64();
            var lengthOffset = reader.ReadInt32();
            return new PosMap(logPos, actualPos, lengthOffset);
        }
        
        public static PosMap From12ByteFormat(BinaryReader reader)
        {
            var actualPos = reader.ReadInt32();
            var logPos = reader.ReadInt64();
            return new PosMap(logPos, actualPos);
        }

        public static PosMap FromOldFormat(BinaryReader reader)
        {
            var posmap = reader.ReadUInt64();
            var logPos = (int)(posmap >> 32);
            var actualPos = (int)(posmap & 0xFFFFFFFF);
            return new PosMap(logPos, actualPos);
        }

        public void Write(BinaryWriter writer)
        {
            writer.Write(ActualPos);
            writer.Write(LogPos);
            writer.Write(LengthOffset);
        }

        public override string ToString()
        {
            return string.Format("LogPos: {0}, ActualPos: {1}, LengthOffset: {2}", LogPos, ActualPos, LengthOffset);
        }
    }
}