using System;
using System.IO;
using System.Windows;
using System.Windows.Forms;
using ModdingTool.Structs;
using ModdingTool.Windows;

namespace ModdingTool.Utils
{
    public static class ProjectHelper
    {
        public static (string? folder, string? modName, string? author) CreateNewProject(Window owner)
        {
            var dialog = new FolderBrowserDialog();
            dialog.Description = "Select folder for new project";
            if (dialog.ShowDialog() != DialogResult.OK)
                return (null, null, null);
            string stageJsonPath = Path.Combine(dialog.SelectedPath, "stage.json");
            if (File.Exists(stageJsonPath))
            {
                var msgBox = new CustomMessageBox("A project (stage.json) already exists in this folder. Overwrite?", "Overwrite Project?", true);
                if (owner is System.Windows.Window w) msgBox.Owner = w;
                msgBox.ShowDialog();
                if (msgBox.Result != true)
                    return (null, null, null);
            }
            var prompt = new ModInfoPrompt("", "");
            if (owner is System.Windows.Window w2) prompt.Owner = w2;
            if (prompt.ShowDialog() == true)
            {
                var project = new ModProject
                {
                    ModName = prompt.ModName,
                    Author = prompt.Author,
                    GameId = null,
                    GamePath = null,
                    Replacements = new System.Collections.Generic.List<ModProject.ReplacementEntry>()
                };
                var json = System.Text.Json.JsonSerializer.Serialize(project, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(stageJsonPath, json);
                return (dialog.SelectedPath, prompt.ModName, prompt.Author);
            }
            return (null, null, null);
        }

        public static string? LoadProject(Window owner)
        {
            var dialog = new FolderBrowserDialog();
            dialog.Description = "Select project folder to open";
            if (dialog.ShowDialog() != DialogResult.OK)
                return null;
            string stageJsonPath = Path.Combine(dialog.SelectedPath, "stage.json");
            if (!File.Exists(stageJsonPath))
            {
                var msgBox = new CustomMessageBox("No project (stage.json) found in this folder.", "Error", false);
                if (owner is System.Windows.Window w) msgBox.Owner = w;
                msgBox.ShowDialog();
                return null;
            }
            return dialog.SelectedPath;
        }
    }
} 