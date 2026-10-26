using Spectre.Console;

namespace Archinstaller;

public enum PackageManager
{
	Pacman,
	Yay,
}

public static class Installer
{
	private const string CursorTheme = "Bibata-Modern-Classic";
	private const string CursorSize = "24";
	private const string PacmanConf = "/etc/pacman.conf";

	private static readonly string Home =
		Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

	private static readonly string DotfilesPath = Path.Combine(Home, ".dotfiles");
	public static PackageManager Manager { get; set; } = PackageManager.Pacman;

	public static string Repo { get; set; } = $"https://github.com/{Environment.UserName}/dotfiles.git";

	public static void SetupPackageManager()
	{
		if (Shell.Exists("yay"))
		{
			Manager = PackageManager.Yay;
			return;
		}

		if (!AnsiConsole.Confirm("[yellow]yay[/] is not installed. Install?"))
			return;

		AnsiConsole.Clear();

		const string dir = "/tmp/yay";
		if (Directory.Exists(dir))
			Directory.Delete(dir, true);

		var ok = Shell.Run("Installing build tools", "sudo", ["pacman", "-S", "--noconfirm", "--needed", "git", "base-devel"])
				 && Shell.Run("Cloning yay", "git", ["clone", "https://aur.archlinux.org/yay.git", dir])
				 && Shell.Run("Building yay", "makepkg", ["-si", "--noconfirm"], dir);

		if (ok && Shell.Exists("yay"))
			Manager = PackageManager.Yay;

		Shell.Pause();
	}

	private static bool RunPm(string title, params string[] args) =>
		Manager == PackageManager.Yay
			? Shell.Run(title, "yay", args)
			: Shell.Run(title, "sudo", ["pacman", .. args]);

	public static bool Install(string title, IEnumerable<string> packages, bool standalone = true)
	{
		if (standalone)
			AnsiConsole.Clear();

		var ok = InstallCore(title, packages);

		if (standalone)
			Shell.Pause();

		return ok;
	}

	private static bool InstallCore(string title, IEnumerable<string> packages)
	{
		var list = packages.Where(Packages.IsValidName).Distinct().ToList();

		if (list.Count == 0)
		{
			AnsiConsole.MarkupLine("[yellow]No packages to install.[/]");
			return false;
		}

		if (list.Contains("steam") && !EnsureMultilib())
			return false;

		if (Manager != PackageManager.Pacman) 
			return RunPm(title, ["-S", "--noconfirm", "--needed", .. list]);
		
		var official = OfficialPackages();
		var skipped = official is null 
			? new List<string>() 
			: list.Where(p => !official.Contains(p)).ToList();

		if (skipped.Count <= 0)
		{
			return list.Count != 0
				   && RunPm(title, ["-S", "--noconfirm", "--needed", .. list]);
		}

		AnsiConsole.MarkupLineInterpolated(
			$"[yellow]Skipping AUR package:[/] {string.Join(", ", skipped)}\n");
		
		list = [.. list.Except(skipped)];
		return list.Count != 0 
			   && RunPm(title, ["-S", "--noconfirm", "--needed", .. list]);
	}

	private static HashSet<string>? OfficialPackages()
	{
		var packages = Shell.Capture("pacman", ["-Slq"]);
		var groups = Shell.Capture("pacman", ["-Sgq"]);

		if (packages is null)
			return null;

		return
		[
			.. (packages + "\n" + groups)
			.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
		];
	}

	public static void InstallCursor()
	{
		AnsiConsole.Clear();

		if (!Install("Installing cursor theme", Packages.Cursor, false))
		{
			Shell.Pause();
			return;
		}

		if (Shell.Exists("gsettings"))
		{
			Shell.Run("Setting the GTK cursor theme", "gsettings",
					  ["set", "org.gnome.desktop.interface", "cursor-theme", CursorTheme]);
			Shell.Run("Setting the GTK cursor size", "gsettings",
					  ["set", "org.gnome.desktop.interface", "cursor-size", CursorSize]);
		}

		Shell.Pause();
	}


	public static void DownloadDotfiles()
	{
		AnsiConsole.Clear();

		if (Directory.Exists(DotfilesPath))
		{
			if (!AnsiConsole.Confirm(
					$"[yellow]{DotfilesPath}[/] already exists. Delete it?",
					false))
				return;

			Directory.Delete(DotfilesPath, true);
		}

		var cloned = RunPm("Installing stow and git", "-S", "--noconfirm", "--needed", "stow", "git")
					 && Shell.Run("Cloning dotfiles", "git", ["clone", Repo, DotfilesPath]);

		if (!cloned)
		{
			Shell.Pause();
			return;
		}

		var stowPackages = new DirectoryInfo(DotfilesPath)
						   .GetDirectories()
						   .Select(d => d.Name)
						   .Where(n => !n.StartsWith('.'))
						   .Order()
						   .ToList();

		if (stowPackages.Count == 0)
		{
			Shell.Notify("[yellow]No stow packages found in the repository.[/]");
			return;
		}

		if (Shell.Run("Stowing dotfiles", "stow", ["--adopt", "-t", Home, .. stowPackages], DotfilesPath))
			Shell.Run("Resetting adopted files", "git", ["reset", "--hard", "HEAD"], DotfilesPath);

		Shell.Pause();
	}

	public static void InstallPlugins()
	{
		AnsiConsole.Clear();

		if (!Shell.Exists("hyprpm"))
		{
			Shell.Notify("[red]hyprpm is still not available.[/]");
			return;
		}

		if (Packages.HyprPlugins.Count == 0)
		{
			Shell.Notify("[yellow]No plugins. Add one first.[/]");
			return;
		}

		var urls = AnsiConsole.Prompt(
			new MultiSelectionPrompt<string>()
				.Title("Select [green]plugins[/] to install")
				.InstructionsText("[grey]space - toggle\n enter - confirm)[/]")
				.NotRequired()
				.UseConverter(url => $"{Markup.Escape(Packages.HyprPlugins[url])} [grey]({Markup.Escape(url)})[/]")
				.AddChoices(Packages.HyprPlugins.Keys));

		if (urls.Count == 0)
			return;

		AnsiConsole.Clear();
		var ok = Shell.Run("Updating hyprpm", "hyprpm", ["update"]);

		foreach (var url in urls)
		{
			if (!ok)
				break;

			var name = Packages.HyprPlugins[url];
			ok = Shell.Run($"Adding {name}", "hyprpm", ["add", url])
				 && Shell.Run($"Enabling {name}", "hyprpm", ["enable", name]);
		}

		Shell.Run("Reloading plugins", "hyprpm", ["reload"]);

		Shell.Pause();
	}

	private static bool EnsureMultilib()
	{
		if (!File.Exists(PacmanConf))
		{
			AnsiConsole.MarkupLine($"[red]{PacmanConf} not found.[/]");
			return false;
		}

		if (IsMultilibEnabled())
			return true;

		return Shell.Run("Enabling multilib", "sudo",
						 ["sed", "-i", @"/^#\?\[multilib\]/{s/^#//;n;s/^#Include/Include/}", PacmanConf])
			   && RunPm("Upgrading system", "-Syu", "--noconfirm");
	}

	private static bool IsMultilibEnabled()
	{
		var lines = File.ReadAllLines(PacmanConf).Select(l => l.Trim()).ToList();
		var header = lines.IndexOf("[multilib]");

		if (header < 0)
			return false;

		foreach (var line in lines.Skip(header + 1))
		{
			if (line.StartsWith('['))
				return false;
			if (line.StartsWith("Include") || line.StartsWith("Server"))
				return true;
		}

		return false;
	}
}