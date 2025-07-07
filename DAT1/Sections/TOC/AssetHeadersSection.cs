using DAT1.Sections.Generic;
using System.IO;

namespace DAT1.Sections.TOC {
	public class AssetHeadersSection: ByteBufferSection {
		public const uint TAG = 0x654BDED9; // Archive TOC Asset Header Data

		public byte[] ReadHeaderAtOffset(int offset) {
			// TODO: this doesn't work for RCRA, where all headers are 36 bytes and don't follow this MSM2 structure
			// (thus, extraction of assets into STG with Modding Tool is currently broken for RCRA)
			// as both seem to have the same "i29" format of toc/sections, there needs to be a way to reliably determine
			// whether it's RCRA or not

			byte[] sizes = Read(offset + 4, 4);

			using var r = new BinaryReader(new MemoryStream(sizes));
			r.ReadByte(); // unknown
			var pairs = r.ReadByte();
			var extra = r.ReadUInt16();

			var totalSize = 8 + pairs * 8 + extra;
			return Read(offset, totalSize);
		}
	}
}
