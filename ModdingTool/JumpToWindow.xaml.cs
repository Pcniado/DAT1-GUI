//
// This program is free software, and can be redistributed and/or modified by you. It is provided 'as-is', without any warranty.
// For more details, terms and conditions, see GNU General Public License.
// A copy of the that license should come with this program (LICENSE.txt). If not, see <http://www.gnu.org/licenses/>.

using System;
using System.Windows;
using System.Windows.Input;
using MahApps.Metro.Controls;

namespace ModdingTool;

public partial class JumpToWindow: MetroWindow {
	public bool Jumped = false;
	public string Path = null;

	public JumpToWindow() {
		InitializeComponent();
		this.Activated += OnActivated;
		this.Deactivated += OnDeactivated;
	}

	private void PathTextBox_KeyUp(object sender, KeyEventArgs e) {
		if (e.Key == Key.Enter) {
			Jump();
		}
	}

	private void JumpButton_Click(object sender, RoutedEventArgs e) {
		Jump();
	}

	private void Jump() {
		Jumped = true;
		Path = PathTextBox.Text;
		Close();
	}

	private void OnActivated(object sender, EventArgs e) {
		this.WindowTitleBrush = (System.Windows.Media.LinearGradientBrush)FindResource("AppTitleBarGradient");
	}

	private void OnDeactivated(object sender, EventArgs e) {
		this.WindowTitleBrush = (System.Windows.Media.LinearGradientBrush)FindResource("AppTitleBarGradient");
	}
}
