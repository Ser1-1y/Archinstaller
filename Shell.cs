using System.Diagnostics;
using Spectre.Console;

namespace Archinstaller;

public static class Shell
{
	public static bool Exists(string command) =>
		(Environment.GetEnvironmentVariable("PATH") ?? "")
		.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
		.Any(dir => File.Exists(Path.Combine(dir, command)));

	public static bool Run(string title, string file, IEnumerable<string> args, string? workDir = null)
	{
		AnsiConsole.Write(new Rule($"[cyan]{Markup.Escape(title)}[/]").LeftJustified());

		var psi = new ProcessStartInfo(file) { UseShellExecute = false };
		if (workDir is not null)
			psi.WorkingDirectory = workDir;
		foreach (var arg in args)
			psi.ArgumentList.Add(arg);

		AnsiConsole.MarkupLineInterpolated($"[grey]$ {file} {string.Join(' ', psi.ArgumentList)}[/]");

		try
		{
			using var process = Process.Start(psi)!;
			process.WaitForExit();

			if (process.ExitCode == 0)
			{
				AnsiConsole.MarkupLine("[green]Done.[/]\n");
				return true;
			}

			AnsiConsole.MarkupLineInterpolated($"[red]Failed ({process.ExitCode}).[/]\n");
		}
		catch (Exception ex)
		{
			AnsiConsole.MarkupLineInterpolated($"[red]{ex.Message}[/]\n");
		}

		return false;
	}

	public static string? Capture(string file, IEnumerable<string> args)
	{
		var psi = new ProcessStartInfo(file) { UseShellExecute = false, RedirectStandardOutput = true };
		foreach (var arg in args)
			psi.ArgumentList.Add(arg);

		try
		{
			using var process = Process.Start(psi)!;
			var output = process.StandardOutput.ReadToEnd();
			process.WaitForExit();
			return process.ExitCode == 0 ? output : null;
		}
		catch
		{
			return null;
		}
	}

	public static void Pause()
	{
		AnsiConsole.MarkupLine("[grey]Press any key to continue...[/]");
		Console.ReadKey(true);
	}

	public static void Notify(string markup)
	{
		AnsiConsole.MarkupLine(markup);
		Pause();
	}
}