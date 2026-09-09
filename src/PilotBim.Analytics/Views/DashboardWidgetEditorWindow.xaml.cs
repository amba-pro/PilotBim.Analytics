using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using PilotBim.Analytics.Models;
using UiResources = PilotBim.Analytics.Properties.Resources;

namespace PilotBim.Analytics.Views
{
    public partial class DashboardWidgetEditorWindow : Window
    {
        private readonly bool _chartOnly;

        public DashboardWidgetEditorWindow(
            IEnumerable<ChartOptionItem> sources,
            IEnumerable<ChartOptionItem> kinds,
            IEnumerable<ChartOptionItem> topN,
            DashboardWidgetState existing,
            bool chartOnly)
        {
            InitializeComponent();
            _chartOnly = chartOnly;

            KindBox.ItemsSource = BuildKindOptions(chartOnly);
            SourceBox.ItemsSource = sources;
            ChartKindBox.ItemsSource = kinds;
            TopNBox.ItemsSource = topN;
            SpanBox.ItemsSource = new[]
            {
                new ChartOptionItem { Id = "2", Title = "На всю ширину" },
                new ChartOptionItem { Id = "1", Title = "Половина" }
            };

            if (existing != null)
            {
                TitleBox.Text = existing.Title ?? "";
                SelectById(KindBox, existing.WidgetKind == DashboardWidgetKinds.Chart
                    ? DashboardWidgetKinds.Chart
                    : existing.WidgetKind);
                SelectById(SourceBox, existing.ChartSource ?? "Types");
                SelectById(ChartKindBox, existing.ChartKind ?? "HorizontalBar");
                SelectById(TopNBox, existing.TopN.ToString());
                SelectById(SpanBox, existing.ColumnSpan <= 1 ? "1" : "2");
            }
            else
            {
                TitleBox.Text = chartOnly ? "Новый график" : "KPI / сводка";
                KindBox.SelectedIndex = 0;
                SourceBox.SelectedIndex = 0;
                ChartKindBox.SelectedIndex = 0;
                TopNBox.SelectedIndex = 1;
                SpanBox.SelectedIndex = 0;
            }

            UpdateChartPanelVisibility();
        }

        public DashboardWidgetState Result { get; private set; }

        private static List<ChartOptionItem> BuildKindOptions(bool chartOnly)
        {
            if (chartOnly)
            {
                return new List<ChartOptionItem>
                {
                    new ChartOptionItem { Id = DashboardWidgetKinds.Chart, Title = "График / диаграмма" }
                };
            }

            return new List<ChartOptionItem>
            {
                new ChartOptionItem { Id = DashboardWidgetKinds.Chart, Title = "График / диаграмма" },
                new ChartOptionItem { Id = DashboardWidgetKinds.Kpi, Title = "KPI / сводка" },
                new ChartOptionItem { Id = DashboardWidgetKinds.Bim, Title = "BIM сводка" },
                new ChartOptionItem { Id = DashboardWidgetKinds.Responsible, Title = "Ответственные" }
            };
        }

        private static void SelectById(ComboBox box, string id)
        {
            if (box.ItemsSource == null)
                return;
            foreach (var item in box.ItemsSource)
            {
                var opt = item as ChartOptionItem;
                if (opt != null && opt.Id == id)
                {
                    box.SelectedItem = opt;
                    return;
                }
            }
            if (box.Items.Count > 0)
                box.SelectedIndex = 0;
        }

        private void KindBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateChartPanelVisibility();
        }

        private void UpdateChartPanelVisibility()
        {
            var kind = GetSelectedId(KindBox) ?? DashboardWidgetKinds.Chart;
            ChartPanel.Visibility = kind == DashboardWidgetKinds.Chart
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private static string GetSelectedId(ComboBox box)
        {
            var opt = box.SelectedItem as ChartOptionItem;
            return opt != null ? opt.Id : null;
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            var kind = GetSelectedId(KindBox) ?? DashboardWidgetKinds.Chart;
            var title = (TitleBox.Text ?? "").Trim();
            if (string.IsNullOrEmpty(title))
            {
                MessageBox.Show(UiResources.Widget_EnterTitle, "PilotBim.Analytics");
                return;
            }

            int topN = 12;
            int.TryParse(GetSelectedId(TopNBox) ?? "12", out topN);
            int span = 2;
            int.TryParse(GetSelectedId(SpanBox) ?? "2", out span);

            Result = new DashboardWidgetState
            {
                Title = title,
                WidgetKind = kind,
                IsVisible = true,
                ColumnSpan = kind == DashboardWidgetKinds.Chart ? span : 2,
                ChartSource = GetSelectedId(SourceBox) ?? "Types",
                ChartKind = GetSelectedId(ChartKindBox) ?? "HorizontalBar",
                TopN = topN
            };

            if (kind == DashboardWidgetKinds.Kpi)
            {
                Result.Id = DashboardBlockIds.Kpi;
                Result.Title = string.IsNullOrWhiteSpace(title) ? "KPI / сводка" : title;
            }
            else if (kind == DashboardWidgetKinds.Bim)
            {
                Result.Id = DashboardBlockIds.Bim;
            }
            else if (kind == DashboardWidgetKinds.Responsible)
            {
                Result.Id = DashboardBlockIds.Responsible;
            }

            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
