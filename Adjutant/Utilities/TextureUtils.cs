﻿using Adjutant.Blam.Common;
using System;
using System.Collections.Generic;
using System.Drawing.Dds;
using System.IO.Endian;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace Adjutant.Utilities
{
    public static class TextureUtils
    {
        #region Lookups

        private static readonly Dictionary<KnownTextureFormat, DxgiFormat> dxgiLookup = new Dictionary<KnownTextureFormat, DxgiFormat>
        {
            { KnownTextureFormat.DXT1, DxgiFormat.BC1_UNorm },
            { KnownTextureFormat.DXT3, DxgiFormat.BC2_UNorm },
            { KnownTextureFormat.DXT5, DxgiFormat.BC3_UNorm },
            { KnownTextureFormat.BC7_unorm, DxgiFormat.BC7_UNorm },
            { KnownTextureFormat.A8R8G8B8, DxgiFormat.B8G8R8A8_UNorm },
            { KnownTextureFormat.X8R8G8B8, DxgiFormat.B8G8R8X8_UNorm },
            { KnownTextureFormat.R5G6B5, DxgiFormat.B5G6R5_UNorm },
            { KnownTextureFormat.A1R5G5B5, DxgiFormat.B5G5R5A1_UNorm },
            { KnownTextureFormat.A4R4G4B4, DxgiFormat.B4G4R4A4_UNorm }
        };

        private static readonly Dictionary<KnownTextureFormat, XboxFormat> xboxLookup = new Dictionary<KnownTextureFormat, XboxFormat>
        {
            { KnownTextureFormat.A8, XboxFormat.A8 },
            { KnownTextureFormat.A8Y8, XboxFormat.Y8A8 },
            { KnownTextureFormat.AY8, XboxFormat.AY8 },
            { KnownTextureFormat.CTX1, XboxFormat.CTX1 },
            { KnownTextureFormat.DXT3a_mono, XboxFormat.DXT3a_mono },
            { KnownTextureFormat.DXT3a_alpha, XboxFormat.DXT3a_alpha },
            { KnownTextureFormat.BC4_unorm, XboxFormat.DXT5a_scalar },
            { KnownTextureFormat.DXT5a, XboxFormat.DXT5a_scalar },
            { KnownTextureFormat.DXT5a_mono, XboxFormat.DXT5a_mono },
            { KnownTextureFormat.DXT5a_alpha, XboxFormat.DXT5a_alpha },
            { KnownTextureFormat.DXN, XboxFormat.DXN },
            { KnownTextureFormat.DXN_SNorm, XboxFormat.DXN_SNorm },
            { KnownTextureFormat.DXN_mono_alpha, XboxFormat.DXN_mono_alpha },
            { KnownTextureFormat.P8, XboxFormat.Y8 },
            { KnownTextureFormat.P8_bump, XboxFormat.Y8 },
            { KnownTextureFormat.U8V8, XboxFormat.V8U8 },
            { KnownTextureFormat.Y8, XboxFormat.Y8 }
        };

        #endregion

        #region Extensions

        private enum KnownTextureFormat
        {
            Unknown,
            A8,
            Y8,
            AY8,
            A8Y8,
            R5G6B5,
            A1R5G5B5,
            A4R4G4B4,
            X8R8G8B8,
            A8R8G8B8,
            DXT1,
            DXT3,
            DXT5,
            P8_bump,
            P8,
            ARGBFP32,
            RGBFP32,
            RGBFP16,
            U8V8,
            DXT5a,
            DXN,
            DXN_SNorm,
            CTX1,
            DXT3a_alpha,
            DXT3a_mono,
            DXT5a_alpha,
            DXT5a_mono,
            DXN_mono_alpha,
            BC4_unorm, //same as DXT5a
            BC7_unorm
        }

        private enum KnownTextureType : short
        {
            Texture2D,
            Texture3D,
            CubeMap,
            Array
        }

        private static T ParseToEnum<T>(this object input, T defaultValue = default(T)) where T : struct
        {
            if (input != null)
            {
                T enumValue;
                if (Enum.TryParse(input.ToString(), out enumValue))
                    return enumValue;
            }

            return defaultValue;
        }

        //number of bits used to store each pixel
        private static int GetBpp(KnownTextureFormat format)
        {
            switch (format)
            {
                case KnownTextureFormat.A8R8G8B8:
                case KnownTextureFormat.X8R8G8B8:
                case KnownTextureFormat.ARGBFP32:
                case KnownTextureFormat.RGBFP32:
                    return 32;

                case KnownTextureFormat.A8:
                case KnownTextureFormat.Y8:
                case KnownTextureFormat.AY8:
                case KnownTextureFormat.P8_bump:
                    return 8;

                case KnownTextureFormat.CTX1:
                case KnownTextureFormat.DXT1:
                case KnownTextureFormat.DXT3a_alpha:
                case KnownTextureFormat.DXT3a_mono:
                case KnownTextureFormat.DXT5a:
                case KnownTextureFormat.DXT5a_alpha:
                case KnownTextureFormat.DXT5a_mono:
                case KnownTextureFormat.BC4_unorm:
                    return 4;

                case KnownTextureFormat.DXT3:
                case KnownTextureFormat.DXT5:
                case KnownTextureFormat.DXN:
                case KnownTextureFormat.DXN_mono_alpha:
                case KnownTextureFormat.BC7_unorm:
                    return 8;

                default: return 16;
            }
        }

        //the size in bytes of each read/write unit
        //ie 32bit uses ints, DXT uses shorts etc. Used for endian swaps.
        private static int GetLinearUnitSize(KnownTextureFormat format)
        {
            switch (format)
            {
                case KnownTextureFormat.A8R8G8B8:
                case KnownTextureFormat.X8R8G8B8:
                    return 4;

                case KnownTextureFormat.A8:
                case KnownTextureFormat.Y8:
                case KnownTextureFormat.AY8:
                case KnownTextureFormat.P8_bump:
                    return 1;

                default: return 2;
            }
        }

        //the width and height in pixels of each compressed block
        private static int GetLinearBlockSize(KnownTextureFormat format)
        {
            switch (format)
            {
                case KnownTextureFormat.DXT5a_mono:
                case KnownTextureFormat.DXT5a_alpha:
                case KnownTextureFormat.DXT1:
                case KnownTextureFormat.CTX1:
                case KnownTextureFormat.DXT5a:
                case KnownTextureFormat.DXT3a_alpha:
                case KnownTextureFormat.DXT3a_mono:
                case KnownTextureFormat.DXT3:
                case KnownTextureFormat.DXT5:
                case KnownTextureFormat.DXN:
                case KnownTextureFormat.DXN_mono_alpha:
                    return 4;

                default: return 1;
            }
        }

        //the size in bytes of each compressed block
        private static int GetLinearTexelPitch(KnownTextureFormat format)
        {
            switch (format)
            {
                case KnownTextureFormat.DXT5a_mono:
                case KnownTextureFormat.DXT5a_alpha:
                case KnownTextureFormat.DXT1:
                case KnownTextureFormat.CTX1:
                case KnownTextureFormat.DXT5a:
                case KnownTextureFormat.DXT3a_alpha:
                case KnownTextureFormat.DXT3a_mono:
                    return 8;

                case KnownTextureFormat.DXT3:
                case KnownTextureFormat.DXT5:
                case KnownTextureFormat.DXN:
                case KnownTextureFormat.DXN_mono_alpha:
                    return 16;

                case KnownTextureFormat.A8:
                case KnownTextureFormat.AY8:
                case KnownTextureFormat.P8:
                case KnownTextureFormat.P8_bump:
                case KnownTextureFormat.Y8:
                    return 1;

                case KnownTextureFormat.A8R8G8B8:
                case KnownTextureFormat.X8R8G8B8:
                    return 4;

                default: return 2;
            }
        }

        //on xbox 360 these texture formats must have dimensions that are multiples of these values.
        //if the bitmap dimensions are not multiples they are rounded up and cropped when displayed.
        private static int GetTileSize(KnownTextureFormat format)
        {
            switch (format)
            {
                case KnownTextureFormat.A8:
                case KnownTextureFormat.AY8:
                case KnownTextureFormat.A8R8G8B8:
                case KnownTextureFormat.X8R8G8B8:
                case KnownTextureFormat.A4R4G4B4:
                case KnownTextureFormat.R5G6B5:
                case KnownTextureFormat.U8V8:
                    return 32;

                case KnownTextureFormat.A8Y8:
                case KnownTextureFormat.Y8:
                case KnownTextureFormat.DXT5a_mono:
                case KnownTextureFormat.DXT5a_alpha:
                case KnownTextureFormat.DXT1:
                case KnownTextureFormat.CTX1:
                case KnownTextureFormat.DXT5a:
                case KnownTextureFormat.DXT3a_alpha:
                case KnownTextureFormat.DXT3a_mono:
                case KnownTextureFormat.DXT3:
                case KnownTextureFormat.DXT5:
                case KnownTextureFormat.DXN:
                case KnownTextureFormat.DXN_mono_alpha:
                    return 128;

                default: return 1;
            }
        }

        public static int Bpp(this Blam.Halo2.TextureFormat format) => GetBpp(format.ParseToEnum<KnownTextureFormat>());
        public static int LinearUnitSize(this Blam.Halo2.TextureFormat format) => GetLinearUnitSize(format.ParseToEnum<KnownTextureFormat>());

        //round up to the nearst valid size, accounting for block sizes and tile sizes
        public static void GetVirtualSize(object format, int width, int height, out int virtualWidth, out int virtualHeight)
        {
            var knownFormat = format.ParseToEnum<KnownTextureFormat>();
            if (knownFormat == KnownTextureFormat.Unknown)
                throw new ArgumentException("Could not translate to a known texture format.", nameof(format));

            double blockSize = GetLinearBlockSize(knownFormat);
            double tileSize = GetTileSize(knownFormat);

            virtualWidth = (int)(Math.Ceiling(width / tileSize) * tileSize);
            virtualHeight = (int)(Math.Ceiling(height / tileSize) * tileSize);

            virtualWidth = (int)(Math.Ceiling(virtualWidth / blockSize) * blockSize);
            virtualHeight = (int)(Math.Ceiling(virtualHeight / blockSize) * blockSize);
        }

        public static byte[] ApplyCrop(byte[] data, object format, int faces, int inWidth, int inHeight, int outWidth, int outHeight)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            if (outWidth >= inWidth && outHeight >= inHeight)
                return data;

            var knownFormat = format.ParseToEnum<KnownTextureFormat>();
            if (knownFormat == KnownTextureFormat.Unknown)
                throw new ArgumentException("Could not translate to a known texture format.", nameof(format));

            double blockLen = GetLinearBlockSize(knownFormat);
            var blockSize = GetLinearTexelPitch(knownFormat);

            var inRows = (int)Math.Ceiling(inHeight / blockLen) / faces;
            var outRows = (int)Math.Ceiling(outHeight / blockLen) / faces;
            var inStride = (int)Math.Ceiling(inWidth / blockLen) * blockSize;
            var outStride = (int)Math.Ceiling(outWidth / blockLen) * blockSize;

            var output = new byte[outRows * outStride * faces];
            for (int f = 0; f < faces; f++)
            {
                var srcTileStart = inRows * inStride * f;
                var destTileStart = outRows * outStride * f;

                for (int s = 0; s < outRows; s++)
                    Array.Copy(data, srcTileStart + inStride * s, output, destTileStart + outStride * s, outStride);
            }

            return output;
        }

        public static object DXNSwap(object format, bool shouldSwap)
        {
            var bitmapFormat = format.ParseToEnum<KnownTextureFormat>();
            return shouldSwap && bitmapFormat == KnownTextureFormat.DXN
                ? KnownTextureFormat.DXN_SNorm
                : bitmapFormat;
        }

        public static DdsImage GetDds(int height, int width, object format, bool isCubemap, byte[] data, bool isPC = false)
        {
            var knownFormat = format.ParseToEnum<KnownTextureFormat>();
            if (knownFormat == KnownTextureFormat.Unknown)
                throw new ArgumentException("Could not translate to a known texture format.", nameof(format));

            DdsImage dds;
            if (isPC && knownFormat == KnownTextureFormat.DXN)
                dds = new DdsImage(height, width, XboxFormat.DXN_SNorm, data);
            else if (dxgiLookup.ContainsKey(knownFormat))
                dds = new DdsImage(height, width, dxgiLookup[knownFormat], data);
            else if (xboxLookup.ContainsKey(knownFormat))
                dds = new DdsImage(height, width, xboxLookup[knownFormat], data);
            else throw Exceptions.BitmapFormatNotSupported(format.ToString());

            if (isCubemap)
                dds.CubemapFlags = CubemapFlags.DdsCubemapAllFaces;

            return dds;
        }

        public static int GetBitmapDataLength(IBitmapData submap, bool includeMips)
        {
            if (submap.MipmapCount == 0)
                includeMips = false;

            int virtualWidth, virtualHeight;
            if (!submap.UsesPadding)
            {
                virtualWidth = submap.Width;
                virtualHeight = submap.Height;
            }
            else
                GetVirtualSize(submap.BitmapFormat, submap.Width, submap.Height, out virtualWidth, out virtualHeight);

            var bitmapFormat = submap.BitmapFormat.ParseToEnum<KnownTextureFormat>();
            var frameSize = virtualWidth * virtualHeight * GetBpp(bitmapFormat) / 8;

            if (includeMips)
            {
                var mipsSize = 0;
                var minUnit = (int)Math.Pow(GetLinearBlockSize(bitmapFormat), 2) * GetBpp(bitmapFormat) / 8;
                for (int i = 1; i <= submap.MipmapCount; i++)
                    mipsSize += Math.Max(minUnit, (int)(frameSize * Math.Pow(0.25, i)));
                frameSize += mipsSize;
            }

            return frameSize * Math.Max(1, submap.FrameCount);
        }

        public static DdsImage GetDds(IBitmapData submap, byte[] data, bool includeMips)
        {
            if (submap.MipmapCount == 0)
                includeMips = false;

            int virtualWidth, virtualHeight;
            if (!submap.UsesPadding)
            {
                virtualWidth = submap.Width;
                virtualHeight = submap.Height;
            }
            else
                GetVirtualSize(submap.BitmapFormat, submap.Width, submap.Height, out virtualWidth, out virtualHeight);

            var bitmapFormat = submap.BitmapFormat.ParseToEnum<KnownTextureFormat>();
            var textureType = submap.BitmapType.ParseToEnum<KnownTextureType>();

            if (submap.ByteOrder == ByteOrder.BigEndian)
            {
                var unitSize = GetLinearUnitSize(bitmapFormat);
                if (unitSize > 1)
                {
                    for (int i = 0; i < data.Length - 1; i += unitSize)
                        Array.Reverse(data, i, unitSize);
                }
            }

            var arrayHeight = virtualHeight * Math.Max(1, submap.FrameCount);

            if (includeMips)
            {
                var mipsHeight = 0d;
                for (int i = 1; i <= submap.MipmapCount; i++)
                    mipsHeight += arrayHeight * Math.Pow(0.25, i);

                var minUnit = GetLinearBlockSize(bitmapFormat);
                mipsHeight += (minUnit - (mipsHeight % minUnit)) % minUnit;

                arrayHeight += (int)mipsHeight * Math.Max(1, submap.FrameCount);
            }

            if (submap.Swizzled)
                data = XTextureScramble(data, virtualWidth, arrayHeight, submap.BitmapFormat, false);

            if (virtualWidth > submap.Width || virtualHeight > submap.Height)
                data = ApplyCrop(data, submap.BitmapFormat, submap.FrameCount, virtualWidth, virtualHeight, submap.Width, submap.Height);

            DdsImage dds;
            if (dxgiLookup.ContainsKey(bitmapFormat))
                dds = new DdsImage(submap.Height, submap.Width, dxgiLookup[bitmapFormat], data);
            else if (xboxLookup.ContainsKey(bitmapFormat))
                dds = new DdsImage(submap.Height, submap.Width, xboxLookup[bitmapFormat], data);
            else throw Exceptions.BitmapFormatNotSupported(bitmapFormat.ToString());

            if (textureType == KnownTextureType.CubeMap)
                dds.CubemapFlags = CubemapFlags.DdsCubemapAllFaces;
            if (textureType == KnownTextureType.Array)
                dds.ArraySize = submap.FrameCount;
            if (includeMips)
                dds.MipmapCount = submap.MipmapCount + 1;

            return dds;
        }

        #endregion

        #region Original Xbox

        /* http://www.h2maps.net/Tools/Xbox/Mutation/Mutation/DDS/Swizzle.cs */

        private class MaskSet
        {
            public readonly int x;
            public readonly int y;
            public readonly int z;

            public MaskSet(int w, int h, int d)
            {
                int bit = 1;
                int index = 1;

                while (bit < w || bit < h || bit < d)
                {
                    if (bit < w)
                    {
                        x |= index;
                        index <<= 1;
                    }

                    if (bit < h)
                    {
                        y |= index;
                        index <<= 1;
                    }

                    if (bit < d)
                    {
                        z |= index;
                        index <<= 1;
                    }

                    bit <<= 1;
                }
            }
        }

        public static byte[] Swizzle(byte[] data, int width, int height, int depth, int bpp)
        {
            return Swizzle(data, width, height, depth, bpp, true);
        }

        public static byte[] Swizzle(byte[] data, int width, int height, int depth, int bpp, bool deswizzle)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            int a = 0, b = 0;
            var output = new byte[data.Length];

            var masks = new MaskSet(width, height, depth);
            for (int y = 0; y < height * depth; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (deswizzle)
                    {
                        a = ((y * width) + x) * bpp;
                        b = (Swizzle(x, y, depth, masks)) * bpp;
                    }
                    else
                    {
                        b = ((y * width) + x) * bpp;
                        a = (Swizzle(x, y, depth, masks)) * bpp;
                    }

                    if (a < output.Length && b < data.Length)
                    {
                        for (int i = 0; i < bpp; i++)
                            output[a + i] = data[b + i];
                    }
                    else return null;
                }
            }

            return output;
        }

        private static int Swizzle(int x, int y, int z, MaskSet masks)
        {
            return SwizzleAxis(x, masks.x) | SwizzleAxis(y, masks.y) | (z == -1 ? 0 : SwizzleAxis(z, masks.z));
        }

        private static int SwizzleAxis(int val, int mask)
        {
            int bit = 1;
            int result = 0;

            while (bit <= mask)
            {
                int tmp = mask & bit;

                if (tmp != 0) result |= (val & bit);
                else val <<= 1;

                bit <<= 1;
            }

            return result;
        }

        #endregion

        #region Xbox 360

        /* https://github.com/gdkchan/MESTool/blob/master/MESTool/Program.cs */
        public static byte[] XTextureScramble(byte[] data, int width, int height, object format)
        {
            return XTextureScramble(data, width, height, format, false);
        }

        public static byte[] XTextureScramble(byte[] data, int width, int height, object format, bool toLinear)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            var knownFormat = format.ParseToEnum<KnownTextureFormat>();
            if (knownFormat == KnownTextureFormat.Unknown)
                throw new ArgumentException("Could not translate to a known texture format.", nameof(format));

            var blockSize = GetLinearBlockSize(knownFormat);
            var texelPitch = GetLinearTexelPitch(knownFormat);
            var bpp = GetBpp(knownFormat);
            var tileSize = GetTileSize(knownFormat);

            width = (int)Math.Ceiling((float)width / tileSize) * tileSize;
            height = (int)Math.Ceiling((float)height / tileSize) * tileSize;

            var expectedSize = width * height * bpp / 8;
            if (expectedSize > data.Length)
                Array.Resize(ref data, expectedSize);

            var output = new byte[data.Length];

            int xBlocks = width / blockSize;
            int yBlocks = height / blockSize;

            for (int i = 0; i < yBlocks; i++)
            {
                for (int j = 0; j < xBlocks; j++)
                {
                    int blockOffset = i * xBlocks + j;

                    int x = XGAddress2DTiledX(blockOffset, xBlocks, texelPitch);
                    int y = XGAddress2DTiledY(blockOffset, xBlocks, texelPitch);

                    int sourceIndex = i * xBlocks * texelPitch + j * texelPitch;
                    int destIndex = y * xBlocks * texelPitch + x * texelPitch;

                    if (toLinear)
                        Array.Copy(data, destIndex, output, sourceIndex, texelPitch);
                    else
                        Array.Copy(data, sourceIndex, output, destIndex, texelPitch);
                }
            }

            return output;
        }

        private static int XGAddress2DTiledX(int offset, int width, int texelPitch)
        {
            int alignedWidth = (width + 31) & ~31;

            int logBPP = (texelPitch >> 2) + ((texelPitch >> 1) >> (texelPitch >> 2));
            int offsetB = offset << logBPP;
            int offsetT = ((offsetB & ~4095) >> 3) + ((offsetB & 1792) >> 2) + (offsetB & 63);
            int offsetM = offsetT >> (7 + logBPP);

            int macroX = (offsetM % (alignedWidth >> 5)) << 2;
            int tile = (((offsetT >> (5 + logBPP)) & 2) + (offsetB >> 6)) & 3;
            int macro = (macroX + tile) << 3;
            int micro = ((((offsetT >> 1) & ~15) + (offsetT & 15)) & ((texelPitch << 3) - 1)) >> logBPP;

            return macro + micro;
        }

        private static int XGAddress2DTiledY(int offset, int width, int texelPitch)
        {
            int alignedWidth = (width + 31) & ~31;

            int logBPP = (texelPitch >> 2) + ((texelPitch >> 1) >> (texelPitch >> 2));
            int offsetB = offset << logBPP;
            int offsetT = ((offsetB & ~4095) >> 3) + ((offsetB & 1792) >> 2) + (offsetB & 63);
            int offsetM = offsetT >> (7 + logBPP);

            int macroY = (offsetM / (alignedWidth >> 5)) << 2;
            int tile = ((offsetT >> (6 + logBPP)) & 1) + ((offsetB & 2048) >> 10);
            int macro = (macroY + tile) << 3;
            int micro = (((offsetT & (((texelPitch << 6) - 1) & ~31)) + ((offsetT & 15) << 1)) >> (3 + logBPP)) & ~1;

            return macro + micro + ((offsetT & 16) >> 4);
        }

        #endregion
    }
}
