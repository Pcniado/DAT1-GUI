// ... existing code ...

namespace OverstrikeShared.Utils {
	public class Clipboard {
		public static bool SetClipboard(string text) {
			try {
				System.Windows.Clipboard.SetText(text);
				return true;
			} catch {
				// if failed once, try a few more times (in some cases clipboard in windows might fail to open)
				for (int i = 0; i < 10; i++) {
					try {
						System.Windows.Clipboard.SetText(text);
						return true;
					} catch { }
				}
			}

			// if reached here, it means we failed all those times
			return false;
		}
	}
}
