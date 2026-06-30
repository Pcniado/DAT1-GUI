using System;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace ModdingTool.Windows
{
    public static class InsomniacTextureDecoder
    {
        // true for all block-compressed dxgi formats
        private static bool IsBlockCompressed(uint fmt)
        {
            switch (fmt)
            {
                case 70: case 71: case 72: // BC1
                case 73: case 74: case 75: // BC2
                case 76: case 77: case 78: // BC3
                case 79: case 80: case 81: // BC4
                case 82: case 83: case 84: // BC5
                case 94: case 95: case 96: // BC6H
                case 97: case 98: case 99: // BC7
                    return true;
                default:
                    return false;
            }
        }

        // bytes per block for BC, bytes per texel for uncompressed
        private static uint GetElementBytes(uint fmt)
        {
            switch (fmt)
            {
                case 73: case 74: case 75: // BC2
                case 76: case 77: case 78: // BC3
                case 82: case 83: case 84: // BC5
                case 94: case 95: case 96: // BC6H
                case 97: case 98: case 99: // BC7
                    return 16;
                case 70: case 71: case 72: // BC1
                case 79: case 80: case 81: // BC4
                    return 8;
                case 1: case 2: case 3: case 4: // R32G32B32A32
                    return 16;
                case 9: case 10: case 11: case 12: case 13: case 14: // R16G16B16A16
                case 15: case 16: case 17: case 18: // R32G32
                    return 8;
                case 23: case 24: case 25: case 26: // R10G10B10A2/R11G11B10
                case 27: case 28: case 29: case 30: case 31: case 32: // R8G8B8A8
                case 33: case 34: case 35: case 36: case 37: case 38: // R16G16
                case 39: case 40: case 41: case 42: case 43: // R32
                case 67: // R9G9B9E5
                case 87: case 88: case 89: case 90: case 91: case 92: case 93: // B8G8R8A8/X8
                    return 4;
                case 48: case 49: case 50: case 51: case 52: // R8G8
                case 53: case 54: case 55: case 56: case 57: case 58: case 59: // R16
                case 85: case 86: // B5G6R5/B5G5R5A1
                case 115: // B4G4R4A4
                    return 2;
                case 60: case 61: case 62: case 63: case 64: // R8
                case 65: // A8
                    return 1;
                default:
                    return 4;
            }
        }

        private static uint GetWidthElements(uint fmt, uint w) => IsBlockCompressed(fmt) ? ((w + 3) / 4) : w;
        private static uint GetRowPitch(uint fmt, uint w) => GetWidthElements(fmt, w) * GetElementBytes(fmt);

        // builds a dx10 dds from already-decoded header values + raw mip bytes
        public static byte[] BuildDDS(byte[] mip, uint w, uint h, uint fmt)
        {
            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms);
            bw.Write(Encoding.ASCII.GetBytes("DDS "));
            bw.Write((uint)0x7c);
            bw.Write((uint)(0x1 | 0x2 | 0x4 | 0x1000 | 0x80000 | 0x20000));
            bw.Write(h);
            bw.Write(w);
            bw.Write(GetRowPitch(fmt, w));
            bw.Write((uint)0);
            bw.Write((uint)1);
            bw.Write(new byte[44]);     // reserved
            bw.Write((uint)32);         // pfSize
            bw.Write((uint)4);          // pfFlags FourCC
            bw.Write(Encoding.ASCII.GetBytes("DX10"));
            bw.Write(new byte[20]);     // pfRest
            bw.Write((uint)(0x1000 | 0x8)); // caps
            bw.Write(new byte[16]);     // caps2-4 + reserved
            bw.Write(fmt);
            bw.Write((uint)3);          // resourceDimension 2D
            bw.Write((uint)0);
            bw.Write((uint)1);          // arraySize
            bw.Write((uint)0);
            bw.Write(mip);
            return ms.ToArray();
        }

        // returns true if pfim can decode this dxgi format natively
        // list taken directly from Pfim's DdsHeaderDxt10.cs switch statement
        public static bool IsPfimSupported(uint fmt)
        {
            switch (fmt)
            {
                case 70: case 71: case 72: // BC1
                case 73: case 74: case 75: // BC2
                case 76: case 77: case 78: // BC3
                case 79: case 80:          // BC4
                case 81:                   // BC4_SNORM
                case 82: case 83:          // BC5
                case 84:                   // BC5_SNORM
                case 94: case 95: case 96: // BC6H
                case 97: case 98: case 99: // BC7
                case 27: case 28: case 29: case 30: case 31: case 32: // R8G8B8A8
                case 87: case 88: case 90: case 91: case 93:          // B8G8R8A8/X8
                case 86:                   // B5G5R5A1
                case 54:                   // R16_FLOAT
                case 41:                   // R32_FLOAT
                    return true;
                default:
                    return false;
            }
        }

        // converts pfim-unsupported raw pixel data to Bgra32 for display
        // width/height needed for correct stride math
        // returns null if format is block-compressed (those must go through pfim)
        public static byte[] ConvertToBgra32(byte[] src, uint fmt, uint w, uint h)
        {
            if (IsBlockCompressed(fmt)) return null;
            int pixels = (int)(w * h);
            byte[] dst = new byte[pixels * 4];

            switch (fmt)
            {
                // R16G16_UNORM, R16G16_UINT, R16G16_TYPELESS — show R in red, G in green
                case 33: case 35: case 36:
                {
                    for (int i = 0; i < pixels; i++)
                    {
                        ushort r = BitConverter.ToUInt16(src, i * 4);
                        ushort g = BitConverter.ToUInt16(src, i * 4 + 2);
                        byte r8 = (byte)(r >> 8);
                        byte g8 = (byte)(g >> 8);
                        dst[i * 4 + 0] = 0;
                        dst[i * 4 + 1] = g8;
                        dst[i * 4 + 2] = r8;
                        dst[i * 4 + 3] = 255;
                    }
                    return dst;
                }
                // R16G16_FLOAT — decode half floats, tone-map to 0-255
                case 34:
                {
                    for (int i = 0; i < pixels; i++)
                    {
                        float r = HalfToFloat(BitConverter.ToUInt16(src, i * 4));
                        float g = HalfToFloat(BitConverter.ToUInt16(src, i * 4 + 2));
                        dst[i * 4 + 0] = 0;
                        dst[i * 4 + 1] = FloatToByte(g);
                        dst[i * 4 + 2] = FloatToByte(r);
                        dst[i * 4 + 3] = 255;
                    }
                    return dst;
                }
                // R16G16_SNORM — signed, remap -1..1 to 0..255
                case 37: case 38:
                {
                    for (int i = 0; i < pixels; i++)
                    {
                        short r = BitConverter.ToInt16(src, i * 4);
                        short g = BitConverter.ToInt16(src, i * 4 + 2);
                        dst[i * 4 + 0] = 0;
                        dst[i * 4 + 1] = SNormToByte(g);
                        dst[i * 4 + 2] = SNormToByte(r);
                        dst[i * 4 + 3] = 255;
                    }
                    return dst;
                }
                // R32G32_FLOAT
                case 15: case 16: case 17: case 18:
                {
                    for (int i = 0; i < pixels; i++)
                    {
                        float r = BitConverter.ToSingle(src, i * 8);
                        float g = BitConverter.ToSingle(src, i * 8 + 4);
                        dst[i * 4 + 0] = 0;
                        dst[i * 4 + 1] = FloatToByte(g);
                        dst[i * 4 + 2] = FloatToByte(r);
                        dst[i * 4 + 3] = 255;
                    }
                    return dst;
                }
                // R32G32B32A32 float/uint/sint/typeless
                case 1: case 2: case 3: case 4:
                {
                    for (int i = 0; i < pixels; i++)
                    {
                        float r = BitConverter.ToSingle(src, i * 16);
                        float g = BitConverter.ToSingle(src, i * 16 + 4);
                        float b = BitConverter.ToSingle(src, i * 16 + 8);
                        float a = BitConverter.ToSingle(src, i * 16 + 12);
                        dst[i * 4 + 0] = FloatToByte(b);
                        dst[i * 4 + 1] = FloatToByte(g);
                        dst[i * 4 + 2] = FloatToByte(r);
                        dst[i * 4 + 3] = FloatToByte(a);
                    }
                    return dst;
                }
                // R16G16B16A16 all variants
                case 9: case 11: case 12: case 13: case 14:
                {
                    for (int i = 0; i < pixels; i++)
                    {
                        dst[i * 4 + 0] = (byte)(BitConverter.ToUInt16(src, i * 8 + 4) >> 8); // B
                        dst[i * 4 + 1] = (byte)(BitConverter.ToUInt16(src, i * 8 + 2) >> 8); // G
                        dst[i * 4 + 2] = (byte)(BitConverter.ToUInt16(src, i * 8 + 0) >> 8); // R
                        dst[i * 4 + 3] = (byte)(BitConverter.ToUInt16(src, i * 8 + 6) >> 8); // A
                    }
                    return dst;
                }
                // R16G16B16A16_FLOAT
                case 10:
                {
                    for (int i = 0; i < pixels; i++)
                    {
                        dst[i * 4 + 0] = FloatToByte(HalfToFloat(BitConverter.ToUInt16(src, i * 8 + 4)));
                        dst[i * 4 + 1] = FloatToByte(HalfToFloat(BitConverter.ToUInt16(src, i * 8 + 2)));
                        dst[i * 4 + 2] = FloatToByte(HalfToFloat(BitConverter.ToUInt16(src, i * 8 + 0)));
                        dst[i * 4 + 3] = FloatToByte(HalfToFloat(BitConverter.ToUInt16(src, i * 8 + 6)));
                    }
                    return dst;
                }
                // R10G10B10A2_UNORM and variants
                case 23: case 24: case 25:
                {
                    for (int i = 0; i < pixels; i++)
                    {
                        uint packed = BitConverter.ToUInt32(src, i * 4);
                        uint r10 = packed & 0x3FF;
                        uint g10 = (packed >> 10) & 0x3FF;
                        uint b10 = (packed >> 20) & 0x3FF;
                        uint a2  = (packed >> 30) & 0x3;
                        dst[i * 4 + 0] = (byte)(b10 >> 2);
                        dst[i * 4 + 1] = (byte)(g10 >> 2);
                        dst[i * 4 + 2] = (byte)(r10 >> 2);
                        dst[i * 4 + 3] = (byte)(a2 * 85);
                    }
                    return dst;
                }
                // R11G11B10_FLOAT — show as Bgra32 from float channels
                case 26:
                {
                    for (int i = 0; i < pixels; i++)
                    {
                        uint packed = BitConverter.ToUInt32(src, i * 4);
                        float r = R11FloatToFloat((packed) & 0x7FF);
                        float g = R11FloatToFloat((packed >> 11) & 0x7FF);
                        float b = R10FloatToFloat((packed >> 22) & 0x3FF);
                        dst[i * 4 + 0] = FloatToByte(b);
                        dst[i * 4 + 1] = FloatToByte(g);
                        dst[i * 4 + 2] = FloatToByte(r);
                        dst[i * 4 + 3] = 255;
                    }
                    return dst;
                }
                // R32_UINT/SINT/TYPELESS — show as greyscale
                case 39: case 40: case 42: case 43:
                {
                    for (int i = 0; i < pixels; i++)
                    {
                        byte v = src[i * 4 + 3]; // take high byte as approximation
                        dst[i * 4 + 0] = dst[i * 4 + 1] = dst[i * 4 + 2] = v;
                        dst[i * 4 + 3] = 255;
                    }
                    return dst;
                }
                // R8G8_UNORM and variants — show R red, G green
                case 48: case 49: case 50: case 51: case 52:
                {
                    for (int i = 0; i < pixels; i++)
                    {
                        dst[i * 4 + 0] = 0;
                        dst[i * 4 + 1] = src[i * 2 + 1]; // G
                        dst[i * 4 + 2] = src[i * 2 + 0]; // R
                        dst[i * 4 + 3] = 255;
                    }
                    return dst;
                }
                // R16_UNORM, R16_UINT, R16_TYPELESS, D16
                case 53: case 55: case 56: case 57:
                {
                    for (int i = 0; i < pixels; i++)
                    {
                        byte v = src[i * 2 + 1]; // high byte
                        dst[i * 4 + 0] = dst[i * 4 + 1] = dst[i * 4 + 2] = v;
                        dst[i * 4 + 3] = 255;
                    }
                    return dst;
                }
                // R16_SNORM, R16_SINT
                case 58: case 59:
                {
                    for (int i = 0; i < pixels; i++)
                    {
                        byte v = SNormToByte(BitConverter.ToInt16(src, i * 2));
                        dst[i * 4 + 0] = dst[i * 4 + 1] = dst[i * 4 + 2] = v;
                        dst[i * 4 + 3] = 255;
                    }
                    return dst;
                }
                // R8_* all variants — greyscale
                case 60: case 61: case 62: case 63: case 64:
                {
                    for (int i = 0; i < pixels; i++)
                    {
                        dst[i * 4 + 0] = dst[i * 4 + 1] = dst[i * 4 + 2] = src[i];
                        dst[i * 4 + 3] = 255;
                    }
                    return dst;
                }
                // A8_UNORM — alpha in all channels
                case 65:
                {
                    for (int i = 0; i < pixels; i++)
                    {
                        dst[i * 4 + 0] = dst[i * 4 + 1] = dst[i * 4 + 2] = src[i];
                        dst[i * 4 + 3] = src[i];
                    }
                    return dst;
                }
                // R9G9B9E5_SHAREDEXP
                case 67:
                {
                    for (int i = 0; i < pixels; i++)
                    {
                        uint packed = BitConverter.ToUInt32(src, i * 4);
                        int exp   = (int)(packed >> 27) - 24;
                        float scale = (float)Math.Pow(2, exp) / 511f;
                        float r = ((packed) & 0x1FF) * scale;
                        float g = ((packed >> 9) & 0x1FF) * scale;
                        float b = ((packed >> 18) & 0x1FF) * scale;
                        dst[i * 4 + 0] = FloatToByte(b);
                        dst[i * 4 + 1] = FloatToByte(g);
                        dst[i * 4 + 2] = FloatToByte(r);
                        dst[i * 4 + 3] = 255;
                    }
                    return dst;
                }
                // B8G8R8X8_UNORM — just zero the alpha
                case 88: case 92: case 93:
                {
                    for (int i = 0; i < pixels; i++)
                    {
                        dst[i * 4 + 0] = src[i * 4 + 0];
                        dst[i * 4 + 1] = src[i * 4 + 1];
                        dst[i * 4 + 2] = src[i * 4 + 2];
                        dst[i * 4 + 3] = 255;
                    }
                    return dst;
                }
                default:
                    return null; // can't handle this one
            }
        }

        // float helpers

        private static byte FloatToByte(float v) => (byte)(Math.Max(0, Math.Min(1, v)) * 255f + 0.5f);

        private static byte SNormToByte(short v) => (byte)((v / 32767f * 0.5f + 0.5f) * 255f);

        private static float HalfToFloat(ushort h)
        {
            int exp  = (h >> 10) & 0x1F;
            int mant = h & 0x3FF;
            int sign = (h >> 15) & 1;
            float f;
            if (exp == 0)       f = mant / 16383f;
            else if (exp == 31) f = mant == 0 ? float.PositiveInfinity : float.NaN;
            else                f = (1 + mant / 1024f) * (float)Math.Pow(2, exp - 15);
            return sign == 0 ? f : -f;
        }

        private static float R11FloatToFloat(uint bits)
        {
            int exp  = (int)((bits >> 6) & 0x1F);
            int mant = (int)(bits & 0x3F);
            if (exp == 0) return mant / 64f * (float)Math.Pow(2, -14);
            return (1 + mant / 64f) * (float)Math.Pow(2, exp - 15);
        }

        private static float R10FloatToFloat(uint bits)
        {
            int exp  = (int)((bits >> 5) & 0x1F);
            int mant = (int)(bits & 0x1F);
            if (exp == 0) return mant / 32f * (float)Math.Pow(2, -14);
            return (1 + mant / 32f) * (float)Math.Pow(2, exp - 15);
        }

        // decodes a texel data block from msm2-layout .texture and returns a dds byte array
        public static byte[] DecodeToDDS(byte[] sdData, byte[] hdData = null, int? overrideFormat = null)
        {
            try
            {
                byte[] combined = hdData != null && hdData.Length > 0
                    ? Concat(sdData, hdData)
                    : sdData;
                using var ms = new MemoryStream(combined);
                using var br = new BinaryReader(ms);
                uint size    = br.ReadUInt32();
                uint hdSize  = br.ReadUInt32();
                ushort width = br.ReadUInt16();
                ushort height= br.ReadUInt16();
                br.ReadUInt16(); br.ReadUInt16(); // sdWidth/sdHeight
                br.ReadUInt16(); // images
                br.ReadByte();   // channels
                byte fmtByte = br.ReadByte();
                uint fmt = overrideFormat.HasValue ? (uint)overrideFormat.Value : fmtByte;
                br.BaseStream.Seek(7, SeekOrigin.Current);
                byte[] mip = br.ReadBytes((int)size);
                return BuildDDS(mip, width, height, fmt);
            }
            catch { return null; }
        }

        // overload for callers that already parsed width/height/format themselves
        public static byte[] DecodeToDDS(byte[] rawData, int w, int h, int fmt)
            => BuildDDS(rawData, (uint)w, (uint)h, (uint)fmt);

        public static byte[] DecodeToDDS(byte[] rawData, ushort w, ushort h, ushort fmt)
            => BuildDDS(rawData, w, h, fmt);

        // saves a dds to disk for external tools when pfim can't display it
        public static void SaveDDSForExternalTool(byte[] ddsData, string suggestedPath = null)
        {
            try
            {
                string path = suggestedPath;
                if (string.IsNullOrEmpty(path))
                {
                    using var sfd = new SaveFileDialog();
                    sfd.Filter = "DDS files (*.dds)|*.dds|All files (*.*)|*.*";
                    sfd.FileName = "exported_texture.dds";
                    if (sfd.ShowDialog() != DialogResult.OK) return;
                    path = sfd.FileName;
                }
                File.WriteAllBytes(path, ddsData);
            }
            catch { }
        }

        private static byte[] Concat(byte[] a, byte[] b)
        {
            byte[] r = new byte[a.Length + b.Length];
            Buffer.BlockCopy(a, 0, r, 0, a.Length);
            Buffer.BlockCopy(b, 0, r, a.Length, b.Length);
            return r;
        }
    }
}
