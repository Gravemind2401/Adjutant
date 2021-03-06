﻿using Adjutant.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Endian;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing.Dds;
using Adjutant.Blam.Common;

namespace Adjutant.Saber3D.Halo1X
{
    public class Texture : IBitmap, IBitmapData
    {
        private const int LittleHeader = 0x50494354; //TCIP
        private const int BigHeader = 0x54434950; //PICT

        private readonly PakItem item;
        private readonly bool isBigEndian;

        public Texture(PakItem item)
        {
            this.item = item;

            using (var x = item.Container.CreateReader())
            using (var reader = x.CreateVirtualReader(item.Address))
            {
                reader.Seek(6, SeekOrigin.Begin);
                var head = reader.ReadInt32();
                if (head == LittleHeader)
                    reader.ByteOrder = ByteOrder.LittleEndian;
                else
                {
                    reader.Seek(8, SeekOrigin.Begin);
                    head = reader.ReadInt32();

                    if (head == BigHeader)
                        reader.ByteOrder = ByteOrder.BigEndian;
                    else throw Exceptions.NotASaberTextureItem(item);

                    isBigEndian = true;
                }

                reader.Seek(isBigEndian ? 12 : 16, SeekOrigin.Begin);
                Width = reader.ReadInt32();
                Height = reader.ReadInt32();

                reader.Seek(isBigEndian ? 24 : 28, SeekOrigin.Begin);
                MapCount = reader.ReadInt32();

                reader.Seek(isBigEndian ? 32 : 38, SeekOrigin.Begin);
                Format = (TextureFormat)reader.ReadInt32();
                if (Format == TextureFormat.AlsoDXT1)
                    Format = TextureFormat.DXT1; //for compatibility with KnownTextureFormat

                DataOffset = isBigEndian ? 4096 : 58;
            }
        }

        public int Width { get; }
        public int Height { get; }
        public int MapCount { get; }
        public TextureFormat Format { get; }
        public int DataOffset { get; }

        #region IBitmap

        string IBitmap.SourceFile => item.Container.FileName;

        int IBitmap.Id => item.Address;

        string IBitmap.Name => item.Name;

        string IBitmap.Class => item.ItemType.ToString();

        int IBitmap.SubmapCount => 1;

        CubemapLayout IBitmap.CubeLayout => CubemapLayout.NonCubemap;

        DdsImage IBitmap.ToDds(int index)
        {
            if (index < 0 || index >= 1)
                throw new ArgumentOutOfRangeException(nameof(index));

            byte[] data;
            using (var reader = item.Container.CreateReader())
            {
                reader.Seek(item.Address + DataOffset, SeekOrigin.Begin);
                data = reader.ReadBytes(TextureUtils.GetBitmapDataLength(this, false));
            }

            return TextureUtils.GetDds(this, data, false);
        }
        #endregion

        #region IBitmapData

        ByteOrder IBitmapData.ByteOrder => isBigEndian ? ByteOrder.BigEndian : ByteOrder.LittleEndian;
        bool IBitmapData.UsesPadding => false;
        MipmapLayout IBitmapData.CubeMipLayout => MipmapLayout.None;
        MipmapLayout IBitmapData.ArrayMipLayout => MipmapLayout.None;

        int IBitmapData.Depth => 1;
        int IBitmapData.MipmapCount => 0;
        int IBitmapData.FrameCount => MapCount;

        object IBitmapData.BitmapFormat => Format;
        object IBitmapData.BitmapType => "Texture2D";

        bool IBitmapData.Swizzled => false;

        #endregion
    }

    public enum TextureFormat
    {
        A8R8G8B8 = 0,
        A8Y8 = 10,
        DXT1 = 12,
        AlsoDXT1 = 13,
        DXT3 = 15,
        DXT5 = 17,
        X8R8G8B8 = 22,
        DXN = 36,
        DXT5a = 37
    }
}
