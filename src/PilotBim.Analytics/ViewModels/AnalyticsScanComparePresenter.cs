using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using PilotBim.Analytics.Models;
using PilotBim.Analytics.Services;

namespace PilotBim.Analytics.ViewModels
{
    /// <summary>
    /// Scan/compare/history orchestration extracted from AnalyticsWindowViewModel.
    /// Root VM keeps façade property names for XAML bindings.
    /// </summary>
    internal sealed class AnalyticsScanComparePresenter
    {
        private readonly ScanSnapshotStore _scanStore;
        private readonly ScanDiffService _scanDiff;
        private readonly Action<string> _notify;
        private StoredScanBaseline _currentScanBaseline;
        private ScanHistoryEntry _selectedScanHistory;
        private string _scanDiffHint = "Выполните «Обновить», затем можно сохранять именованные снимки и сравнивать с ними.";
        private bool _scanDiffChangesOnly;
        private readonly List<ScanDiffRow> _scanDiffAll = new List<ScanDiffRow>();

        public AnalyticsScanComparePresenter(Action<string> notifyPropertyChanged)
            : this(new ScanSnapshotStore(), new ScanDiffService(), notifyPropertyChanged)
        {
        }

        internal AnalyticsScanComparePresenter(
            ScanSnapshotStore scanStore,
            ScanDiffService scanDiff,
            Action<string> notifyPropertyChanged)
        {
            _scanStore = scanStore ?? new ScanSnapshotStore();
            _scanDiff = scanDiff ?? new ScanDiffService();
            _notify = notifyPropertyChanged ?? (_ => { });
            ScanDiff = new ObservableCollection<ScanDiffRow>();
            ScanHistory = new ObservableCollection<ScanHistoryEntry>();
        }

        public ObservableCollection<ScanDiffRow> ScanDiff { get; private set; }
        public ObservableCollection<ScanHistoryEntry> ScanHistory { get; private set; }

        public ScanHistoryEntry SelectedScanHistory
        {
            get { return _selectedScanHistory; }
            set
            {
                _selectedScanHistory = value;
                Notify(nameof(SelectedScanHistory));
            }
        }

        public string ScanDiffHint
        {
            get { return _scanDiffHint; }
            private set
            {
                _scanDiffHint = value;
                Notify(nameof(ScanDiffHint));
            }
        }

        public bool ScanDiffChangesOnly
        {
            get { return _scanDiffChangesOnly; }
            set
            {
                _scanDiffChangesOnly = value;
                Notify(nameof(ScanDiffChangesOnly));
                PublishScanDiffRows();
            }
        }

        public void ClearDisplayedDiff()
        {
            ScanDiff.Clear();
        }

        public void ReloadHistoryList()
        {
            var selectedId = _selectedScanHistory != null ? _selectedScanHistory.Id : null;
            ScanHistory.Clear();
            foreach (var e in _scanStore.ListHistory())
                ScanHistory.Add(e);

            if (selectedId != null)
                SelectedScanHistory = ScanHistory.FirstOrDefault(e => e.Id == selectedId);
        }

        public void RefreshOnSnapshot(ProjectAnalyticsSnapshot snapshot)
        {
            _scanDiffAll.Clear();
            ScanDiff.Clear();
            if (snapshot != null)
                snapshot.ScanDiffRows = new List<ScanDiffRow>();
            try
            {
                var current = _scanDiff.Capture(snapshot);
                _currentScanBaseline = current;

                var previousLast = _scanStore.TryLoadLast();
                StoredScanBaseline previous = null;
                string previousLabel = "предыдущий last-scan";

                if (_selectedScanHistory != null && !string.IsNullOrWhiteSpace(_selectedScanHistory.Id))
                {
                    previous = _scanStore.TryLoadById(_selectedScanHistory.Id);
                    if (previous != null)
                        previousLabel = _selectedScanHistory.DisplayTitle;
                }
                if (previous == null)
                {
                    previous = previousLast;
                    previousLabel = "предыдущий last-scan";
                }

                var rows = _scanDiff.Diff(previous, current);
                SetScanDiffRows(rows, snapshot);

                _scanStore.SaveLast(current);

                if (previousLast != null
                    && previousLast.GeneratedAt != current.GeneratedAt
                    && !HistoryHasGeneratedAt(previousLast.GeneratedAt))
                {
                    _scanStore.ArchiveToHistory(
                        previousLast,
                        "Авто · " + previousLast.GeneratedAt.ToString("yyyy-MM-dd HH:mm"));
                }

                ReloadHistoryList();
                ScanDiffHint = previous == null
                    ? "База сохранена. Следующее «Обновить» покажет diff. Можно сохранить именованный снимок."
                    : "Сравнение с: " + previousLabel;
            }
            catch (Exception ex)
            {
                SetScanDiffRows(new List<ScanDiffRow>
                {
                    new ScanDiffRow
                    {
                        Area = "Ошибка",
                        Metric = "Сравнение сканов",
                        Previous = "—",
                        Current = "—",
                        Delta = "n/a",
                        Notes = ex.Message
                    }
                }, snapshot);
                ScanDiffHint = "Ошибка сравнения: " + ex.Message;
            }
        }

        public void CompareWithSelectedHistory(ProjectAnalyticsSnapshot snapshot)
        {
            if (_currentScanBaseline == null && snapshot != null)
                _currentScanBaseline = _scanDiff.Capture(snapshot);

            if (_currentScanBaseline == null)
            {
                ScanDiffHint = "Нет текущего скана — нажмите «Обновить».";
                return;
            }

            if (_selectedScanHistory == null)
            {
                ScanDiffHint = "Выберите снимок в списке истории.";
                return;
            }

            var previous = _scanStore.TryLoadById(_selectedScanHistory.Id);
            var rows = _scanDiff.Diff(previous, _currentScanBaseline);
            SetScanDiffRows(rows, snapshot);

            ScanDiffHint = previous == null
                ? "Снимок не найден на диске."
                : "Сравнение с: " + _selectedScanHistory.DisplayTitle;
        }

        public bool SaveNamedScan(string name, ProjectAnalyticsSnapshot snapshot)
        {
            if (_currentScanBaseline == null && snapshot != null)
                _currentScanBaseline = _scanDiff.Capture(snapshot);

            if (_currentScanBaseline == null)
            {
                ScanDiffHint = "Нет текущего скана — нажмите «Обновить».";
                return false;
            }

            var id = _scanStore.SaveNamed(_currentScanBaseline, name);
            ReloadHistoryList();
            if (id != null)
            {
                SelectedScanHistory = ScanHistory.FirstOrDefault(e => e.Id == id);
                ScanDiffHint = "Сохранён снимок: " + (SelectedScanHistory != null ? SelectedScanHistory.DisplayTitle : name);
                return true;
            }

            ScanDiffHint = "Не удалось сохранить снимок.";
            return false;
        }

        public void DeleteSelectedHistory()
        {
            if (_selectedScanHistory == null)
                return;
            var id = _selectedScanHistory.Id;
            if (_scanStore.DeleteHistory(id))
            {
                SelectedScanHistory = null;
                ReloadHistoryList();
                ScanDiffHint = "Снимок удалён из истории.";
            }
        }

        internal static bool IsMeaningfulChange(ScanDiffRow row)
        {
            if (row == null)
                return false;
            if (row.Area == "Скан" && row.Metric == "Время скана")
                return true;
            if (row.Area == "Ошибка")
                return true;
            var d = row.Delta;
            if (string.IsNullOrWhiteSpace(d) || d == "0" || d == "n/a" || d == "+0")
                return false;
            return true;
        }

        private void SetScanDiffRows(List<ScanDiffRow> rows, ProjectAnalyticsSnapshot snapshot)
        {
            _scanDiffAll.Clear();
            if (rows != null)
                _scanDiffAll.AddRange(rows);

            if (snapshot != null)
            {
                snapshot.ScanDiffRows = new List<ScanDiffRow>();
                foreach (var row in _scanDiffAll)
                    snapshot.ScanDiffRows.Add(row);
            }

            PublishScanDiffRows();
        }

        private void PublishScanDiffRows()
        {
            ScanDiff.Clear();
            foreach (var row in _scanDiffAll)
            {
                if (_scanDiffChangesOnly && !IsMeaningfulChange(row))
                    continue;
                ScanDiff.Add(row);
            }
        }

        private bool HistoryHasGeneratedAt(DateTime generatedAt)
        {
            return _scanStore.ListHistory()
                .Any(e => e != null && e.GeneratedAt == generatedAt);
        }

        private void Notify(string propertyName)
        {
            _notify(propertyName);
        }
    }
}
