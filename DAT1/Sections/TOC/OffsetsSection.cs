using DAT1.Sections.Generic;
using System.Collections.Generic;
using System.IO;

namespace DAT1.Sections.TOC {
	public class OffsetsSection: ArraySection<OffsetsSection.OffsetEntry> {
		public class OffsetEntry {
			public uint ArchiveIndex, Offset;
		}

		public const uint TAG = 0xDCD720B5;

		public List<OffsetEntry> Entries => Values;

		protected override uint GetValueByteSize() { return 8; }

		protected override OffsetEntry Read(BinaryReader r) {
			var a = r.ReadUInt32();
			var b = r.ReadUInt32();
			return new OffsetEntry() { ArchiveIndex = a, Offset = b };
		}

		protected override void Write(BinaryWriter w, OffsetEntry v) {
			w.Write(v.ArchiveIndex);
			w.Write(v.Offset);
		}
	}
}
