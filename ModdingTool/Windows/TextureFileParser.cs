using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using ModdingTool.Windows;

namespace ModdingTool.Windows
{
    public static class TextureFileParser
    {
        public class TexelDataBlock
        {
            public int SpanIndex; // 0 = SD, 1 = HD
            public byte[] Data;
        }

        public class TextureInfo
        {
            public uint Size;
            public uint HDSize;
            public ushort Width;
            public ushort Height;
            public ushort SDWidth;
            public ushort SDHeight;
            public ushort Images;
            public byte Channels;
            public int Format;
            public int Mipmaps;
            public int HDMipmaps;
            public string SourceFile;
            public bool HasHD;
        }

        // extracts all texel data blocks (sd/hd) from a .texture file
        public static List<TexelDataBlock> ExtractTexelDataBlocks(string filePath)
        {
            var blocks = new List<TexelDataBlock>();
            using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            using (var br = new BinaryReader(fs))
            {
                fs.Seek(0, SeekOrigin.Begin);
                while (fs.Position < fs.Length)
                {
                    long blockStart = fs.Position;
                    if (fs.Length - fs.Position < 8) break;
                    int nameLen = br.ReadInt32();
                    if (nameLen < 0 || nameLen > 64) break;
                    string name = new string(br.ReadChars(nameLen));
                    int blockSize = br.ReadInt32();
                    if (blockSize < 0 || blockSize > fs.Length - fs.Position) break;
                    if (name == "Texel Data")
                    {
                        int spanIndex = br.ReadInt32();
                        byte[] data = br.ReadBytes(blockSize - 4);
                        blocks.Add(new TexelDataBlock { SpanIndex = spanIndex, Data = data });
                    }
                    else
                    {
                        fs.Seek(blockSize, SeekOrigin.Current);
                    }
                }
            }
            return blocks;
        }

        // returns the best dds (hd preferred, fallback to sd) as a byte array, or null
        public static byte[] GetBestDDS(string filePath, out int spanIndex)
        {
            spanIndex = -1;
            if (!File.Exists(filePath)) return null;
            try
            {
                string hdPath = Path.ChangeExtension(filePath, ".hd.texture");
                byte[] fileBytes = File.ReadAllBytes(filePath);
                var asset = new AssetManager(fileBytes);
                int offset = asset.GetAssetSectionOffset(Section.Texture.Content);
                int sectionSize = asset.GetAssetSectionSize(Section.Texture.Content);
                if (offset < 0 || sectionSize <= 0) return null;
                bool isLegacy = asset._assetGame != AssetManager.Game.MSM2;

                using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                using (var br = new BinaryReader(fs))
                {
                    fs.Seek(offset, SeekOrigin.Begin);

                    if (isLegacy)
                    {
                        // layout confirmed against silktexture's Source.cs legacy branch
                        uint sdLen      = br.ReadUInt32();
                        uint hdLen      = br.ReadUInt32();
                        ushort hdWidth  = br.ReadUInt16();
                        ushort hdHeight = br.ReadUInt16();
                        ushort sdWidth  = br.ReadUInt16();
                        ushort sdHeight = br.ReadUInt16();
                        ushort arrSize  = br.ReadUInt16();
                        br.ReadByte();                // channels (skip)
                        br.ReadByte();                // skip
                        ushort fmt      = br.ReadUInt16();
                        br.ReadBytes(8);              // skip
                        br.ReadByte();                // sd mipmaps
                        br.ReadByte();                // skip
                        br.ReadByte();                // hd mipmaps
                        br.ReadBytes(11);             // skip
                        // mip data follows sequentially in the section
                        bool hasHd = hdLen > 0 && File.Exists(hdPath);
                        if (hasHd)
                        {
                            byte[] hdData = File.ReadAllBytes(hdPath);
                            var dds = InsomniacTextureDecoder.DecodeToDDS(hdData, hdWidth, hdHeight, fmt);
                            if (dds != null) { spanIndex = 1; return dds; }
                        }
                        byte[] sdData = br.ReadBytes((int)(sdLen / Math.Max(1, (int)arrSize)));
                        var sdDds = InsomniacTextureDecoder.DecodeToDDS(sdData, sdWidth, sdHeight, fmt);
                        if (sdDds != null) { spanIndex = 0; return sdDds; }
                        return null;
                    }
                    else
                    {
                        // msm2 layout
                        uint imgSize    = br.ReadUInt32();
                        uint hdSize     = br.ReadUInt32();
                        ushort width    = br.ReadUInt16();
                        ushort height   = br.ReadUInt16();
                        ushort sdWidth  = br.ReadUInt16();
                        ushort sdHeight = br.ReadUInt16();
                        ushort images   = br.ReadUInt16();
                        br.ReadByte();               // channels
                        br.ReadBytes(5);
                        byte format     = br.ReadByte();
                        br.ReadByte();
                        br.ReadByte();
                        br.ReadByte();
                        br.ReadBytes(4);

                        bool hasHd = hdSize > 0 && File.Exists(hdPath);
                        if (hasHd)
                        {
                            byte[] hdData = File.ReadAllBytes(hdPath);
                            var dds = InsomniacTextureDecoder.DecodeToDDS(hdData, width, height, format);
                            if (dds != null) { spanIndex = 1; return dds; }
                        }
                        byte[] sdData = br.ReadBytes((int)(imgSize / Math.Max(1, (int)images)));
                        var sdDds = InsomniacTextureDecoder.DecodeToDDS(sdData, sdWidth, sdHeight, format);
                        if (sdDds != null) { spanIndex = 0; return sdDds; }
                        return null;
                    }
                }
            }
            catch { return null; }
        }

        // extracts header info from a .texture file
        public static TextureInfo ExtractTextureInfo(string filePath)
        {
            if (!File.Exists(filePath)) return null;
            try
            {
                var asset = new AssetManager(File.ReadAllBytes(filePath));
                int offset = asset.GetAssetSectionOffset(Section.Texture.Content);
                int size   = asset.GetAssetSectionSize(Section.Texture.Content);
                if (offset < 0 || size <= 0) return null;

                using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                using (var br = new BinaryReader(fs))
                {
                    fs.Seek(offset, SeekOrigin.Begin);
                    var info    = new TextureInfo();
                    info.Size   = br.ReadUInt32();
                    info.HDSize = br.ReadUInt32();
                    info.Width  = br.ReadUInt16();
                    info.Height = br.ReadUInt16();
                    info.SDWidth  = br.ReadUInt16();
                    info.SDHeight = br.ReadUInt16();
                    info.Images   = br.ReadUInt16();
                    info.Channels = br.ReadByte();

                    if (asset._assetGame == AssetManager.Game.MSM2)
                    {
                        br.ReadBytes(5);
                        info.Format    = br.ReadByte();
                        br.ReadByte();
                        info.Mipmaps   = br.ReadByte();
                        info.HDMipmaps = br.ReadByte();
                        br.ReadBytes(4);
                    }
                    else
                    {
                        // layout confirmed against silktexture's Source.cs legacy branch
                        br.ReadByte();
                        info.Format    = br.ReadUInt16();
                        br.ReadBytes(8);
                        info.Mipmaps   = br.ReadByte();
                        br.ReadByte();
                        info.HDMipmaps = br.ReadByte();
                        br.ReadBytes(11);
                    }

                    info.SourceFile = filePath;
                    info.HasHD = File.Exists(Path.ChangeExtension(filePath, ".hd.texture"));
                    return info;
                }
            }
            catch { return null; }
        }

        // maps a dxgi format code to its name, same table as silktexture's dxgi.cs
        public static string GetDXGIFormatName(int format)
        {
            switch (format)
            {
                case 0:   return "UNKNOWN";
                case 1:   return "R32G32B32A32_TYPELESS";
                case 2:   return "R32G32B32A32_FLOAT";
                case 3:   return "R32G32B32A32_UINT";
                case 4:   return "R32G32B32A32_SINT";
                case 5:   return "R32G32B32_TYPELESS";
                case 6:   return "R32G32B32_FLOAT";
                case 7:   return "R32G32B32_UINT";
                case 8:   return "R32G32B32_SINT";
                case 9:   return "R16G16B16A16_TYPELESS";
                case 10:  return "R16G16B16A16_FLOAT";
                case 11:  return "R16G16B16A16_UNORM";
                case 12:  return "R16G16B16A16_UINT";
                case 13:  return "R16G16B16A16_SNORM";
                case 14:  return "R16G16B16A16_SINT";
                case 15:  return "R32G32_TYPELESS";
                case 16:  return "R32G32_FLOAT";
                case 17:  return "R32G32_UINT";
                case 18:  return "R32G32_SINT";
                case 19:  return "R32G8X24_TYPELESS";
                case 20:  return "D32_FLOAT_S8X24_UINT";
                case 21:  return "R32_FLOAT_X8X24_TYPELESS";
                case 22:  return "X32_TYPELESS_G8X24_UINT";
                case 23:  return "R10G10B10A2_TYPELESS";
                case 24:  return "R10G10B10A2_UNORM";
                case 25:  return "R10G10B10A2_UINT";
                case 26:  return "R11G11B10_FLOAT";
                case 27:  return "R8G8B8A8_TYPELESS";
                case 28:  return "R8G8B8A8_UNORM";
                case 29:  return "R8G8B8A8_UNORM_SRGB";
                case 30:  return "R8G8B8A8_UINT";
                case 31:  return "R8G8B8A8_SNORM";
                case 32:  return "R8G8B8A8_SINT";
                case 33:  return "R16G16_TYPELESS";
                case 34:  return "R16G16_FLOAT";
                case 35:  return "R16G16_UNORM";
                case 36:  return "R16G16_UINT";
                case 37:  return "R16G16_SNORM";
                case 38:  return "R16G16_SINT";
                case 39:  return "R32_TYPELESS";
                case 40:  return "D32_FLOAT";
                case 41:  return "R32_FLOAT";
                case 42:  return "R32_UINT";
                case 43:  return "R32_SINT";
                case 44:  return "R24G8_TYPELESS";
                case 45:  return "D24_UNORM_S8_UINT";
                case 46:  return "R24_UNORM_X8_TYPELESS";
                case 47:  return "X24_TYPELESS_G8_UINT";
                case 48:  return "R8G8_TYPELESS";
                case 49:  return "R8G8_UNORM";
                case 50:  return "R8G8_UINT";
                case 51:  return "R8G8_SNORM";
                case 52:  return "R8G8_SINT";
                case 53:  return "R16_TYPELESS";
                case 54:  return "R16_FLOAT";
                case 55:  return "D16_UNORM";
                case 56:  return "R16_UNORM";
                case 57:  return "R16_UINT";
                case 58:  return "R16_SNORM";
                case 59:  return "R16_SINT";
                case 60:  return "R8_TYPELESS";
                case 61:  return "R8_UNORM";
                case 62:  return "R8_UINT";
                case 63:  return "R8_SNORM";
                case 64:  return "R8_SINT";
                case 65:  return "A8_UNORM";
                case 66:  return "R1_UNORM";
                case 67:  return "R9G9B9E5_SHAREDEXP";
                case 68:  return "R8G8_B8G8_UNORM";
                case 69:  return "G8R8_G8B8_UNORM";
                case 70:  return "BC1_TYPELESS";
                case 71:  return "BC1_UNORM";
                case 72:  return "BC1_UNORM_SRGB";
                case 73:  return "BC2_TYPELESS";
                case 74:  return "BC2_UNORM";
                case 75:  return "BC2_UNORM_SRGB";
                case 76:  return "BC3_TYPELESS";
                case 77:  return "BC3_UNORM";
                case 78:  return "BC3_UNORM_SRGB";
                case 79:  return "BC4_TYPELESS";
                case 80:  return "BC4_UNORM";
                case 81:  return "BC4_SNORM";
                case 82:  return "BC5_TYPELESS";
                case 83:  return "BC5_UNORM";
                case 84:  return "BC5_SNORM";
                case 85:  return "B5G6R5_UNORM";
                case 86:  return "B5G5R5A1_UNORM";
                case 87:  return "B8G8R8A8_UNORM";
                case 88:  return "B8G8R8X8_UNORM";
                case 89:  return "R10G10B10_XR_BIAS_A2_UNORM";
                case 90:  return "B8G8R8A8_TYPELESS";
                case 91:  return "B8G8R8A8_UNORM_SRGB";
                case 92:  return "B8G8R8X8_TYPELESS";
                case 93:  return "B8G8R8X8_UNORM_SRGB";
                case 94:  return "BC6H_TYPELESS";
                case 95:  return "BC6H_UF16";
                case 96:  return "BC6H_SF16";
                case 97:  return "BC7_TYPELESS";
                case 98:  return "BC7_UNORM";
                case 99:  return "BC7_UNORM_SRGB";
                case 100: return "AYUV";
                case 101: return "Y410";
                case 102: return "Y416";
                case 103: return "NV12";
                case 104: return "P010";
                case 105: return "P016";
                case 106: return "420_OPAQUE";
                case 107: return "YUY2";
                case 108: return "Y210";
                case 109: return "Y216";
                case 110: return "NV11";
                case 111: return "AI44";
                case 112: return "IA44";
                case 113: return "P8";
                case 114: return "A8P8";
                case 115: return "B4G4R4A4_UNORM";
                case 130: return "P208";
                case 131: return "V208";
                case 132: return "V408";
                default:  return $"Unknown ({format})";
            }
        }

        // extracts image data, width, height, and format from a .texture file (msm2 layout only)
        public static (byte[] imageData, int width, int height, int format) ExtractSilkCompatibleImageData(string filePath)
        {
            var asset = new AssetManager(File.ReadAllBytes(filePath));
            int offset = asset.GetAssetSectionOffset(Section.Texture.Content);
            int size   = asset.GetAssetSectionSize(Section.Texture.Content);
            if (offset < 0 || size <= 0) throw new InvalidOperationException("Invalid section offset/size");
            using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            using (var br = new BinaryReader(fs))
            {
                fs.Seek(offset, SeekOrigin.Begin);
                uint imgSize  = br.ReadUInt32();
                uint hdSize   = br.ReadUInt32();
                ushort width  = br.ReadUInt16();
                ushort height = br.ReadUInt16();
                ushort sdW    = br.ReadUInt16();
                ushort sdH    = br.ReadUInt16();
                ushort images = br.ReadUInt16();
                br.ReadByte();
                br.ReadBytes(5);
                byte format   = br.ReadByte();
                br.ReadByte(); br.ReadByte(); br.ReadByte();
                br.ReadBytes(4);
                int blockSize = (int)(imgSize / Math.Max(1, (int)images));
                byte[] data   = br.ReadBytes(blockSize);
                return (data, width, height, format);
            }
        }
    }
}
