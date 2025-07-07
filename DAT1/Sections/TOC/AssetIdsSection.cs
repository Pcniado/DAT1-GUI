using DAT1.Sections.Generic;
using System.Collections.Generic;

namespace DAT1.Sections.TOC {
	public class AssetIdsSection: UInt64ArraySection {
		public const uint TAG = 0x506D7B8A; // Archive TOC Asset IDs

		public List<ulong> Ids => Values;
	}
}
