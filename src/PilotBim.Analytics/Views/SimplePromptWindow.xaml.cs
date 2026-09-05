using System.Windows;

namespace PilotBim.Analytics.Views
{
    public partial class SimplePromptWindow : Window
    {
        public SimplePromptWindow(string title, string prompt, string defaultValue)
        {
            InitializeComponent();
            Title = title ?? "PilotBim.Analytics";
            PromptText.Text = prompt ?? "";
            ValueBox.Text = defaultValue ?? "";
            ValueBox.SelectAll();
            Loaded += (s, e) => ValueBox.Focus();
        }

        public string ResultText { get; private set; }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            ResultText = ValueBox.Text;
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
