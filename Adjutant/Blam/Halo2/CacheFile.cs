﻿using Adjutant.Blam.Definitions;
using Adjutant.IO;
using Adjutant.Utilities;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Endian;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Adjutant.Blam.Halo2
{
    public class CacheFile : ICacheFile
    {
        public string FileName { get; private set; }
        public string BuildString => Header.BuildString;
        public CacheType Type => Header.CacheType;

        public CacheHeader Header { get; private set; }
        public CacheIndex Index { get; private set; }

        public HeaderAddressTranslator HeaderTranslator { get; private set; }
        public TagAddressTranslator MetadataTranslator { get; private set; }

        public CacheFile(string fileName)
        {
            FileName = fileName;
            HeaderTranslator = new HeaderAddressTranslator(this);
            MetadataTranslator = new TagAddressTranslator(this);

            using (var reader = CreateReader(HeaderTranslator))
            {
                Header = reader.ReadObject<CacheHeader>();
                reader.Seek(Header.IndexAddress, SeekOrigin.Begin);
                Index = reader.ReadObject(new CacheIndex(this));
                Index.ReadItems();
            }
        }

        public DependencyReader CreateReader(IAddressTranslator translator)
        {
            var fs = new FileStream(FileName, FileMode.Open, FileAccess.Read);
            var reader = new DependencyReader(fs, ByteOrder.LittleEndian);
            reader.RegisterType<CacheFile>(() => this);
            reader.RegisterType<Pointer>(() => new Pointer(reader.ReadInt32(), translator));
            reader.RegisterType<IAddressTranslator>(() => translator);
            return reader;
        }
    }

    [FixedSize(2048)]
    public class CacheHeader
    {
        [Offset(0)]
        public int Head { get; set; }

        [Offset(36)]
        [VersionNumber]
        public int Version { get; set; }

        [Offset(8)]
        public int FileSize { get; set; }

        [Offset(16)]
        public int IndexAddress { get; set; }

        [Offset(20)]
        public int MetadataAddress { get; set; }

        [Offset(288, MinVersion = 0)]
        [Offset(300, MaxVersion = 0)]
        [NullTerminated(Length = 32)]
        public string BuildString { get; set; }

        [Offset(356)]
        public int StringCount { get; set; }

        [Offset(360)]
        public int StringTableSize { get; set; }

        [Offset(364)]
        public int StringTableIndexAddress { get; set; }

        [Offset(368)]
        public int StringTableAddress { get; set; }

        [Offset(444, MinVersion = 0)]
        [Offset(456, MaxVersion = 0)]
        [NullTerminated(Length = 256)]
        public string ScenarioName { get; set; }

        [Offset(704, MinVersion = 0)]
        [Offset(716, MaxVersion = 0)]
        public int FileCount { get; set; }

        [Offset(708, MinVersion = 0)]
        [Offset(720, MaxVersion = 0)]
        public int FileTableAddress { get; set; }

        [Offset(712, MinVersion = 0)]
        [Offset(724, MaxVersion = 0)]
        public int FileTableSize { get; set; }

        [Offset(716, MinVersion = 0)]
        [Offset(728, MaxVersion = 0)]
        public int FileTableIndexOffset { get; set; }

        public CacheType CacheType
        {
            get
            {
                switch (Version)
                {
                    case 0: return CacheType.Halo2Xbox;
                    case -1: return CacheType.Halo2Vista;
                    default: return CacheType.Unknown;
                }
            }
        }
    }

    [FixedSize(32)]
    public class CacheIndex : IEnumerable<IndexItem>
    {
        private readonly CacheFile cache;
        private readonly List<IndexItem> items;
        private readonly Dictionary<int, string> filenames;

        internal Dictionary<int, string> Filenames => filenames;

        [Offset(0)]
        public int Magic { get; set; }

        [Offset(4)]
        public int TagClassCount { get; set; }

        [Offset(8)]
        public Pointer TagInfoAddress { get; set; }

        [Offset(24)]
        public int TagCount { get; set; }

        public CacheIndex(CacheFile cache)
        {
            if (cache == null)
                throw new ArgumentNullException(nameof(cache));

            this.cache = cache;
            items = new List<IndexItem>();
            filenames = new Dictionary<int, string>();
        }

        internal void ReadItems()
        {
            if (items.Any())
                throw new InvalidOperationException();

            using (var reader = cache.CreateReader(cache.MetadataTranslator))
            {
                for (int i = 0; i < TagCount; i++)
                {
                    reader.Seek(TagInfoAddress.Address + i * 16, SeekOrigin.Begin);

                    var item = reader.ReadObject(new IndexItem(cache));
                    items.Add(item);
                }

                reader.Seek(cache.Header.FileTableIndexOffset, SeekOrigin.Begin);
                var indices = reader.ReadEnumerable<int>(TagCount).ToArray();

                for (int i = 0; i < TagCount; i++)
                {
                    reader.Seek(cache.Header.FileTableAddress + indices[i], SeekOrigin.Begin);
                    filenames.Add(i, reader.ReadNullTerminatedString());
                }
            }
        }

        public IndexItem this[int index] => items[index];

        public IEnumerator<IndexItem> GetEnumerator() => items.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => items.GetEnumerator();
    }

    [FixedSize(16)]
    public class IndexItem : IIndexItem
    {
        private readonly CacheFile cache;

        public IndexItem(CacheFile cache)
        {
            this.cache = cache;
        }

        [Offset(0)]
        [ByteOrder(ByteOrder.BigEndian)]
        public int ClassId { get; set; }

        [Offset(4)]
        [StoreType(typeof(ushort))]
        public int Id { get; set; }

        [Offset(8)]
        public Pointer MetaPointer { get; set; }

        [Offset(12)]
        public int MetaSize { get; set; }

        public string ClassCode => Encoding.UTF8.GetString(BitConverter.GetBytes(ClassId)).TrimEnd();

        public string FileName => cache.Index.Filenames[Id];

        public override string ToString()
        {
            return Utils.CurrentCulture($"[{ClassCode}] {FileName}");
        }
    }
}