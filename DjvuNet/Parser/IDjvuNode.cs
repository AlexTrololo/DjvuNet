﻿using DjvuNet.DataChunks;

namespace DjvuNet
{
    public interface IDjvuNode
    {
        ChunkType ChunkType { get; }

        string ChunkID { get; set; }

        long DataOffset { get; set; }

        long NodeOffset { get; set; }

        uint OffsetDiff { get; }

        long Length { get; set; }

        bool IsDirty { get; set; }

        long GetDataLength();

        void Initialize();

        void Initialize(IDjvuReader reader);

        bool IsInitialized { get; }

        byte[] ChunkData { get; set; }

        string Name { get; }

        IDjvuElement Parent { get; set; }

        IDjvuRootElement RootElement { get; set; }

        IDjvuDocument Document { get; set; }

        IDjvuReader Reader { get; set; }

        void ReadData(IDjvuReader reader);

        void WriteData(IDjvuWriter writer, bool writeHeader);
    }
}
