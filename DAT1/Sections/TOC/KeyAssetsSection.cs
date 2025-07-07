using DAT1.Sections.Generic;
using System.Collections.Generic;

namespace DAT1.Sections.TOC {
	public class KeyAssetsSection: UInt64ArraySection {
		public const uint TAG = 0x6D921D7B; // Archive TOC Key Asset IDs

		public List<ulong> Ids => Values;
	}
}
