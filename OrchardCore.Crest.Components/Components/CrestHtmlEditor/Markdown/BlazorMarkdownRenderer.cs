using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Crest.Components.Primitives.Documents.Markdown;

namespace Crest.Components.Primitives.Rendering;

#nullable enable

/// <summary>
/// Renders markdown content as Blazor components.
/// </summary>
internal class BlazorMarkdownRenderer(BlazorMarkdownRendererOptions options, RenderTreeBuilder builder, Action<RenderTreeBuilder, int> outlet) : NodeVisitorBase
{
    /// <summary>
    /// The outlet placeholder format.
    /// </summary>
    public const string Outlet = "<!--rz-outlet-{0}-->";

    private static readonly Regex OutletRegex = new(@"<!--rz-outlet-(\d+)-->");
    private static readonly Regex HtmlTagRegex = new(@"<(\w+)((?:\s+[^>]*)?)\/?>");
    private static readonly Regex HtmlClosingTagRegex = new(@"</(\w+)>");
    private static readonly Regex AttributeRegex = new(@"(\w+)(?:\s*=\s*(?:([""'])(.*?)\2|([^\s>]+)))?");
    private readonly HtmlSanitizer sanitizer = new(options.AllowedHtmlTags, options.AllowedHtmlAttributes);

    /// <inheritdoc />
    public override void VisitHeading(Heading heading)
    {
        builder.OpenComponent<CrestText>(0);
        builder.AddAttribute(1, nameof(CrestText.ChildContent), RenderChildren(heading.Children));

        switch (heading.Level)
        {
            case 1:
                builder.AddAttribute(2, nameof(CrestText.TextStyle), TextStyle.H1);
                break;
            case 2:
                builder.AddAttribute(3, nameof(CrestText.TextStyle), TextStyle.H2);
                break;
            case 3:
                builder.AddAttribute(4, nameof(CrestText.TextStyle), TextStyle.H3);
                break;
            case 4:
                builder.AddAttribute(5, nameof(CrestText.TextStyle), TextStyle.H4);
                break;
            case 5:
                builder.AddAttribute(6, nameof(CrestText.TextStyle), TextStyle.H5);
                break;
            case 6:
                builder.AddAttribute(7, nameof(CrestText.TextStyle), TextStyle.H6);
                break;
        }

        if (heading.Level <= options.AutoLinkHeadingDepth)
        {
            var anchor = Regex.Replace(heading.Value, @"[^\w\s-]", string.Empty).Replace(' ', '-').ToLowerInvariant().Trim();
            builder.AddAttribute(8, nameof(CrestText.Anchor), anchor);
        }
        else
        {
            builder.AddAttribute(9, nameof(CrestText.Anchor), (string?)null);
        }

        builder.CloseComponent();
    }

    /// <inheritdoc />
    public override void VisitTable(Table table)
    {
        builder.OpenComponent<CrestTable>(0);
        builder.AddAttribute(1, nameof(CrestTable.ChildContent), RenderChildren(table.Rows));
        builder.CloseComponent();
    }

    /// <inheritdoc />
    public override void VisitTableRow(TableRow row)
    {
        builder.OpenComponent<CrestTableRow>(0);
        builder.AddAttribute(1, nameof(CrestTableRow.ChildContent), RenderChildren(row.Cells));
        builder.CloseComponent();
    }

    /// <inheritdoc />
    public override void VisitTableCell(TableCell cell)
    {
        builder.OpenComponent<CrestTableCell>(0);
        builder.AddAttribute(1, nameof(CrestTableCell.ChildContent), RenderChildren(cell.Children));
        RenderCellAlignment(builder, cell.Alignment);
        builder.CloseComponent();
    }

    private static void RenderCellAlignment(RenderTreeBuilder builder, TableCellAlignment alignment)
    {
        switch (alignment)
        {
            case TableCellAlignment.Center:
                builder.AddAttribute(2, nameof(CrestTableCell.Style), "text-align: center");
                break;
            case TableCellAlignment.Right:
                builder.AddAttribute(3, nameof(CrestTableCell.Style), "text-align: right");
                break;
        }
    }

    /// <inheritdoc />
    public override void VisitTableHeaderRow(TableHeaderRow header)
    {
        builder.OpenComponent<CrestTableHeader>(0);
        builder.AddAttribute(1, nameof(CrestTableHeader.ChildContent), new RenderFragment(headerBuilder =>
        {
            headerBuilder.OpenComponent<CrestTableHeaderRow>(0);
            headerBuilder.AddAttribute(1, nameof(CrestTableHeaderRow.ChildContent), new RenderFragment(headerRowBuilder =>
            {
                foreach (var cell in header.Cells)
                {
                    headerRowBuilder.OpenComponent<CrestTableHeaderCell>(0);
                    headerRowBuilder.AddAttribute(1, nameof(CrestTableHeaderCell.ChildContent), RenderChildren(cell.Children));
                    RenderCellAlignment(headerRowBuilder, cell.Alignment);
                    headerRowBuilder.CloseComponent();
                }
            }));
            headerBuilder.CloseComponent();
        }));
        builder.CloseComponent();
    }

    /// <inheritdoc />
    public override void VisitIndentedCodeBlock(IndentedCodeBlock code)
    {
        builder.OpenElement(0, "pre");
        builder.OpenElement(1, "code");
        builder.AddContent(2, code.Value);
        builder.CloseElement();
        builder.CloseElement();
    }

    /// <inheritdoc />
    public override void VisitParagraph(Paragraph paragraph)
    {
        if (paragraph.Parent is ListItem item && item.Parent is List list && list.Tight)
        {
            VisitChildren(paragraph.Children);
        }
        else
        {
            builder.OpenComponent<CrestText>(0);
            builder.AddAttribute(1, nameof(CrestText.ChildContent), RenderChildren(paragraph.Children));
            builder.CloseComponent();
        }
    }

    private RenderFragment RenderChildren(IEnumerable<INode> children)
    {
        return innerBuilder =>
        {
            var inner = new BlazorMarkdownRenderer(options, innerBuilder, outlet);
            inner.VisitChildren(children);
        };
    }

    /// <inheritdoc />
    public override void VisitBlockQuote(BlockQuote blockQuote)
    {
        builder.OpenElement(0, "blockquote");
        VisitChildren(blockQuote.Children);
        builder.CloseElement();
    }

    /// <inheritdoc />
    public override void VisitCode(Code code)
    {
        builder.OpenElement(0, "code");
        builder.AddContent(1, code.Value);
        builder.CloseElement();
    }

    /// <inheritdoc />
    public override void VisitStrong(Strong strong)
    {
        builder.OpenElement(0, "strong");
        VisitChildren(strong.Children);
        builder.CloseElement();
    }

    /// <inheritdoc />
    public override void VisitEmphasis(Emphasis emphasis)
    {
        builder.OpenElement(0, "em");
        VisitChildren(emphasis.Children);
        builder.CloseElement();
    }

    /// <inheritdoc />
    public override void VisitLink(Link link)
    {
        if (link.Destination?.StartsWith('#') == true)
        {
            builder.OpenComponent<CrestAnchor>(0);
            builder.AddAttribute(0, "href", link.Destination);
            if (!string.IsNullOrEmpty(link.Title))
            {
                builder.AddAttribute(1, "title", link.Title);
            }
            builder.AddAttribute(2, "class", "rz-link");
            builder.AddAttribute(3, nameof(CrestAnchor.ChildContent), RenderChildren(link.Children));
            builder.CloseComponent();
        }
        else
        {
            builder.OpenComponent<CrestLink>(0);

            if (!string.IsNullOrEmpty(link.Destination) && !HtmlSanitizer.IsDangerousUrl(link.Destination))
            {
                builder.AddAttribute(1, nameof(CrestLink.Path), link.Destination);
            }

            builder.AddAttribute(2, nameof(CrestLink.ChildContent), RenderChildren(link.Children));

            if (!string.IsNullOrEmpty(link.Title))
            {
                builder.AddAttribute(3, "title", link.Title);
            }
            builder.CloseComponent();
        }
    }

    /// <inheritdoc />
    public override void VisitImage(Image image)
    {
        builder.OpenComponent<CrestImage>(0);

        if (!string.IsNullOrEmpty(image.Destination) && !HtmlSanitizer.IsDangerousUrl(image.Destination))
        {
            builder.AddAttribute(1, nameof(CrestImage.Path), image.Destination);
        }

        if (!string.IsNullOrEmpty(image.Title))
        {
            builder.AddAttribute(2, nameof(CrestImage.AlternateText), image.Title);
        }

        builder.CloseElement();
    }

    /// <inheritdoc />
    public override void VisitOrderedList(OrderedList orderedList)
    {
        builder.OpenElement(0, "ol");
        VisitChildren(orderedList.Children);
        builder.CloseElement();
    }

    /// <inheritdoc />
    public override void VisitUnorderedList(UnorderedList unorderedList)
    {
        builder.OpenElement(0, "ul");
        VisitChildren(unorderedList.Children);
        builder.CloseElement();
    }

    /// <inheritdoc />
    public override void VisitListItem(ListItem listItem)
    {
        builder.OpenElement(0, "li");
        VisitChildren(listItem.Children);
        builder.CloseElement();
    }

    /// <inheritdoc />
    public override void VisitFencedCodeBlock(FencedCodeBlock fencedCodeBlock)
    {
        builder.OpenElement(0, "pre");
        builder.OpenElement(1, "code");
        builder.AddContent(2, fencedCodeBlock.Value);
        builder.CloseElement();
        builder.CloseElement();
    }

    /// <inheritdoc />
    public override void VisitThematicBreak(ThematicBreak thematicBreak)
    {
        builder.OpenElement(0, "hr");
        builder.CloseElement();
    }

    /// <inheritdoc />
    public override void VisitHtmlBlock(HtmlBlock htmlBlock)
    {
        var match = OutletRegex.Match(htmlBlock.Value);

        if (match.Success)
        {
            var markerId = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
            outlet(builder, markerId);
        }
        else if (options.AllowHtml)
        {
            var html = sanitizer.Sanitize(htmlBlock.Value);
            builder.AddMarkupContent(0, html);
        }
        else
        {
            builder.AddContent(0, htmlBlock.Value);
        }
    }

    /// <inheritdoc />
    public override void VisitLineBreak(LineBreak lineBreak)
    {
        builder.OpenElement(0, "br");
        builder.CloseElement();
    }

    /// <inheritdoc />
    public override void VisitText(Crest.Components.Primitives.Documents.Markdown.Text text)
    {
        builder.AddContent(0, text.Value);
    }

    private static bool IsVoidElement(string tagName)
    {
        return tagName.ToLowerInvariant() switch
        {
            "area" => true,
            "base" => true,
            "br" => true,
            "col" => true,
            "embed" => true,
            "hr" => true,
            "img" => true,
            "input" => true,
            "link" => true,
            "meta" => true,
            "param" => true,
            "source" => true,
            "track" => true,
            "wbr" => true,
            _ => false
        };
    }

    /// <inheritdoc />
    public override void VisitSoftLineBreak(SoftLineBreak softBreak)
    {
        builder.AddContent(0, "\n");
    }

    /// <inheritdoc />
    public override void VisitHtmlInline(HtmlInline htmlInline)
    {
        if (htmlInline.Value == null)
        {
            return;
        }

        var match = OutletRegex.Match(htmlInline.Value);

        if (match.Success)
        {
            var markerId = Convert.ToInt32(match.Groups[1].Value, CultureInfo.InvariantCulture);
            outlet(builder, markerId);
            return;
        }

        if (!options.AllowHtml)
        {
            builder.AddContent(0, htmlInline.Value);
            return;
        }

        var html = sanitizer.Sanitize(htmlInline.Value);

        var closingMatch = HtmlClosingTagRegex.Match(html);

        if (closingMatch.Success)
        {
            builder.CloseElement();
            return;
        }

        var openingMatch = HtmlTagRegex.Match(html);

        if (openingMatch.Success)
        {
            var tagName = openingMatch.Groups[1].Value;

            builder.OpenElement(0, tagName);

            var attributes = openingMatch.Groups[2].Value;

            if (!string.IsNullOrEmpty(attributes))
            {
                var matches = AttributeRegex.Matches(attributes);

                foreach (Match attribute in matches)
                {
                    var name = attribute.Groups[1].Value;
                    var value = name;

                    if (attribute.Groups[2].Success) // Quoted value (either single or double)
                    {
                        value = attribute.Groups[3].Value;
                    }
                    else if (attribute.Groups[4].Success) // Unquoted value
                    {
                        value = attribute.Groups[4].Value;
                    }

                    builder.AddAttribute(1, name, value);
                }
            }

            if (html.EndsWith("/>", StringComparison.Ordinal) || IsVoidElement(tagName))
            {
                builder.CloseElement();
            }
        }
    }
}
