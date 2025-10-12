using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using HtmlAgilityPack;
using ReverseMarkdown;

public static class LlmHtmlProcessor
{
    static private readonly Config _converterConfig = new Config
    {
        GithubFlavored = true,
        RemoveComments = true,
        SmartHrefHandling = true
    };

    public static List<string> HtmlToLlmMarkdown(
        string htmlInput,
        int chunkSize = 22000,
        int chunkOverlap = 800)
    {
        // Parse and clean HTML
        HtmlDocument doc = new();
        doc.LoadHtml(htmlInput);

        // Remove all <div> elements that contain class "key-alt"
        // e.g. Za pretragu DeWalt elektricne busilice, možete koristiti sledece varijacije kljucnih reci: DeWalt DWP849X...
        HtmlNodeCollection keyAltNodes = doc.DocumentNode.SelectNodes("//div[contains(@class, 'key-alt')]");
        if (keyAltNodes != null)
        {
            foreach (HtmlNode node in keyAltNodes.ToArray())
                node.Remove();
        }

        // Remove irrelevant / noisy tags
        string[] removeTags = { "iframe", "form", "img" };
        foreach (var tag in removeTags)
        {
            HtmlNodeCollection nodes = doc.DocumentNode.SelectNodes("//" + tag);
            if (nodes == null) continue;
            foreach (HtmlNode node in nodes.ToArray())
                node.Remove();
        }

        // Replace <br> with newline
        HtmlNodeCollection brNodes = doc.DocumentNode.SelectNodes("//br");
        if (brNodes != null)
        {
            foreach (HtmlNode br in brNodes.ToArray())
            {
                HtmlTextNode newline = doc.CreateTextNode("\n");
                br.ParentNode.ReplaceChild(newline, br);
            }
        }

        string cleanHtml = doc.DocumentNode.InnerHtml;

        // Decode HTML entities (&scaron; → š)
        string decodedHtml = WebUtility.HtmlDecode(cleanHtml);

        // Convert HTML → Markdown
        Converter converter = new Converter(_converterConfig);
        string markdownText = converter.Convert(decodedHtml);

        List<string> chunks = SplitIntoChunks(markdownText, chunkSize, chunkOverlap);

        return chunks;
    }

    private static List<string> SplitIntoChunks(string text, int chunkSize, int chunkOverlap)
    {
        if (string.IsNullOrEmpty(text))
            return [];

        if (text.Length <= chunkSize)
            return [text];

        List<string> chunks = new();

        int numberOfChunks = (int)Math.Ceiling((double)text.Length / chunkSize);
        int baseChunkSize = text.Length / numberOfChunks;

        int start = 0;

        for (int i = 0; i < numberOfChunks; i++)
        {
            if (i == numberOfChunks - 1)
            {
                chunks.Add(text.Substring(start));
                break;
            }

            int end = start + baseChunkSize;

            int length = Math.Min(end + chunkOverlap, text.Length) - start;

            chunks.Add(text.Substring(start, length));

            start += baseChunkSize;
        }

        return chunks;
    }
}
