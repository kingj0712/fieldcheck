using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using FieldCheck.Core.Models;
using FieldCheck.Core.Services;

namespace FieldCheck.App.Services;

/// <summary>
/// Produces a clean, functional printout for a checklist: project + checklist context, a progress
/// summary, then Open and Completed items grouped by section with notes and tags.
/// </summary>
public static class PrintService
{
    public static void Print(string projectName, Checklist checklist)
    {
        var dialog = new PrintDialog();
        if (dialog.ShowDialog() != true)
            return;

        var document = BuildDocument(projectName, checklist, dialog.PrintableAreaWidth);
        IDocumentPaginatorSource source = document;
        dialog.PrintDocument(source.DocumentPaginator, $"FieldCheck - {checklist.Name}");
    }

    private static FlowDocument BuildDocument(string projectName, Checklist checklist, double pageWidth)
    {
        var doc = new FlowDocument
        {
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 12,
            PagePadding = new Thickness(50),
            ColumnWidth = double.PositiveInfinity,
            Foreground = Brushes.Black
        };
        if (!double.IsNaN(pageWidth) && pageWidth > 0)
            doc.PageWidth = pageWidth;

        doc.Blocks.Add(new Paragraph(new Run("FieldCheck"))
        {
            FontSize = 11, Foreground = Brushes.Gray, Margin = new Thickness(0, 0, 0, 2)
        });
        doc.Blocks.Add(new Paragraph(new Run(checklist.Name))
        {
            FontSize = 22, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 2)
        });

        var meta = new Paragraph { FontSize = 11, Foreground = Brushes.Gray, Margin = new Thickness(0, 0, 0, 12) };
        if (!string.IsNullOrWhiteSpace(projectName))
            meta.Inlines.Add(new Run($"Project: {projectName}    "));
        meta.Inlines.Add(new Run($"Printed: {DateTime.Now:yyyy-MM-dd HH:mm}"));
        doc.Blocks.Add(meta);

        doc.Blocks.Add(new Paragraph(new Run($"Progress: {checklist.CompletedCount} of {checklist.TotalCount} complete"))
        {
            FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 12)
        });

        AddView(doc, "Open items", ChecklistService.OpenItems(checklist), isDone: false);
        AddView(doc, "Completed", ChecklistService.CompletedItems(checklist), isDone: true);

        return doc;
    }

    private static void AddView(FlowDocument doc, string heading, IEnumerable<ChecklistItem> items, bool isDone)
    {
        var groups = ChecklistService.GroupBySection(items);
        var total = groups.Sum(g => g.Items.Count);

        doc.Blocks.Add(new Paragraph(new Run($"{heading} ({total})"))
        {
            FontSize = 14, FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 10, 0, 4),
            BorderBrush = Brushes.LightGray, BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(0, 0, 0, 3)
        });

        if (total == 0)
        {
            doc.Blocks.Add(new Paragraph(new Run("None"))
            {
                Foreground = Brushes.Gray, FontStyle = FontStyles.Italic, Margin = new Thickness(0, 0, 0, 6)
            });
            return;
        }

        foreach (var group in groups)
        {
            doc.Blocks.Add(new Paragraph(new Run(group.Section))
            {
                FontSize = 11, FontWeight = FontWeights.SemiBold, Foreground = Brushes.DimGray,
                Margin = new Thickness(0, 8, 0, 2)
            });

            foreach (var item in group.Items)
            {
                var line = new Paragraph { Margin = new Thickness(0, 1, 0, 1) };
                line.Inlines.Add(new Run(isDone ? "☑  " : "☐  ") { FontFamily = new FontFamily("Segoe UI Symbol") });
                line.Inlines.Add(new Run(item.Text));
                if (isDone && item.CompletedAt is { } when)
                    line.Inlines.Add(new Run($"   ({when.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)})")
                    {
                        Foreground = Brushes.Gray, FontSize = 10
                    });
                doc.Blocks.Add(line);

                if (!string.IsNullOrWhiteSpace(item.Notes))
                    doc.Blocks.Add(new Paragraph(new Run(item.Notes))
                    {
                        Margin = new Thickness(22, 0, 0, 1), FontSize = 10.5, Foreground = Brushes.DimGray
                    });

                if (item.Tags.Count > 0)
                    doc.Blocks.Add(new Paragraph(new Run("Tags: " + string.Join(", ", item.Tags)))
                    {
                        Margin = new Thickness(22, 0, 0, 3), FontSize = 10, Foreground = Brushes.Gray
                    });
            }
        }
    }
}
