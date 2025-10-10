using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using HtmlAgilityPack;
using ReverseMarkdown;

public static class LlmHtmlProcessor
{
    public static List<string> HtmlToLlmMarkdown(
        string htmlInput,
        int chunkSize = 22000,
        int chunkOverlap = 800)
    {
        // Parse and clean HTML
        var doc = new HtmlDocument();
        doc.LoadHtml(htmlInput);

        // Remove all <div> elements that contain class "key-alt"
        // e.g. Za pretragu DeWalt elektricne busilice, možete koristiti sledece varijacije kljucnih reci: DeWalt DWP849X...
        var keyAltNodes = doc.DocumentNode.SelectNodes("//div[contains(@class, 'key-alt')]");
        if (keyAltNodes != null)
        {
            foreach (var node in keyAltNodes.ToArray())
                node.Remove();
        }

        // Remove irrelevant / noisy tags
        string[] removeTags = { "iframe", "form", "img" };
        foreach (var tag in removeTags)
        {
            var nodes = doc.DocumentNode.SelectNodes("//" + tag);
            if (nodes == null) continue;
            foreach (var node in nodes.ToArray())
                node.Remove();
        }

        // Replace <br> with newline
        var brNodes = doc.DocumentNode.SelectNodes("//br");
        if (brNodes != null)
        {
            foreach (var br in brNodes.ToArray())
            {
                var newline = doc.CreateTextNode("\n");
                br.ParentNode.ReplaceChild(newline, br);
            }
        }

        string cleanHtml = doc.DocumentNode.InnerHtml;

        // Decode HTML entities (&scaron; → š)
        string decodedHtml = WebUtility.HtmlDecode(cleanHtml);

        // Convert HTML → Markdown
        var config = new Config
        {
            GithubFlavored = true,
            RemoveComments = true,
            SmartHrefHandling = true
        };
        var converter = new Converter(config);
        string markdownText = converter.Convert(decodedHtml);

        // Chunk text intelligently
        var chunks = SplitIntoChunks(markdownText, chunkSize, chunkOverlap);

        return chunks;
    }

    private static List<string> SplitIntoChunks(string text, int chunkSize, int chunkOverlap)
    {
        var separators = new[] { "\n## ", "\n# ", "\n", ". ", " " };
        var chunks = new List<string>();

        int start = 0;
        while (start < text.Length)
        {
            int end = Math.Min(start + chunkSize, text.Length);
            int bestSplit = end;

            // Try to split at a natural boundary
            foreach (var sep in separators)
            {
                int idx = text.LastIndexOf(sep, end, StringComparison.Ordinal);
                if (idx > start && idx < end)
                {
                    bestSplit = idx + sep.Length;
                    break;
                }
            }

            string chunk = text.Substring(start, bestSplit - start).Trim();
            if (!string.IsNullOrEmpty(chunk))
                chunks.Add(chunk);

            start = bestSplit - chunkOverlap;
            if (start < 0) start = 0;
        }

        return chunks;
    }
}
