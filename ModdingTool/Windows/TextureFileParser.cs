using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.IO.Compression;
using System.Linq;
using System.Windows.Forms;
using ModdingTool.Windows;

namespace ModdingTool.Windows
{
    public static class TextureFileParser
    {
        public class TexelDataBlock
        {
            public int SpanIndex; // 0 = SD, 1 = HD
            public byte[] Data; // Raw texel data (may be compressed or custom format)
        }

        // Struct to hold extracted texture info
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

        // Extracts all Texel Data blocks (SD/HD) from a .texture file
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
                    if (fs.Length - fs.Position < 8)
                        break;
                    int nameLen = br.ReadInt32();
                    if (nameLen < 0 || nameLen > 64) break;
                    string name = new string(br.ReadChars(nameLen));
                    int blockSize = br.ReadInt32();
                    if (blockSize < 0 || blockSize > fs.Length - fs.Position) break;
                    if (name == "Texel Data")
                    {
                        // Read span index (4 bytes, usually 0=SD, 1=HD)
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

        // Returns the best DDS (HD preferred, fallback to SD) as a byte array, or null if not possible
        public static byte[] GetBestDDS(string filePath, out int spanIndex)
        {
            spanIndex = -1;
            if (!File.Exists(filePath))
                return null;
            try
            {
                string hdPath = System.IO.Path.ChangeExtension(filePath, ".hd.texture");
                if (File.Exists(hdPath))
                {
                    // Parse header from SD
                    var asset = new AssetManager(File.ReadAllBytes(filePath));
                    int offset = asset.GetAssetSectionOffset(Section.Texture.Content);
                    int size = asset.GetAssetSectionSize(Section.Texture.Content);
                    if (offset < 0 || size <= 0)
                        return null;
                    using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                    using (var br = new BinaryReader(fs))
                    {
                        fs.Seek(offset, SeekOrigin.Begin);
                        uint imgSize = br.ReadUInt32();
                        uint hdSize = br.ReadUInt32();
                        ushort width = br.ReadUInt16();
                        ushort height = br.ReadUInt16();
                        ushort sdWidth = br.ReadUInt16();
                        ushort sdHeight = br.ReadUInt16();
                        ushort images = br.ReadUInt16();
                        byte channels = br.ReadByte();
                        br.ReadBytes(5); // skip 5 bytes (unknown)
                        byte format = br.ReadByte(); // format byte
                        br.ReadByte(); // skip unknown byte
                        br.ReadByte(); // skip mipmaps
                        br.ReadByte(); // skip HDMipmaps
                        br.ReadBytes(4); // skip 4 bytes (unknown)
                        // Read all bytes from HD file as image data
                        byte[] imageData = File.ReadAllBytes(hdPath);
#if DEBUG
                        File.AppendAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TextureViewer_debug.log"), $"[HD] Using SD header: width={width}, height={height}, format={format}, hdDataLen={imageData.Length}\n");
#endif
                        var dds = InsomniacTextureDecoder.DecodeToDDS(imageData, width, height, format);
                        if (dds != null)
                        {
                                spanIndex = 1;
                                    return dds;
                        }
                    }
                }
                // Fallback to SD
                var assetSD = new AssetManager(File.ReadAllBytes(filePath));
                int offsetSD = assetSD.GetAssetSectionOffset(Section.Texture.Content);
                int sizeSD = assetSD.GetAssetSectionSize(Section.Texture.Content);
                if (offsetSD < 0 || sizeSD <= 0)
                    return null;
                using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                using (var br = new BinaryReader(fs))
                {
                    fs.Seek(offsetSD, SeekOrigin.Begin);
                    long pos;
                    pos = fs.Position; uint imgSize = br.ReadUInt32();
#if DEBUG
                    File.AppendAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TextureViewer_debug.log"), $"[SD] imgSize @ 0x{pos:X}: {imgSize}\n");
#endif
                    pos = fs.Position; uint hdSize = br.ReadUInt32();
#if DEBUG
                    File.AppendAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TextureViewer_debug.log"), $"[SD] hdSize @ 0x{pos:X}: {hdSize}\n");
#endif
                    pos = fs.Position; ushort width = br.ReadUInt16();
#if DEBUG
                    File.AppendAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TextureViewer_debug.log"), $"[SD] width @ 0x{pos:X}: {width}\n");
#endif
                    pos = fs.Position; ushort height = br.ReadUInt16();
#if DEBUG
                    File.AppendAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TextureViewer_debug.log"), $"[SD] height @ 0x{pos:X}: {height}\n");
#endif
                    pos = fs.Position; ushort sdWidth = br.ReadUInt16();
#if DEBUG
                    File.AppendAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TextureViewer_debug.log"), $"[SD] sdWidth @ 0x{pos:X}: {sdWidth}\n");
#endif
                    pos = fs.Position; ushort sdHeight = br.ReadUInt16();
#if DEBUG
                    File.AppendAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TextureViewer_debug.log"), $"[SD] sdHeight @ 0x{pos:X}: {sdHeight}\n");
#endif
                    pos = fs.Position; ushort images = br.ReadUInt16();
#if DEBUG
                    File.AppendAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TextureViewer_debug.log"), $"[SD] images @ 0x{pos:X}: {images}\n");
#endif
                    pos = fs.Position; byte channels = br.ReadByte();
#if DEBUG
                    File.AppendAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TextureViewer_debug.log"), $"[SD] channels @ 0x{pos:X}: {channels}\n");
#endif
                    pos = fs.Position; byte[] skip5 = br.ReadBytes(5);
#if DEBUG
                    File.AppendAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TextureViewer_debug.log"), $"[SD] skip5 @ 0x{pos:X}: {BitConverter.ToString(skip5)}\n");
#endif
                    pos = fs.Position; byte formatByte = br.ReadByte();
#if DEBUG
                    File.AppendAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TextureViewer_debug.log"), $"[SD] format (byte) @ 0x{pos:X}: {formatByte}\n");
#endif
                    // Try alternate layout if formatByte is 0 or invalid
                    byte format = formatByte;
                    if (formatByte == 0 || formatByte == 0xFF)
                    {
                        pos = fs.Position; byte skip1 = br.ReadByte();
#if DEBUG
                        File.AppendAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TextureViewer_debug.log"), $"[SD] skip1 (alt) @ 0x{pos:X}: {skip1}\n");
#endif
                        pos = fs.Position; ushort formatAlt = br.ReadUInt16();
#if DEBUG
                        File.AppendAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TextureViewer_debug.log"), $"[SD] format (alt ushort) @ 0x{pos:X}: {formatAlt}\n");
#endif
                        format = (byte)formatAlt; // fallback for now
                    }
                    pos = fs.Position; byte unk = br.ReadByte();
#if DEBUG
                    File.AppendAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TextureViewer_debug.log"), $"[SD] skip unknown byte @ 0x{pos:X}: {unk}\n");
#endif
                    pos = fs.Position; byte mipmaps = br.ReadByte();
#if DEBUG
                    File.AppendAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TextureViewer_debug.log"), $"[SD] mipmaps @ 0x{pos:X}: {mipmaps}\n");
#endif
                    pos = fs.Position; byte hdmipmaps = br.ReadByte();
#if DEBUG
                    File.AppendAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TextureViewer_debug.log"), $"[SD] hdmipmaps @ 0x{pos:X}: {hdmipmaps}\n");
#endif
                    pos = fs.Position; byte[] skip4 = br.ReadBytes(4);
#if DEBUG
                    File.AppendAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TextureViewer_debug.log"), $"[SD] skip4 @ 0x{pos:X}: {BitConverter.ToString(skip4)}\n");
#endif
                    // Read all bytes from HD file as image data
                    byte[] imageData = File.Exists(hdPath) ? File.ReadAllBytes(hdPath) : br.ReadBytes((int)(imgSize / images));
#if DEBUG
                    File.AppendAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TextureViewer_debug.log"), $"[SD] width={width}, height={height}, format={format}, dataLen={imageData.Length}\n");
#endif
                    var dds = InsomniacTextureDecoder.DecodeToDDS(imageData, width, height, format);
                            if (dds != null)
                            {
                        spanIndex = File.Exists(hdPath) ? 1 : 0;
                                return dds;
                    }
                }
                return null;
            }
            catch (Exception ex)
            {
#if DEBUG
                File.AppendAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TextureViewer_debug.log"), $"[ERROR] {filePath}: {ex}\n");
#endif
                return null;
            }
        }

        // Helper to load DDS from a .texture file (SD or HD), with debug logging for format
        private static byte[] LoadDDSFromTextureFile(string filePath, out int spanIndex, string label)
        {
            spanIndex = -1;
            var asset = new AssetManager(File.ReadAllBytes(filePath));
            int offset = asset.GetAssetSectionOffset(Section.Texture.Content);
            int size = asset.GetAssetSectionSize(Section.Texture.Content);
            if (offset < 0 || size <= 0)
                return null;
            using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            using (var br = new BinaryReader(fs))
            {
                fs.Seek(offset, SeekOrigin.Begin);
                long headerStart = fs.Position;
                uint imgSize = br.ReadUInt32();
                uint hdSize = br.ReadUInt32();
                ushort width = br.ReadUInt16();
                ushort height = br.ReadUInt16();
                ushort sdWidth = br.ReadUInt16();
                ushort sdHeight = br.ReadUInt16();
                ushort images = br.ReadUInt16();
                byte channels = br.ReadByte();
                br.ReadBytes(5); // skip 5 bytes (unknown)
                long formatOffset = fs.Position;
                byte format = br.ReadByte(); // format byte
                br.ReadByte(); // skip unknown byte
                br.ReadByte(); // skip mipmaps
                br.ReadByte(); // skip HDMipmaps
                br.ReadBytes(4); // skip 4 bytes (unknown)
                // After parsing the header, read image data from current position
                long imageDataOffset = fs.Position;
#if DEBUG
                File.AppendAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TextureViewer_debug.log"), $"[{label}] Header start: 0x{headerStart:X}, Format offset: 0x{formatOffset:X}, Format: {format}, Image data offset: 0x{imageDataOffset:X}\n");
#endif
                int imageBlockSize = (int)(imgSize / images);
                byte[] imageData = br.ReadBytes(imageBlockSize); // Only the first image's data
                // Debug: dump first 32 bytes as hex
                StringBuilder hex = new StringBuilder();
                for (int i = 0; i < Math.Min(32, imageData.Length); i++)
                    hex.Append($"{imageData[i]:X2} ");
#if DEBUG
                File.AppendAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TextureViewer_debug.log"), $"[{label}] First 32 bytes: {hex}\n");
#endif
                var dds = InsomniacTextureDecoder.DecodeToDDS(imageData, width, height, format);
                if (dds != null)
                {
#if DEBUG
                    File.AppendAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TextureViewer_debug.log"), $"[{label}] DDS constructed: width={width}, height={height}, format={format}, mipmapDataLength={imageData.Length}, ddsLength={dds.Length}\n");
#endif
                    spanIndex = (label == "HD") ? 1 : 0;
                    return dds;
                }
                return null;
            }
        }

        // Helper to extract a Texel Data block with a given span index from a section byte array
        private static byte[] ExtractTexelDataBlockFromSection(byte[] sectionData, int wantedSpan)
        {
            bool foundAnyBlock = false;
            StringBuilder debugBlocks = new StringBuilder();
#if DEBUG
            string debugLogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TextureViewer_debug.log");
#endif
            using (var ms = new MemoryStream(sectionData))
            using (var br = new BinaryReader(ms))
            {
                ms.Seek(0, SeekOrigin.Begin);
                while (ms.Position < ms.Length)
                {
                    long blockStart = ms.Position;
                    if (ms.Length - ms.Position < 8)
                    break;
                    int nameLen = br.ReadInt32();
                    if (nameLen < 0 || nameLen > 64) break;
                    string name = new string(br.ReadChars(nameLen));
                    int blockSize = br.ReadInt32();
                    if (blockSize < 0 || blockSize > ms.Length - ms.Position) break;
                    debugBlocks.AppendLine($"Block: '{name}', Size: {blockSize}, Pos: 0x{blockStart:X}");
                    if (name == "Texel Data")
                    {
                        int spanIndex = br.ReadInt32();
                        byte[] data = br.ReadBytes(blockSize - 4);
                        if (spanIndex == wantedSpan)
                        {
                            // Log the offset and size of the extracted texel data block
#if DEBUG
                            File.AppendAllText(debugLogPath, $"Extracted Texel Data block: Span={spanIndex}, Offset=0x{blockStart:X}, Size={data.Length}\n");
#endif
                            return data;
                        }
                        foundAnyBlock = true;
                    }
                    else
                    {
                        ms.Seek(blockSize, SeekOrigin.Current);
                    }
                }
            }
            if (!foundAnyBlock)
            {
                // No debug popups or hex dumps; just return null silently
            }
            return null;
        }

        private static bool IsDDS(byte[] data)
        {
            return data.Length > 4 && data[0] == 0x44 && data[1] == 0x44 && data[2] == 0x53 && data[3] == 0x20; // 'DDS '
        }

        // Extracts all header info from a .texture or .hd.texture file (using .texture for header)
        public static TextureInfo ExtractTextureInfo(string filePath)
        {
            if (!File.Exists(filePath))
                return null;
#if DEBUG
            string debugLogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TextureViewer_debug.log");
#endif
            try
            {
                var asset = new AssetManager(File.ReadAllBytes(filePath));
                int offset = asset.GetAssetSectionOffset(Section.Texture.Content);
                int size = asset.GetAssetSectionSize(Section.Texture.Content);
                if (offset < 0 || size <= 0)
                    return null;
                using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                using (var br = new BinaryReader(fs))
                {
#if DEBUG
                    using (var debugWriter = new StreamWriter(debugLogPath, true))
                    {
                        debugWriter.WriteLine($"\n--- ExtractTextureInfo: {filePath} ---");
                        debugWriter.WriteLine($"Section offset: {offset}, size: {size}");
                        fs.Seek(offset, SeekOrigin.Begin);
                        var info = new TextureInfo();
                        long pos;
                        pos = fs.Position; info.Size = br.ReadUInt32(); debugWriter.WriteLine($"[{pos:X}] Size: {info.Size}");
                        pos = fs.Position; info.HDSize = br.ReadUInt32(); debugWriter.WriteLine($"[{pos:X}] HDSize: {info.HDSize}");
                        pos = fs.Position; info.Width = br.ReadUInt16(); debugWriter.WriteLine($"[{pos:X}] Width: {info.Width}");
                        pos = fs.Position; info.Height = br.ReadUInt16(); debugWriter.WriteLine($"[{pos:X}] Height: {info.Height}");
                        pos = fs.Position; info.SDWidth = br.ReadUInt16(); debugWriter.WriteLine($"[{pos:X}] SDWidth: {info.SDWidth}");
                        pos = fs.Position; info.SDHeight = br.ReadUInt16(); debugWriter.WriteLine($"[{pos:X}] SDHeight: {info.SDHeight}");
                        pos = fs.Position; info.Images = br.ReadUInt16(); debugWriter.WriteLine($"[{pos:X}] Images: {info.Images}");
                        pos = fs.Position; info.Channels = br.ReadByte(); debugWriter.WriteLine($"[{pos:X}] Channels: {info.Channels}");

                        if (asset._assetGame == AssetManager.Game.MSM2)
                        {
                            pos = fs.Position; var skip5 = br.ReadBytes(5); debugWriter.WriteLine($"[{pos:X}] Skip 5 bytes: {BitConverter.ToString(skip5)}");
                            pos = fs.Position; info.Format = br.ReadByte(); debugWriter.WriteLine($"[{pos:X}] Format (byte): {info.Format}");
                            pos = fs.Position; var unk = br.ReadByte(); debugWriter.WriteLine($"[{pos:X}] Skip unknown byte: {unk}");
                            pos = fs.Position; info.Mipmaps = br.ReadByte(); debugWriter.WriteLine($"[{pos:X}] Mipmaps: {info.Mipmaps}");
                            pos = fs.Position; info.HDMipmaps = br.ReadByte(); debugWriter.WriteLine($"[{pos:X}] HDMipmaps: {info.HDMipmaps}");
                            pos = fs.Position; var skip4 = br.ReadBytes(4); debugWriter.WriteLine($"[{pos:X}] Skip 4 bytes: {BitConverter.ToString(skip4)}");
                        }
                        else
                        {
                            pos = fs.Position; var skip1 = br.ReadByte(); debugWriter.WriteLine($"[{pos:X}] Skip 1 byte: {skip1}");
                            pos = fs.Position; info.Format = br.ReadUInt16(); debugWriter.WriteLine($"[{pos:X}] Format (ushort): {info.Format}");
                            pos = fs.Position; var skip8 = br.ReadBytes(8); debugWriter.WriteLine($"[{pos:X}] Skip 8 bytes: {BitConverter.ToString(skip8)}");
                            pos = fs.Position; info.Mipmaps = br.ReadByte(); debugWriter.WriteLine($"[{pos:X}] Mipmaps: {info.Mipmaps}");
                            pos = fs.Position; var skip1b = br.ReadByte(); debugWriter.WriteLine($"[{pos:X}] Skip 1 byte: {skip1b}");
                            pos = fs.Position; info.HDMipmaps = br.ReadByte(); debugWriter.WriteLine($"[{pos:X}] HDMipmaps: {info.HDMipmaps}");
                            pos = fs.Position; var skip11 = br.ReadBytes(11); debugWriter.WriteLine($"[{pos:X}] Skip 11 bytes: {BitConverter.ToString(skip11)}");
                        }

                        info.SourceFile = filePath;
                        info.HasHD = File.Exists(System.IO.Path.ChangeExtension(filePath, ".hd.texture"));
                        debugWriter.Flush();
                        return info;
                    }
#else
                    fs.Seek(offset, SeekOrigin.Begin);
                    var info = new TextureInfo();
                    long pos;
                    pos = fs.Position; info.Size = br.ReadUInt32();
                    pos = fs.Position; info.HDSize = br.ReadUInt32();
                    pos = fs.Position; info.Width = br.ReadUInt16();
                    pos = fs.Position; info.Height = br.ReadUInt16();
                    pos = fs.Position; info.SDWidth = br.ReadUInt16();
                    pos = fs.Position; info.SDHeight = br.ReadUInt16();
                    pos = fs.Position; info.Images = br.ReadUInt16();
                    pos = fs.Position; info.Channels = br.ReadByte();

                    if (asset._assetGame == AssetManager.Game.MSM2)
                    {
                        pos = fs.Position; br.ReadBytes(5);
                        pos = fs.Position; info.Format = br.ReadByte();
                        pos = fs.Position; br.ReadByte();
                        pos = fs.Position; info.Mipmaps = br.ReadByte();
                        pos = fs.Position; info.HDMipmaps = br.ReadByte();
                        pos = fs.Position; br.ReadBytes(4);
                    }
                    else
                    {
                        pos = fs.Position; br.ReadByte();
                        pos = fs.Position; info.Format = br.ReadUInt16();
                        pos = fs.Position; br.ReadBytes(8);
                        pos = fs.Position; info.Mipmaps = br.ReadByte();
                        pos = fs.Position; br.ReadByte();
                        pos = fs.Position; info.HDMipmaps = br.ReadByte();
                        pos = fs.Position; br.ReadBytes(11);
                    }

                    info.SourceFile = filePath;
                    info.HasHD = File.Exists(System.IO.Path.ChangeExtension(filePath, ".hd.texture"));
                    return info;
#endif
                }
            }
            catch (Exception ex)
            {
#if DEBUG
                File.AppendAllText(debugLogPath, $"[ERROR] {filePath}: {ex}\n");
#endif
                return null;
            }
        }

        // Returns a human-readable DXGI format name for a given format code
        public static string GetDXGIFormatName(int format)
        {
            switch (format)
            {
                case 98: return "BC7_UNORM (98)";
                case 97: return "BC7_TYPELESS (97)";
                case 99: return "BC7_UNORM_SRGB (99)";
                case 71: return "BC1_UNORM (71)";
                case 70: return "BC1_TYPELESS (70)";
                case 72: return "BC1_UNORM_SRGB (72)";
                case 74: return "BC2_UNORM (74)";
                case 73: return "BC2_TYPELESS (73)";
                case 75: return "BC2_UNORM_SRGB (75)";
                case 77: return "BC3_UNORM (77)";
                case 76: return "BC3_TYPELESS (76)";
                case 78: return "BC3_UNORM_SRGB (78)";
                case 83: return "BC5_UNORM (83)";
                case 82: return "BC5_TYPELESS (82)";
                case 84: return "BC5_SNORM (84)";
                case 87: return "B8G8R8A8_UNORM (87)";
                case 28: return "R8G8B8A8_UNORM (28)";
                case 61: return "R8_UNORM (61)";
                case 56: return "R16_UNORM (56)";
                case 115: return "B4G4R4A4_UNORM (115)";
                case 24: return "R10G10B10A2_UNORM (24)";
                case 2: return "R32G32B32A32_FLOAT (2)";
                case 10: return "R16G16B16A16_FLOAT (10)";
                case 41: return "R32_FLOAT (41)";
                case 40: return "D32_FLOAT (40)";
                case 55: return "D16_UNORM (55)";
                case 65: return "A8_UNORM (65)";
                default: return $"Unknown ({format})";
            }
        }

        // Extracts image data, width, height, and format from a .texture file exactly like SilkTexture
        public static (byte[] imageData, int width, int height, int format) ExtractSilkCompatibleImageData(string filePath)
        {
            var asset = new AssetManager(File.ReadAllBytes(filePath));
            int offset = asset.GetAssetSectionOffset(Section.Texture.Content);
            int size = asset.GetAssetSectionSize(Section.Texture.Content);
            if (offset < 0 || size <= 0)
                throw new InvalidOperationException("Invalid section offset/size");
            using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            using (var br = new BinaryReader(fs))
            {
                fs.Seek(offset, SeekOrigin.Begin);
                byte[] sectionData = br.ReadBytes(size);
                using (var ms = new MemoryStream(sectionData))
                using (var br2 = new BinaryReader(ms))
                {
                    uint imgSize = br2.ReadUInt32();
                    uint hdSize = br2.ReadUInt32();
                    ushort width = br2.ReadUInt16();
                    ushort height = br2.ReadUInt16();
                    ushort sdWidth = br2.ReadUInt16();
                    ushort sdHeight = br2.ReadUInt16();
                    ushort images = br2.ReadUInt16();
                    byte channels = br2.ReadByte();
                    br2.ReadBytes(5); // skip 5 bytes (unknown)
                    byte format = br2.ReadByte(); // format byte
                    br2.ReadByte(); // skip unknown byte
                    br2.ReadByte(); // skip mipmaps
                    br2.ReadByte(); // skip HDMipmaps
                    br2.ReadBytes(4); // skip 4 bytes (unknown)
                    int imageBlockSize = (int)(imgSize / images);
                    byte[] imageData = br2.ReadBytes(imageBlockSize); // Only the first image's data
                    return (imageData, width, height, format);
                }
            }
        }
    }
} 