using System.Windows;
using System.Windows.Controls;
using Knode.Services;

namespace Knode;

public partial class OneNoteSectionPickerWindow : Window
{
    private readonly List<OneNoteSection> _sections;

    public IReadOnlyList<OneNoteSection> SelectedSections { get; private set; } = Array.Empty<OneNoteSection>();

    public OneNoteSectionPickerWindow(
        IReadOnlyList<OneNoteSection> sections,
        IReadOnlyCollection<string> preselectedSectionIds)
    {
        InitializeComponent();
        _sections = sections.OrderBy(s => s.NotebookName).ThenBy(s => s.DisplayName).ToList();
        SectionsList.ItemsSource = _sections;
        SectionsList.SelectionChanged += SectionsList_SelectionChanged;

        if (preselectedSectionIds.Count > 0)
        {
            foreach (var section in _sections)
            {
                if (!preselectedSectionIds.Contains(section.Id))
                    continue;
                SectionsList.SelectedItems.Add(section);
            }
        }

        UpdateSelectionHint();
    }

    private void SectionsList_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateSelectionHint();

    private void UpdateSelectionHint()
    {
        var count = SectionsList.SelectedItems.Count;
        SelectionHintText.Text = count == 0
            ? "No sections selected."
            : $"{count} section(s) selected.";
    }

    private void SelectAll_Click(object sender, RoutedEventArgs e)
    {
        SectionsList.SelectAll();
        UpdateSelectionHint();
    }

    private void ClearSelection_Click(object sender, RoutedEventArgs e)
    {
        SectionsList.SelectedItems.Clear();
        UpdateSelectionHint();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void UseSelected_Click(object sender, RoutedEventArgs e)
    {
        SelectedSections = SectionsList.SelectedItems.OfType<OneNoteSection>().ToList();
        DialogResult = true;
        Close();
    }
}
