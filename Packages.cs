using System.Text.RegularExpressions;

namespace Archinstaller;

public static partial class Packages
{
	public static readonly List<string> Basic =
	[
		"bat", "btop", "fastfetch", "fish", "lsd", "micro", "ripgrep", "starship", "tree",
		"man", "man-pages", "pacseek", "plymouth", "sshfs", "stow",
		"dotnet-sdk",
		"hip-runtime-amd", "vulkan-radeon",
	];

	public static readonly List<string> Desktop =
	[
		"grimblast-git", "hypridle", "hyprland", "hyprlock", "hyprpaper", "noctalia-git", "tofi",
		"breeze-gtk", "nerd-fonts", "nwg-look", "qt5ct", "qt6ct",
		"feh", "kitty", "librewolf", "dolphin-plugins", "ffmpegthumbs", "ark",
		"playerctl", "brightnessctl", "throne-bin", "vesktop-bin", "vscodium-bin-marketplace",
		"gamemode", "gamescope", "heroic-games-launcher-bin", "prismlauncher", "steam",
	];

	public static readonly List<string> Cursor = ["bibata-cursor-theme-bin"];

	public static readonly List<string> Custom = [];

	public static readonly Dictionary<string, string> HyprPlugins = new()
	{
		["https://github.com/virtcode/hypr-dynamic-cursors"] = "dynamic-cursors",
	};

	public static bool IsValidName(string name) => NameRegex().IsMatch(name);

	[GeneratedRegex(@"^[a-zA-Z0-9@._+-]+$")]
	private static partial Regex NameRegex();
}