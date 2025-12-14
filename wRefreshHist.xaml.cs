using System.Data;
using System.Windows;

namespace pbiWatcher
{
    /// <summary>
    /// Interaction logic for wRefreshHist.xaml
    /// </summary>
    public partial class wRefreshHist : Window
    {
        DataTable? refreshHist;
        public wRefreshHist(string? obType, string? obName, DataTable? rh)
        {
            InitializeComponent();
            lblTitle.Text = $"Refresh History for {obType} '{obName}'";
            refreshHist = rh;
        }

        private void rbTZUtc_Checked(object sender, RoutedEventArgs e)
        {
            if (dgHist == null) return;

            foreach (DataRowView r in dgHist.Items)
            {                
                if (r != null)
                {
                    DateTime dtLocal = (DateTime)r["StartTime"];
                    DateTime dtUtc = dtLocal.ToUniversalTime();
                    r["StartTime"] = dtUtc;
                    dtLocal = (DateTime)r["EndTime"];
                    dtUtc = dtLocal.ToUniversalTime();
                    r["EndTime"] = dtUtc;
                }
            }
        }

        private void rbTZlocal_Checked(object sender, RoutedEventArgs e)
        {
            if (dgHist == null) return;

            foreach (DataRowView r in dgHist.Items)
            {
                if (r != null)
                {
                    DateTime dtLocal = (DateTime)r["StartTime"];
                    DateTime dtUtc = dtLocal.ToLocalTime();
                    r["StartTime"] = dtUtc;
                    dtLocal = (DateTime)r["EndTime"];
                    dtUtc = dtLocal.ToLocalTime();
                    r["EndTime"] = dtUtc;
                }
            }
        }

        private void dgHist_Loaded(object sender, RoutedEventArgs e)
        {
            dgHist.ItemsSource = refreshHist.DefaultView;
            dgHist.Items.Refresh();
        }
    }
}
