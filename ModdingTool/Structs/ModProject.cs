using System.Collections.Generic;

namespace ModdingTool.Structs
{
    public class ModProject
    {
        public string ModName { get; set; }
        public string Author { get; set; }
        public string GameId { get; set; }
        public string GamePath { get; set; }
        public List<ReplacementEntry> Replacements { get; set; } = new();

        public class ReplacementEntry
        {
            public int Span { get; set; }
            public ulong Id { get; set; }
            public string Name { get; set; }
            public string FullPath { get; set; }
            public string Replacement { get; set; }
        }
    }
} 