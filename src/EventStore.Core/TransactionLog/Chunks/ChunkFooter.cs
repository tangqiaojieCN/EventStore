using System;
using System.IO;
using EventStore.Common.Utils;
using EventStore.Core.TransactionLog.Chunks.TFChunk;

namespace EventStore.Core.TransactionLog.Chunks
{
    public static class PosMapVersion {
        public const byte PosMapV3 = 3;
        public const byte PosMapV2 = 2;
        public const byte PosMapV1 = 1;
    }

    public class ChunkFooter
    {
        public const int Size = 128;
        public const int ChecksumSize = 16;

        // flags within single byte
        public readonly bool IsCompleted;
        public readonly byte MapVersion;

        public readonly int PhysicalDataSize; // the size of a section of data in chunk
        public readonly long LogicalDataSize;  // the size of a logical data size (after scavenge LogicalDataSize can be > physicalDataSize)
        public readonly int MapSize;
        public readonly byte[] MD5Hash;

        public readonly int MapCount; // calculated, not stored

        public ChunkFooter(bool isCompleted, byte mapVersion, int physicalDataSize, long logicalDataSize, int mapSize, byte[] md5Hash)
        {
            Ensure.Nonnegative(physicalDataSize, "physicalDataSize");
            Ensure.Nonnegative(logicalDataSize, "logicalDataSize");
            // if (logicalDataSize < physicalDataSize)
            //     throw new ArgumentOutOfRangeException("logicalDataSize", string.Format("LogicalDataSize {0} is less than PhysicalDataSize {1}", logicalDataSize, physicalDataSize));
            Ensure.Nonnegative(mapSize, "mapSize");
            Ensure.NotNull(md5Hash, "md5Hash");
            if (md5Hash.Length != ChecksumSize)
                throw new ArgumentException("MD5Hash is of wrong length.", "md5Hash");

            IsCompleted = isCompleted;
            Console.WriteLine("MapVersion passed in: {0}", mapVersion);
            MapVersion = mapVersion;

            PhysicalDataSize = physicalDataSize;
            LogicalDataSize = logicalDataSize;
            MapSize = mapSize;
            MD5Hash = md5Hash;

            var posMapSize = PosMap.FullSize;
            if(MapVersion == PosMapVersion.PosMapV2) {
                posMapSize = PosMap.Old12ByteSize;
            }
            else if(MapVersion == PosMapVersion.PosMapV1) {
                posMapSize = PosMap.DeprecatedSize;
            }
            if (MapSize % posMapSize != 0)
                throw new Exception(string.Format("Wrong MapSize {0} -- not divisible by PosMap.Size {1}.", MapSize, posMapSize));
            MapCount = mapSize / posMapSize;
        }

        public byte[] AsByteArray()
        {
            var array = new byte[Size];
            using (var memStream = new MemoryStream(array))
            using (var writer = new BinaryWriter(memStream))
            {
                var flags = (byte) ((IsCompleted ? 1 : 0) |
                                    (MapVersion == PosMapVersion.PosMapV2 ? 2 : 0) |
                                    (MapVersion == PosMapVersion.PosMapV3 ? 4 : 0));
                writer.Write(flags);
                writer.Write(PhysicalDataSize);
                if (MapVersion != PosMapVersion.PosMapV1)
                    writer.Write(LogicalDataSize);
                else
                    writer.Write((int)LogicalDataSize);
                writer.Write(MapSize);
                
                memStream.Position = Size - ChecksumSize;
                writer.Write(MD5Hash);
            }
            return array;
        }

        public static ChunkFooter FromStream(Stream stream)
        {
            var reader = new BinaryReader(stream);
            var flags = reader.ReadByte();
            var isCompleted = (flags & 1) != 0;
            var isMap12Bytes = (flags & 2) != 0;
            var isMap16Bytes = (flags & 4) != 0;
            var physicalDataSize = reader.ReadInt32();
            var logicalDataSize = isMap12Bytes || isMap16Bytes ? reader.ReadInt64() : reader.ReadInt32();
            var mapSize = reader.ReadInt32();

            stream.Seek(-ChecksumSize, SeekOrigin.End);
            var hash = reader.ReadBytes(ChecksumSize);

            var version = PosMapVersion.PosMapV1;
            if(isMap12Bytes) {
                version = PosMapVersion.PosMapV2;
            } else if(isMap16Bytes) {
                version = PosMapVersion.PosMapV3;
            }
            return new ChunkFooter(isCompleted, version, physicalDataSize, logicalDataSize, mapSize, hash);
        }
    }
}