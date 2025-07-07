using DAT1.Sections.Generic;
using System.Collections.Generic;
using System.IO;

namespace DAT1.Sections.TOC {
	public class SpansSection: ArraySection<SpansSection.Span> {
		public class Span {
			public uint AssetIndex, Count;
		}

		public const uint TAG = 0xEDE8ADA9; // Archive TOC Header

		public List<Span> Entries => Values;

		protected override uint GetValueByteSize() { return 8; }

		protected override Span Read(BinaryReader r) {
			var a = r.ReadUInt32();
			var b = r.ReadUInt32();
			return new Span() { AssetIndex = a, Count = b };
		}

		protected override void Write(BinaryWriter w, Span v) {
			w.Write(v.AssetIndex);
			w.Write(v.Count);
		}
	}
}
