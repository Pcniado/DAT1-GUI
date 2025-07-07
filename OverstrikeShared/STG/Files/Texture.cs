using DAT1.Files;
using DAT1.Sections.Texture;
using System.IO;

namespace OverstrikeShared.STG.Files {
	public class Texture: STG {
		private Texture_I30 _texture;

		#region sections

		public TextureHeaderSection_I30 HeaderSection => _texture.HeaderSection;

		#endregion

		#region STG

		protected override DAT1.DAT1 ReadDat1(BinaryReader br) {
			var header = Header;
			DAT1.Utils.Assert(header != null);
			DAT1.Utils.Assert(header.Magic == Texture_I30.MAGIC);

			_texture = new Texture_I30(br);
			return _texture;
		}

		public override byte[] Save(bool packRawIfNoExtras = true) {
			return base.Save(packRawIfNoExtras);
		}

		#endregion

		#region API

		public byte[] GetDDS() {
			return _texture.GetDDS();
		}

		#endregion
	}
}
