using Spectre.Console;

namespace Archinstaller;

public static class Program
{
	public static void Main()
	{
		if (Environment.IsPrivilegedProcess)
		{
			AnsiConsole.MarkupLine(
				"[red]Don't run as root.[/]");
			return;
		}

		Console.CancelKeyPress += (_, _) => Console.CursorVisible = true;

		Installer.SetupPackageManager();

		Menu("Archinstaller",
			 ("Install basic packages", () => Installer.Install("Installing basic packages", Packages.Basic)),
			 ("Install desktop packages", () => Installer.Install("Installing desktop packages", Packages.Desktop)),
			 ("Download dotfiles", Installer.DownloadDotfiles),
			 ("Install cursor", Installer.InstallCursor),
			 ("Customize", CustomizeMenu),
			 ("Settings", SettingsMenu),
			 ("Exit", null));

		AnsiConsole.Clear();
	}

	private static void Menu(string title, params (string Label, Action? Run)[] items)
	{
		while (true)
		{
			AnsiConsole.Clear();

			var choice = AnsiConsole.Prompt(
				new SelectionPrompt<(string Label, Action? Run)>()
					.Title($"[bold blue]{Markup.Escape(title)}[/]")
					.UseConverter(i => Markup.Escape(i.Label))
					.AddChoices(items));

			if (choice.Run is null)
				return;

			choice.Run();
		}
	}

	private static void CustomizeMenu() =>
		Menu("Customize",
			 ("Basic packages", () => EditPackages("Basic packages", Packages.Basic)),
			 ("Desktop packages", () => EditPackages("Desktop packages", Packages.Desktop)),
			 ("Custom packages", () => EditPackages("Custom packages", Packages.Custom)),
			 ("Install custom packages", () => Installer.Install("Installing custom packages", Packages.Custom)),
			 ("Hyprland plugins", PluginsMenu),
			 ("Back", null));

	private static void PluginsMenu() =>
		Menu("Hyprland plugins",
			 ("Install plugins", Installer.InstallPlugins),
			 ("Add custom plugin", AddPlugin),
			 ("Back", null));

	private static void SettingsMenu() =>
		Menu("Settings",
			 ("Change GitHub repository", ChangeRepo),
			 ("Change package manager", ChangePackageManager),
			 ("Back", null));

	private static void EditPackages(string title, List<string> packages) =>
		Menu(title,
			 ("Show / add packages", () => AddPackages(title, packages)),
			 ("Remove packages", () => RemovePackages(packages)),
			 ("Back", null));

	private static void AddPackages(string title, List<string> packages)
	{
		AnsiConsole.Clear();
		AnsiConsole.Write(Markup.Escape(title));
		AnsiConsole.WriteLine();

		var input = AnsiConsole.Prompt(
			new TextPrompt<string>("Packages to add [grey](space-separated)[/]:")
				.AllowEmpty()
				.Validate(text =>
				{
					var invalid = text.Split(' ', StringSplitOptions.RemoveEmptyEntries)
									  .FirstOrDefault(n => !Packages.IsValidName(n));
					return invalid is null
						? ValidationResult.Success()
						: ValidationResult.Error($"[red]Invalid package name:[/] {Markup.Escape(invalid)}");
				}));

		foreach (var name in input.Split(' ', StringSplitOptions.RemoveEmptyEntries))
		{
			if (!packages.Contains(name))
				packages.Add(name);
		}
	}

	private static void RemovePackages(List<string> packages)
	{
		if (packages.Count == 0)
		{
			Shell.Notify("[yellow]Nothing to remove.[/]");
			return;
		}

		var toRemove = AnsiConsole.Prompt(
			new MultiSelectionPrompt<string>()
				.Title("Select packages to [red]remove[/]")
				.InstructionsText("[grey]space - toggle\n enter - confirm[/]")
				.NotRequired()
				.PageSize(20)
				.MoreChoicesText("[grey](move to show more)[/]")
				.AddChoices(packages));

		packages.RemoveAll(toRemove.Contains);
	}

	private static void AddPlugin()
	{
		AnsiConsole.Clear();

		var url = AnsiConsole.Prompt(
								 new TextPrompt<string>("Plugin repository URL:")
									 .AllowEmpty()
									 .Validate(u => u.Length == 0 || u.StartsWith("https://github.com/")
												   ? ValidationResult.Success()
												   : ValidationResult.Error(
													   "[red]Must be a GitHub repository URL.[/]")))
							 .Trim();

		if (url.Length == 0)
			return;

		var defaultName = url.TrimEnd('/').Split('/').Last();
		if (defaultName.EndsWith(".git"))
			defaultName = defaultName[..^4];
		var name = AnsiConsole.Prompt(new TextPrompt<string>("Plugin name:").DefaultValue(defaultName));

		Packages.HyprPlugins[url] = name;
	}

	private static void ChangeRepo()
	{
		AnsiConsole.Clear();
		AnsiConsole.MarkupLineInterpolated($"Current: [blue]{Installer.Repo}[/]\n");

		var user = AnsiConsole.Prompt(
			new TextPrompt<string>("GitHub username:").DefaultValue(Environment.UserName));
		var repo = AnsiConsole.Prompt(
			new TextPrompt<string>("Repository:").DefaultValue("dotfiles"));

		Installer.Repo = $"https://github.com/{user}/{repo}.git";
	}

	private static void ChangePackageManager()
	{
		AnsiConsole.Clear();

		Installer.Manager = AnsiConsole.Prompt(
			new SelectionPrompt<PackageManager>()
				.Title("Package manager")
				.AddChoices(Enum.GetValues<PackageManager>())
				.DefaultValue(Installer.Manager));
	}
}