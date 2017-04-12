// <copyright file="IFFChunk.cs" company="">
// TODO: Update copyright text.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DjvuNet.DataChunks.Directory;
using DjvuNet;

namespace DjvuNet.DataChunks
{
    /// <summary>
    /// TODO: Update summary.
    /// </summary>
    public abstract class IffChunk : IDjvuNode
    {
        #region Public Properties

        /// <summary>
        /// Gets the reader for the chunk data
        /// </summary>
        //[DataMember]
        public IDjvuReader Reader { get; set; }

        /// <summary>
        /// Gets the chunk type
        /// </summary>
        public abstract ChunkType ChunkType { get; }

        /// <summary>
        /// Gets the parent for the IFF chunk
        /// </summary>
        public IDjvuNode Parent { get; set; }

        /// <summary>
        /// Gets the length of the chunk data
        /// </summary>
        public long Length { get; set; }

        /// <summary>
        /// Gets the chunk identifier
        /// </summary>
        public string ChunkID { get; internal set; }

        /// <summary>
        /// Gets the offset to the start of the chunk data
        /// </summary>
        public long Offset { get; set; }

        /// <summary>
        /// Gets the name of the chunk
        /// </summary>
        //[DataMember]
        public string Name { get { return ChunkType.ToString(); } }

        /// <summary>
        /// True if the chunk is a sub form chunk, false otherwise
        /// </summary>
        public bool IsFormChunk (ChunkType type)
        {
            return type == ChunkType.Djvu || type == ChunkType.Djvi || 
                    type == ChunkType.Thum || type == ChunkType.Djvm || type == ChunkType.Form;
        }
        
        /// <summary>
        /// Three types of FORM chunks could form a root of DjVu document / file:
        /// DJVU for single page documents, DJVM for multi page documents 
        /// and DJVI for include files associated with separate multi page 
        /// document - include files can not function independently (?).
        /// 
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public bool IsRootFormChild(ChunkType type)
        {
            // DJVM form is the only chunk type which is always in document root
            if (type == ChunkType.Djvm) return false;

            return IsFormChunk(type) || type == ChunkType.Dirm || 
                type == ChunkType.Navm;
        }

        /// <summary>
        /// Gets the root Djvu document for the form
        /// </summary>
        public IDjvuDocument Document { get; internal set; }

        public bool IsInitialized { get; internal set; }

        public virtual byte[] ChunkData { get; set; }

        public IDjvuRootElement RootElement { get; set; }

        #endregion Public Properties

        #region Constructors

        /// <summary>
        /// IFFChunk parameterless constructor mainly used for testing
        /// </summary>
        protected IffChunk() { }

        /// <summary>
        /// IFFChunk constructor
        /// </summary>
        /// <param name="reader"></param>
        /// <param name="parent"></param>
        /// <param name="document"></param>
        /// <param name="chunkID"></param>
        /// <param name="length"></param>
        public IffChunk(IDjvuReader reader, IffChunk parent, IDjvuDocument document, 
            string chunkID = "", long length = 0)
        {
            Reader = reader;
            Parent = parent;
            Document = document;
            Length = length;
            ChunkID = chunkID;
            Offset = reader.Position;
        }

        public virtual void Initialize()
        {
            Initialize(Reader);
        }

        /// <summary>
        /// Initialize allows to delay reading of IFFChunk content to the moment it is needed.
        /// </summary>
        /// <param name="reader"></param>
        public virtual void Initialize(IDjvuReader reader)
        {
            reader.Position = Offset;
            ReadChunkData(reader);
            IsInitialized = true;
        }

        #endregion Constructors

        #region Public Methods

        /// <summary>
        /// Gets the chunk type based on the ID
        /// </summary>
        /// <param name="ID"></param>
        /// <returns></returns>
        public static ChunkType GetChunkType(string ID)
        {
            try { return (ChunkType)Enum.Parse(typeof(ChunkType), ID, true); }
            catch (ArgumentException) { } // Catch unsupported chunks i.e. "LTAnno"

            return ChunkType.Unknown;
        }

        /// <summary>
        /// Builds the appropriate chunk for the ID
        /// </summary>
        /// <returns></returns>
        public static IffChunk BuildIFFChunk(IDjvuReader reader, IDjvuDocument rootDocument, 
            IffChunk parent, ChunkType chunkType,
            string chunkID = "", long length = 0)
        {
            IffChunk result = null;

            switch (chunkType)
            {
                case ChunkType.Djvm:
                    result = new DjvmChunk(reader, parent, rootDocument, chunkID, length);
                    break;
                case ChunkType.Djvu:
                    result = new DjvuChunk(reader, parent, rootDocument, chunkID, length);
                    break;
                case ChunkType.Djvi:
                    result = new DjviChunk(reader, parent, rootDocument, chunkID, length);
                    break;
                case ChunkType.Thum:
                    result = new ThumChunk(reader, parent, rootDocument, chunkID, length);
                    break;
                case ChunkType.Dirm:
                    result = new DirmChunk(reader, parent, rootDocument, chunkID, length);
                    break;
                case ChunkType.Navm:
                    result = new NavmChunk(reader, parent, rootDocument, chunkID, length);
                    break;
                case ChunkType.Anta:
                    result = new AntaChunk(reader, parent, rootDocument, chunkID, length);
                    break;
                case ChunkType.Antz:
                    result = new AntzChunk(reader, parent, rootDocument, chunkID, length);
                    break;
                case ChunkType.Txta:
                    result = new TxtaChunk(reader, parent, rootDocument, chunkID, length);
                    break;
                case ChunkType.Txtz:
                    result = new TxtzChunk(reader, parent, rootDocument, chunkID, length);
                    break;
                case ChunkType.Djbz:
                    result = new DjbzChunk(reader, parent, rootDocument, chunkID, length);
                    break;
                case ChunkType.Sjbz:
                    result = new SjbzChunk(reader, parent, rootDocument, chunkID, length);
                    break;
                case ChunkType.FG44:
                    result = new FG44Chunk(reader, parent, rootDocument, chunkID, length);
                    break;
                case ChunkType.BG44:
                    result = new BG44Chunk(reader, parent, rootDocument, chunkID, length);
                    break;
                case ChunkType.TH44:
                    result = new TH44Chunk(reader, parent, rootDocument, chunkID, length);
                    break;
                case ChunkType.WMRM:
                    result = new WmrmChunk(reader, parent, rootDocument, chunkID, length);
                    break;
                case ChunkType.FGbz:
                    result = new FGbzChunk(reader, parent, rootDocument, chunkID, length);
                    break;
                case ChunkType.Info:
                    result = new InfoChunk(reader, parent, rootDocument, chunkID, length);
                    break;
                case ChunkType.Incl:
                    result = new InclChunk(reader, parent, rootDocument, chunkID, length);
                    break;
                case ChunkType.BGjp:
                    result = new BGjpChunk(reader, parent, rootDocument, chunkID, length);
                    break;
                case ChunkType.FGjp:
                    result = new FGjpChunk(reader, parent, rootDocument, chunkID, length);
                    break;
                case ChunkType.Smmr:
                    result = new SmmrChunk(reader, parent, rootDocument, chunkID, length);
                    break;
                case ChunkType.Cida:
                    result = new CidaChunk(reader, parent, rootDocument, chunkID, length);
                    break;
                default:
                    result = new UnknownChunk(reader, parent, rootDocument, chunkID, length);
                    break;
            }
           
            return result;
        }

        /// <summary>
        /// Gets all the children items of the given type
        /// </summary>
        /// <typeparam name="TItem"></typeparam>
        /// <returns></returns>
        public TItem[] GetChildrenItems<TItem>() where TItem : IffChunk
        {
            return GetChildrenItems<TItem>(this);
        }

        /// <summary>
        /// Gets the string representation of the item
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return $"{this.GetType().Name} {{ Name: {Name}; ID: {ChunkID} Offset: {Offset} Length: {Length} }}";
        }

        #endregion Public Methods

        #region Protected Methods

        /// <summary>
        /// Reads the data for the chunk
        /// </summary>
        /// <param name="reader"></param>
        protected abstract void ReadChunkData(IDjvuReader reader);

        #endregion Protected Methods

        #region Private Methods

        /// <summary>
        /// Gets all the children items of the given type
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="page"></param>
        /// <returns></returns>
        internal T[] GetChildrenItems<T>(IffChunk page) where T : IffChunk
        {
            // Check if this is a thumbnail
            if (page is T)
            {
                return new T[] { (T)page };
            }

            // No items if not form
            if (page is FormChunk == false)
            {
                return new T[0];
            }

            List<T> results = new List<T>();
            FormChunk form = (FormChunk)page;

            foreach (IffChunk chunk in form.Children)
            {
                results.AddRange(GetChildrenItems<T>(chunk));
            }

            return results.ToArray();
        }

        #endregion Private Methods
    }
}