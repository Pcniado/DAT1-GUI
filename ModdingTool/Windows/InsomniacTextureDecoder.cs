using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace ModdingTool.Windows
{
    public static class InsomniacTextureDecoder
    {
        // Map Insomniac format byte to DXGI_FORMAT (partial, expand as needed)
        private static uint MapInsomniacFormatToDXGI(byte formatByte)
        {
            // Reference: https://learn.microsoft.com/en-us/windows/win32/direct3ddds/dds-header-dx10
            // and SilkTexture's mapping
            switch (formatByte)
            {
                case 71: // 0x47
                    return 71; // DXGI_FORMAT_BC1_UNORM
                case 74: // 0x4A
                    return 74; // DXGI_FORMAT_BC3_UNORM
                case 77: // 0x4D
                    return 77; // DXGI_FORMAT_BC5_UNORM
                case 87: // 0x57
                    return 87; // DXGI_FORMAT_B8G8R8A8_UNORM
                case 115: // 0x73
                    return 115; // DXGI_FORMAT_B4G4R4A4_UNORM (unsupported by Pfim)
                // Add more mappings as needed
                default:
                    return formatByte; // fallback, may not be correct
            }
        }

        // Decodes a texel data block (raw or from .texture) and returns a DDS byte array, or null if not possible
        public static byte[] DecodeToDDS(byte[] sdData, byte[] hdData = null, int? overrideFormat = null)
        {
            StringBuilder debug = new StringBuilder();
            debug.AppendLine($"DecodeToDDS called. sdData.Length={sdData?.Length ?? 0}, hdData.Length={hdData?.Length ?? 0}, overrideFormat={overrideFormat}");
            try
            {
                byte[] combinedData;
                if (hdData != null && hdData.Length > 0)
                {
                    // Combine SD and HD data (SilkTexture appends HD after SD for export)
                    combinedData = new byte[sdData.Length + hdData.Length];
                    Buffer.BlockCopy(sdData, 0, combinedData, 0, sdData.Length);
                    Buffer.BlockCopy(hdData, 0, combinedData, sdData.Length, hdData.Length);
                    debug.AppendLine($"Combined SD+HD length: {combinedData.Length}");
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
                    // Read header fields (see Source.ExtractTextureInfo)
                    uint size = br.ReadUInt32();
                    uint hdSize = br.ReadUInt32();
                    ushort width = br.ReadUInt16();
                    ushort height = br.ReadUInt16();
                    ushort sd_width = br.ReadUInt16();
                    ushort sd_height = br.ReadUInt16();
                    ushort images = br.ReadUInt16();
                    byte channels = br.ReadByte();
                    byte formatByte = br.ReadByte();
                    uint format = overrideFormat.HasValue ? (uint)overrideFormat.Value : MapInsomniacFormatToDXGI(formatByte);
                    br.BaseStream.Seek(7, SeekOrigin.Current); // skip to mipmap data

                    debug.AppendLine($"size={size}, hdSize={hdSize}, width={width}, height={height}, sd_width={sd_width}, sd_height={sd_height}, images={images}, channels={channels}, formatByte={formatByte}, mappedDXGI={format}");
                    debug.AppendLine($"combinedData.Length={combinedData.Length}, ms.Position={ms.Position}");

                    // For now, just grab the first mipmap (no array)
                    byte[] mipmap = br.ReadBytes((int)size);
                    debug.AppendLine($"mipmap.Length={mipmap.Length}");

                    // --- No deswizzle: write data directly, matching SilkTexture ---
                    // --- DDS HEADER (ported from SilkTexture/SpideyTextureScaler) ---
                    bw.Write(Encoding.ASCII.GetBytes("DDS "));
                    bw.Write((uint)0x7c); // dwSize
                    bw.Write((uint)(0x1 | 0x2 | 0x4 | 0x1000 | 0x80000 | 0x20000)); // dwFlags
                    bw.Write((uint)height); // dwHeight
                    bw.Write((uint)width); // dwWidth
                    bw.Write((uint)size); // dwPitchOrLinearSize
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
                    // Write mipmap data
                    bw.Write(mipmap);
                    debug.AppendLine($"DDS header and mipmap written. Total output length: {outMs.Length}");

                    // Dump first 64 bytes of mipmap data
                    int dumpLen = Math.Min(64, mipmap.Length);
                    StringBuilder mipDump = new StringBuilder();
                    mipDump.AppendLine("First 64 bytes of mipmap data:");
                    for (int i = 0; i < dumpLen; i += 16)
                    {
                        mipDump.Append($"{i:X4}: ");
                        for (int j = 0; j < 16; j++)
                        {
                            if (i + j < dumpLen)
                                mipDump.Append($"{mipmap[i + j]:X2} ");
                            else
                                mipDump.Append("   ");
                        }
                        mipDump.Append(" ");
                        for (int j = 0; j < 16; j++)
                        {
                            if (i + j < dumpLen)
                            {
                                char c = (char)mipmap[i + j];
                                mipDump.Append(char.IsControl(c) ? '.' : c);
                            }
                        }
                        mipDump.AppendLine();
                    }
                    debug.AppendLine(mipDump.ToString());

                    // Dump first 128 bytes of DDS output
                    var ddsBytes = outMs.ToArray();
                    int ddsDumpLen = Math.Min(128, ddsBytes.Length);
                    StringBuilder ddsDump = new StringBuilder();
                    ddsDump.AppendLine("First 128 bytes of DDS output:");
                    for (int i = 0; i < ddsDumpLen; i += 16)
                    {
                        ddsDump.Append($"{i:X4}: ");
                        for (int j = 0; j < 16; j++)
                        {
                            if (i + j < ddsDumpLen)
                                ddsDump.Append($"{ddsBytes[i + j]:X2} ");
                            else
                                ddsDump.Append("   ");
                        }
                        ddsDump.Append(" ");
                        for (int j = 0; j < 16; j++)
                        {
                            if (i + j < ddsDumpLen)
                            {
                                char c = (char)ddsBytes[i + j];
                                ddsDump.Append(char.IsControl(c) ? '.' : c);
                            }
                        }
                        ddsDump.AppendLine();
                    }
                    debug.AppendLine(ddsDump.ToString());

                    return ddsBytes;
                }
            }
            catch (Exception ex)
            {
                debug.AppendLine($"Exception: {ex.Message}\n{ex.StackTrace}");
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