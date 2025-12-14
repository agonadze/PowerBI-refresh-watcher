using Microsoft.Extensions.Configuration;
using Microsoft.Win32;
using Newtonsoft.Json;
using pbiHelper;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;


namespace pbiWatcher
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {

        private bool isMonitoring = false;
        private bool isLoggedIn = false;
        private bool isDirty = false;
        private static cAppsettings pbiSettings = new();
        private cPbiHelper pbiHelper;

        private ObservableCollection<CDataObject> _dataObjects = new ObservableCollection<CDataObject>();
        private DispatcherTimer timer = new DispatcherTimer();
        private string sFilePath = "";
        private int iTimeZone = 0;


        public MainWindow()
        {
            InitializeComponent();
            var builder = new ConfigurationBuilder()
                .SetBasePath(System.AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
            IConfiguration config = builder.Build();

            config.GetSection("AppSettings").Bind(pbiSettings);            
            pbiHelper = new(pbiSettings);

            dataGrid.ItemsSource = _dataObjects;

            timer.Interval = TimeSpan.FromSeconds(pbiSettings.PollingIntervalSec);
            timer.Tick += Timer_Elapsed; ;
        }

        private async void NewMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (!isLoggedIn)
            {
                isLoggedIn = await pbiHelper.InitPowerBICLient().ConfigureAwait(true);
            }
            if (isLoggedIn)
            {
                _dataObjects.Clear();
                var wpbiobjects = new wPbiObjects(pbiHelper, _dataObjects);
                var res = wpbiobjects.ShowDialog();
                if (res == true)
                {
                    isDirty = true;
                    _dataObjects = wpbiobjects.DataObjects;
                    getRefreshStatus(_dataObjects);
                }
            }
            else
            {
                MessageBox.Show("Login failed", "Login", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            
        }

        private async void OpenMenuItem_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dlg = new OpenFileDialog()
            {
                Filter = "JSON Files(*.json)|*.json"
            };
            if (dlg.ShowDialog() == true)
            {
                if (!isLoggedIn)
                {
                    isLoggedIn = await pbiHelper.InitPowerBICLient().ConfigureAwait(true);
                }
                try
                {
                    CDataObject[] ddArray = JsonConvert.DeserializeObject<CDataObject[]>(File.ReadAllText(dlg.FileName));
                    _dataObjects = new ObservableCollection<CDataObject>(ddArray);
                    isDirty = false;
                    getRefreshStatus(_dataObjects);
                } catch (Exception ex)
                {
                    MessageBox.Show("Error parsing " + dlg.FileName + ". Error: " + ex.Message, "Error parsing file", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void SaveMenuItem_Click(object sender, RoutedEventArgs e)
        {
            SaveFile(sFilePath);
            isDirty = false;
        }

        private void SaveAsMenuItem_Click(object sender, RoutedEventArgs e)
        {
            SaveFile("");
            isDirty = false;
        }

        private void AboutMenuItem_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("PowerBI Refresh Watcher\nVersion 0.2\n\nWritten by Alex Gonadze, 2025\nagonadze@agdatapro.net", "About", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ExitMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (isDirty)
            {
                var res = MessageBox.Show("There are unsaved changes. Do you want to exit without saving?", "Unsaved Changes", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (res == MessageBoxResult.No)
                {
                    return;
                }
            }
            Close();
        }

        private async void bStartStop_Click(object sender, RoutedEventArgs e)
        {
                if ( pbiHelper.GetAccessToken() == null)
                {
                    var res = await pbiHelper.LoginInteractive().ConfigureAwait(true);
                    if (res == null) return;
                }
                if (isMonitoring == false)
                {

                    timer.Start();
                    bStartStop.Content = "Stop Monitoring";
                    isMonitoring = true;
                }
                else
                {
                    timer.Stop();
                    bStartStop.Content = "Start Monitoring";
                    isMonitoring = false;
                }
        }

        private void CloseMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (isDirty)
            {
                var res = MessageBox.Show("There are unsaved changes. Do you want to close without saving?", "Unsaved Changes", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (res == MessageBoxResult.No)
                {
                    return;
                }
            }
            _dataObjects.Clear();
            dataGrid.ItemsSource = _dataObjects;
            dataGrid.Items.Refresh();
        }
        
        private void Window_Closing(object sender, CancelEventArgs e)
        {
            if (isDirty)
            {
                var res = MessageBox.Show("There are unsaved changes. Do you want to exit without saving?", "Unsaved Changes", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (res == MessageBoxResult.No)
                {
                    e.Cancel = true;
                }
            }
        }

        private void rbTZlocal_Checked(object sender, RoutedEventArgs e)
        {
            iTimeZone = 0;
            foreach (CDataObject o in _dataObjects)
            {
                o._iTimeZone = 0;

            }
            if (dataGrid != null)
            {
                dataGrid.ItemsSource = _dataObjects;
                dataGrid.Items.Refresh();
            }
        }
        private void rbTZUtc_Checked(object sender, RoutedEventArgs e)
        {
            iTimeZone = 1;
            foreach (CDataObject o in _dataObjects)
            {
                o._iTimeZone = 1;

            }
            if (dataGrid != null)
            {
                dataGrid.ItemsSource = _dataObjects;
                dataGrid.Items.Refresh();
            }
        }

        private void dataGrid_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            var item = dataGrid.SelectedItem as CDataObject;

            switch (item.Status)
            {
                case "Failed":
                case "Success":
                case "Completed":
                case "N/A":
                    mnuCancelRefresh.IsEnabled = false;
                    mnuStartRefresh.IsEnabled = true;
                    break;
                case "InProgress":
                    mnuCancelRefresh.IsEnabled = true;
                    mnuStartRefresh.IsEnabled = false;
                    break;
                default:
                    mnuStartRefresh.IsEnabled = false;
                    mnuCancelRefresh.IsEnabled = false;
                    break;
            }
        }

        private void Timer_Elapsed(object sender, EventArgs e)
        {
            getRefreshStatus(_dataObjects);
        }

        private async void getRefreshStatus(ObservableCollection<CDataObject> dataObjects)
        {

            RefreshData? refreshData = null;
            foreach (CDataObject pd in dataObjects)
            {
                if (pd.objectType == "dataset")
                {
                    refreshData = await pbiHelper.GetRefreshStatus("dataset", pd.WorkspaceId.ToString(), pd.DataObjectId); 
                }
                else if (pd.objectType == "dataflow")
                {
                    refreshData = await pbiHelper.GetRefreshStatus("dataflow", pd.WorkspaceId.ToString(), pd.DataObjectId); 
                }
                if (refreshData == null)
                {
                    pd.Status = "N/A";
                    pd.RefreshEndUTC = null;
                    pd.RefreshStartUTC = null;
                }
                else
                {
                    pd.Status = refreshData.Status;
                    if (new DateTime(1001, 1, 1) == refreshData.EndTime)
                    {
                        pd.RefreshEndUTC = null;
                    }
                    else
                    {
                        pd.RefreshEndUTC = refreshData.EndTime;
                    }
                    pd.RefreshStartUTC = refreshData.StartTime;
                }

                pd._iTimeZone = iTimeZone;
                //dataObjects.Add(pd);
            }
            dataGrid.ItemsSource = dataObjects;
            dataGrid.Items.Refresh();
            
        }
        private void SaveFile(string sPath)
        {
            if (sPath == "")
            {
                SaveFileDialog dlg = new SaveFileDialog()
                {
                    Filter = "JSON Files(*.json)|*.json"
                };
                if (dlg.ShowDialog() == true)
                {
                    sPath = dlg.FileName;
                }
                else
                {
                    return;
                }
            }

            string s = Newtonsoft.Json.JsonConvert.SerializeObject(_dataObjects);
            File.WriteAllText(sPath, s);
            sFilePath = sPath;
        }

        private async void Refresh_Click(object sender, RoutedEventArgs e)
        {
            CDataObject o = dataGrid.SelectedItem as CDataObject;
            bool res = false;
            switch (o.objectType)
            {
                case "dataset":
                    res = await pbiHelper.RefreshDatasetinGroup(o.WorkspaceId.ToString(), o.DataObjectId); //.ConfigureAwait(true);
                    break;
                case "dataflow":
                    res = await pbiHelper.RefreshDataflow(o.WorkspaceId.ToString(), o.DataObjectId); //.ConfigureAwait(true);
                    break;
                default:
                    break;
            }
            if (res)
            {
                MessageBox.Show("Refresh has been triggered for " + o.DataObject, "Refresh", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show("Error triggering refresh for " + o.DataObject, "Refresh", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void Cancel_Click(object sender, RoutedEventArgs e)
        {
            CDataObject o = dataGrid.SelectedItem as CDataObject;
            bool res = false;
            DataTable rh;
            switch (o.objectType)
            {
                case "dataset":
                    rh = await pbiHelper.GetDatasetRefreshHistory(o.WorkspaceId.ToString(), o.DataObjectId);
                    if (rh == null)
                    {
                        MessageBox.Show("Cannot find a running refresh process for " + o.DataObject, "Cannot cancel", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                    string sRefreshId = rh.Rows[0]["id"].ToString();
                    res = await pbiHelper.RefreshDatasetinGroup(o.WorkspaceId.ToString(), o.DataObjectId); //.ConfigureAwait(true);
                    break;
                case "dataflow":
                    rh = await pbiHelper.GetDataflowRefreshHistory(o.WorkspaceId.ToString(), o.DataObjectId);
                    if (rh == null)
                    {
                        MessageBox.Show("Cannot find a running refresh process for " + o.DataObject, "Cannot cancel", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                    string sTransactionId = rh.Rows[0]["id"].ToString();
                    res = await pbiHelper.CancelDataflowRefresh(o.WorkspaceId.ToString(), sTransactionId); //.ConfigureAwait(true);
                    break;
                default:
                    break;
            }
            if (res)
            {
                MessageBox.Show("Cancel command has been triggered for " + o.DataObject, "Refresh", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show("Error triggering cancel refresh command for " + o.DataObject, "Refresh", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void bEdit_Click(object sender, RoutedEventArgs e)
        {
            if (!isLoggedIn)
            {
                isLoggedIn = await pbiHelper.InitPowerBICLient().ConfigureAwait(true);
            }
            if (isLoggedIn)
            {
                var wpbiobjects = new wPbiObjects(pbiHelper, _dataObjects);
                var res = wpbiobjects.ShowDialog();
                
                if (res == true)
                {
                    _dataObjects = wpbiobjects.DataObjects;
                    isDirty = wpbiobjects.IsDirty;
                    getRefreshStatus(_dataObjects);
                }
            }
            else
            {
                MessageBox.Show("Login failed", "Login", MessageBoxButton.OK, MessageBoxImage.Error);
            }

        }

        private async void dataGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // Ensure a row is actually double-clicked, not just empty space
            if (sender is DataGrid dataGrid && dataGrid.SelectedItem != null)
            {
                ShowHistory();
            }
        }

        private void ShowHistory_Click(object sender, RoutedEventArgs e)
        {
            ShowHistory();
        }

        private async void ShowHistory()
        {
            var selectedItem = dataGrid.SelectedItem as CDataObject;
            DataTable? rh;
            switch (selectedItem.objectType)
            {
                case "dataset":
                    rh = await pbiHelper.GetDatasetRefreshHistory(selectedItem.WorkspaceId.ToString(), selectedItem.DataObjectId);
                    if (rh == null)
                    {
                        MessageBox.Show("Cannot find refresh history for " + selectedItem.DataObject, "Refresh History", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                    foreach (DataRow row in rh.Rows)
                    {
                        if (row["StartTime"] != DBNull.Value)
                        {
                            row["StartTime"] = ((DateTime)row["StartTime"]).ToLocalTime();
                        }
                        if (row["EndTime"] != DBNull.Value)
                        {
                            row["EndTime"] = ((DateTime)row["EndTime"]).ToLocalTime();
                        }
                    }
                    break;
                case "dataflow":
                    rh = await pbiHelper.GetDataflowRefreshHistory(selectedItem.WorkspaceId.ToString(), selectedItem.DataObjectId);
                    if (rh == null)
                    {
                        MessageBox.Show("Cannot find refresh history for " + selectedItem.DataObject, "Refresh History", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                    break;
                default:
                    rh = null;
                    break;
            }
            if (rh == null)
            {
                MessageBox.Show("Error retrieving refresh history for " + selectedItem.DataObject, "Refresh History", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            wRefreshHist wrh = new wRefreshHist(selectedItem.objectType, selectedItem.DataObject, rh);
            wrh.ShowDialog();
        }
    }

    public class CDataObject : INotifyPropertyChanged
    {
        public string? Workspace { get; set; }
        public Guid WorkspaceId { get; set; }
        public bool isPremium { get; set; }

        public string? objectType { get; set; }
        public string? DataObject { get; set; }
        public string? DataObjectId { get; set; }

        [JsonIgnore]
        public string imgUri
        {
            get
            {
                if (objectType == "dataset")
                {
                    return "pack://application:,,/images/Dataset.png";
                }
                else if (objectType == "dataflow")
                {
                    return "pack://application:,,/images/Dataflow.png";
                }
                else
                {
                    return "";
                }
            }
        }

        [JsonIgnore]
        public string imgPremUri
        {
            get
            {
                if (isPremium)
                {
                    return "pack://application:,,/images/Premium.png";
                }
                else
                {
                    return "pack://application:,,/images/blank.png";
                }
            }
        }

        [JsonIgnore]
        public int _iTimeZone = 0;

        [JsonIgnore]
        public string? Status { get; set; }

        [JsonIgnore]
        public string? ErrorMessage { get; set; }

        [JsonIgnore]
        public DateTime? RefreshStartUTC { get; set; }

        [JsonIgnore]
        public DateTime? RefreshEndUTC { get; set; }

        [JsonIgnore]
        public DateTime? RefreshStart
        {
            get
            {
                DateTime? val = null;

                if (RefreshStartUTC == null) return null;

                if (this.objectType == "dataset")
                {
                    if (_iTimeZone == 0)
                    {
                        val = RefreshStartUTC.Value.ToLocalTime();
                    }
                    else
                    {
                        val = RefreshStartUTC;
                    }
                }
                else if (this.objectType == "dataflow")
                {
                    if (_iTimeZone == 0)
                    {
                        val = RefreshStartUTC;
                    }
                    else
                    {
                        val = RefreshStartUTC.Value.ToUniversalTime();
                    }
                }
                else
                {
                    val = null;
                }
                return val;
            }
        }

        [JsonIgnore]
        public DateTime? RefreshEnd
        {
            get
            {
                DateTime? val = null;

                if (RefreshEndUTC == null) return null;

                if (this.objectType == "dataset")
                {
                    if (_iTimeZone == 0)
                    {
                        val = RefreshEndUTC.Value.ToLocalTime();
                    }
                    else
                    {
                        val = RefreshEndUTC;
                    }
                }
                else if (this.objectType == "dataflow")
                {
                    if (_iTimeZone == 0)
                    {
                        val = RefreshEndUTC;
                    }
                    else
                    {
                        val = RefreshEndUTC.Value.ToUniversalTime();
                    }
                }
                else
                {
                    val = null;
                }
                return val;
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void NotifyPropertyChanged([CallerMemberName] String propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

}