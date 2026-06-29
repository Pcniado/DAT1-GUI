using System;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace ModdingTool.Windows
{
    public static class InsomniacTextureDecoder
    {
        // m_DXGIFormat in the engine header is already a real DXGI_FORMAT value, no remapping needed
        private static bool IsBlockCompressedFormat(uint dxgiFormat)
        {
            switch (dxgiFormat)
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

        // Bytes per block for BC formats, bytes per texel otherwise. Ported from Engine/Render/DXGIFormatUtil.cpp GetElementBytes.
        private static uint GetElementBytes(uint dxgiFormat)
        {
            switch (dxgiFormat)
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
                    return 4; // best guess for anything not covered above
            }
        }

        // BC formats measure width/height in 4x4 blocks, uncompressed formats use texels directly
        private static uint GetWidthInElements(uint dxgiFormat, uint texelWidth)
        {
            return IsBlockCompressedFormat(dxgiFormat) ? ((texelWidth + 3) / 4) : texelWidth;
        }

        private static uint GetHeightInElements(uint dxgiFormat, uint texelHeight)
        {
            return IsBlockCompressedFormat(dxgiFormat) ? ((texelHeight + 3) / 4) : texelHeight;
        }

        // Row pitch for the top mip, same convention the engine uses for D3D11_SUBRESOURCE_DATA
        private static uint GetRowPitch(uint dxgiFormat, uint width)
        {
            return GetWidthInElements(dxgiFormat, width) * GetElementBytes(dxgiFormat);
        }

        // Decodes a texel data block (raw or from .texture) and returns a DDS byte array, or null if not possible
        public static byte[] DecodeToDDS(byte[] sdData, byte[] hdData = null, int? overrideFormat = null)
        {
            try
            {
                byte[] combinedData;
                if (hdData != null && hdData.Length > 0)
                {
                    // SD and HD get appended together, same as SilkTexture does for export
                    combinedData = new byte[sdData.Length + hdData.Length];
                    Buffer.BlockCopy(sdData, 0, combinedData, 0, sdData.Length);
                    Buffer.BlockCopy(hdData, 0, combinedData, sdData.Length, hdData.Length);
                }
                else
                {
                    combinedData = sdData;
                }
                using (var ms = new MemoryStream(combinedData))
                using (var br = new BinaryReader(ms))
                using (var outMs = new MemoryStream())
                using (var bw = new BinaryWriter(outMs))
                {
                    // Matches TextureHeaderBuilt layout in Engine/Texture/TextureHeader.h
                    uint size = br.ReadUInt32();
                    uint hdSize = br.ReadUInt32();
                    ushort width = br.ReadUInt16();
                    ushort height = br.ReadUInt16();
                    ushort sd_width = br.ReadUInt16();
                    ushort sd_height = br.ReadUInt16();
                    ushort images = br.ReadUInt16();
                    byte channels = br.ReadByte();
                    byte formatByte = br.ReadByte();
                    uint format = overrideFormat.HasValue ? (uint)overrideFormat.Value : (uint)formatByte; // m_DXGIFormat is a real DXGI_FORMAT already
                    br.BaseStream.Seek(7, SeekOrigin.Current); // skip to mipmap data

                    // Only the top mip for now (no array/mip chain support yet)
                    byte[] mipmap = br.ReadBytes((int)size);

                    uint rowPitch = GetRowPitch(format, width);

                    bw.Write(Encoding.ASCII.GetBytes("DDS "));
                    bw.Write((uint)0x7c); // dwSize
                    bw.Write((uint)(0x1 | 0x2 | 0x4 | 0x1000 | 0x80000 | 0x20000)); // dwFlags
                    bw.Write((uint)height); // dwHeight
                    bw.Write((uint)width); // dwWidth
                    bw.Write(rowPitch); // dwPitchOrLinearSize, computed per-format instead of the raw block size
                    bw.Write((uint)0); // dwDepth
                    bw.Write((uint)1); // dwMipMapCount
                    bw.Write(new byte[11 * 4]); // reserved
                    // DDS_PIXELFORMAT (32 bytes)
                    bw.Write((uint)32); // pfSize
                    bw.Write((uint)4); // pfFlags (FourCC)
                    bw.Write(Encoding.ASCII.GetBytes("DX10")); // FourCC
                    bw.Write(new byte[5 * 4]);
                    // caps
                    bw.Write((uint)(0x1000 | 0x8)); // DDSCAPS_TEXTURE | DDSCAPS_MIPMAP
                    bw.Write(new byte[4 * 4]); // caps2-4, reserved
                    // DDS_HEADER_DXT10 (20 bytes)
                    bw.Write((uint)format); // dxgiFormat
                    bw.Write((uint)3); // resourceDimension (2D)
                    bw.Write((uint)0); // miscFlag
                    bw.Write((uint)1); // arraySize
                    bw.Write((uint)0); // miscFlags2 (alpha mode)
                    bw.Write(mipmap);

                    return outMs.ToArray();
                }
            }
            catch (Exception ex)
            {
#if DEBUG
                File.AppendAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TextureViewer_debug.log"), $"[DecodeToDDS] {ex}\n");
#endif
                return null;
            }
        }

        // Add a helper to save DDS to disk if Pfim cannot display it
        public static void SaveDDSForExternalTool(byte[] ddsData, string suggestedPath = null)
        {
            try
            {
                string path = suggestedPath;
                if (string.IsNullOrEmpty(path))
                {
                    using (SaveFileDialog sfd = new SaveFileDialog())
                    {
                        sfd.Filter = "DDS files (*.dds)|*.dds|All files (*.*)|*.*";
                        sfd.FileName = "exported_texture.dds";
                        if (sfd.ShowDialog() != DialogResult.OK)
                            return;
                        path = sfd.FileName;
                    }
                }
                File.WriteAllBytes(path, ddsData);
            }
            catch (Exception ex)
            {
                // Handle exception (e.g., log it)
            }
        }

        // Overload for raw fallback: create a minimal header and call main DecodeToDDS
        public static byte[] DecodeToDDS(byte[] rawData, int width, int height, int format)
        {
            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms))
            {
                // Write a fake header (match the order in DecodeToDDS)
                bw.Write((uint)rawData.Length); // size
                bw.Write((uint)0); // hdSize
                bw.Write((ushort)width);
                bw.Write((ushort)height);
                bw.Write((ushort)width);
                bw.Write((ushort)height);
                bw.Write((ushort)1); // images
                bw.Write((byte)4); // channels (guess RGBA)
                bw.Write((byte)format); // format
                bw.Write(new byte[7]); // skip unknowns/mips
                bw.Write(rawData);
                return DecodeToDDS(ms.ToArray(), null, format);
            }
        }
    }
} 