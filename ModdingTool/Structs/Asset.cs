//
// This program is free software, and can be redistributed and/or modified by you. It is provided 'as-is', without any warranty.
// For more details, terms and conditions, see GNU General Public License.
// A copy of the that license should come with this program (LICENSE.txt). If not, see <http://www.gnu.org/licenses/>.

using ModdingTool.Utils;

namespace ModdingTool.Structs {
	public class Asset {
		public byte Span { get; set; }
		public ulong Id;
		public uint Size { get; set; }
		public string SizeFormatted { get => SizeFormat.FormatSize(Size); }
		public bool HasHeader;

		public string? Name { get; set; }
		public string? Archive { get; set; }
		public string? FullPath = null;
		public string RefPath { get => $"{Span}/{Id:X016}"; }

		public string AssetType {
			get {
				string? fileName = FullPath ?? Name;
				if (string.IsNullOrEmpty(fileName)) return "unknown";
				var idx = fileName.LastIndexOf('.');
				if (idx >= 0 && idx < fileName.Length - 1)
					return fileName.Substring(idx + 1).ToLower();
				return "unknown";
			}
		}
	}
}
