using System.IO;

namespace OverstrikeShared.Utils {
	public class BinaryStreams {
		public static void Align16(BinaryReader br) {
			Align(br, 16);
		}

		public static void Align(BinaryReader br, int count) {
			var pos = br.BaseStream.Position % count;
			if (pos != 0) {
				var rem = count - pos;
				br.ReadBytes((int)rem);
			}
		}

		public static void Align16(BinaryWriter bw) {
			Align(bw, 16);
		}

		public static void Align(BinaryWriter bw, int count) {
			var pos = bw.BaseStream.Position % count;
			if (pos != 0) {
				var rem = count - pos;
				bw.Write(new byte[rem]);
			}
		}
	}
}
