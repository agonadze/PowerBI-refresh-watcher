using Microsoft.PowerBI.Api.Models;
using pbiHelper;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace pbiWatcher
{
    /// <summary>
    /// Interaction logic for wPbiObjects.xaml
    /// </summary>
    public partial class wPbiObjects : Window
    {
        private cPbiHelper _pbiHelper;
        public ObservableCollection<CDataObject> DataObjects;
        public bool IsDirty = false;
        public wPbiObjects(cPbiHelper helper, ObservableCollection<CDataObject> dataObjects)
        {
            InitializeComponent();
            _pbiHelper = helper;
            DataObjects = new ObservableCollection<CDataObject>(dataObjects);
            this.DG.ItemsSource = DataObjects;
        }

        private void bUp_Click(object sender, RoutedEventArgs e)
        {
            int i = DG.SelectedIndex;
            if (i < 1) return;

            DataObjects.Move(i, i - 1);
            IsDirty = true;
        }

        private void bDown_Click(object sender, RoutedEventArgs e)
        {
            int i = DG.SelectedIndex;
            if (i < 0 || i == DG.Items.Count - 1) return;

            DataObjects.Move(i, i + 1);

            IsDirty = true;
        }

        private void bAdd_Click(object sender, RoutedEventArgs e)
        {
            if (tvWorkspaces.SelectedItem != null)
            {
                var o = ((TreeViewItem)tvWorkspaces.SelectedItem).Tag;
                if (o is Group) return;

                addToCollection((CDataObject)o);
                IsDirty = true;
            }
        }

        private void bRemove_Click(object sender, RoutedEventArgs e)
        {
            if (DG.SelectedItems.Count > 0)
            {
                DataObjects.Remove((CDataObject)DG.SelectedItem);
                IsDirty = true;
            }
        }


        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {

            Groups grps = await _pbiHelper.GetWorkspaces().ConfigureAwait(true);
            List<Group>groups = grps.Value.ToList<Group>().OrderBy(o => o.Name).ToList();
            TreeViewItem rootItem = new TreeViewItem();
            rootItem.Header = "All Workspaces";


            tvWorkspaces.Items.Add(rootItem);
            foreach (Group group in groups)
            {
                TreeViewItem item = new TreeViewItem();
                StackPanel sp = new StackPanel();
                sp.Orientation = Orientation.Horizontal;
                Image imgp = new Image();
                if (group.IsOnDedicatedCapacity == true)
                {
                    imgp.Source = new BitmapImage(new Uri("pack://application:,,/Images/FolderP.png"));
                }
                else
                {
                    imgp.Source = new BitmapImage(new Uri("pack://application:,,/Images/Folder.png"));
                }
                imgp.Width = 16;
                imgp.Height = 16;
                
                Label lbl = new Label();
                lbl.Content = group.Name;
                sp.Children.Add(imgp);
                sp.Children.Add(lbl);
                item.Header = sp;
                item.Tag = group;
                rootItem.Items.Add(item);
            }
        }

        private async void expandFolder()
        {
            TreeViewItem item = tvWorkspaces.SelectedItem as TreeViewItem;
            if (item.Tag is Group)
            {
                int i = 0;
                if (item.Items.Count > 0) return;

                List<CDataObject> list = new List<CDataObject>();

                Group g = (Group)item.Tag;
                Datasets d = await _pbiHelper.GetDatasets(g.Id);
                foreach (Dataset ditem in d.Value)
                {
                    CDataObject o = new CDataObject();
                    o.WorkspaceId = g.Id;
                    o.Workspace = g.Name;
                    o.DataObjectId = ditem.Id;
                    o.DataObject = ditem.Name;
                    o.objectType = "dataset";
                    o.isPremium = (bool)g.IsOnDedicatedCapacity;
                    list.Add(o);
                }

                Dataflows dataflows = await _pbiHelper.GetDataflows(g.Id);
                foreach (Dataflow f in dataflows.Value)
                {
                    CDataObject o = new CDataObject();
                    o.WorkspaceId = g.Id;
                    o.Workspace = g.Name;
                    o.DataObjectId = f.ObjectId.ToString();
                    o.DataObject = f.Name;
                    o.objectType = "dataflow";
                    o.isPremium = (bool)g.IsOnDedicatedCapacity;
                    list.Add(o);
                }

                list = list.OrderBy(x => x.DataObject).ToList<CDataObject>();

                foreach (CDataObject o in list)
                {
                    TreeViewItem ti = new TreeViewItem();
                    StackPanel sp = new StackPanel();
                    sp.Orientation = Orientation.Horizontal;
                    Image imgp = new Image();
                    if (o.objectType == "dataset")
                    {
                        imgp.Source = new BitmapImage(new Uri("pack://application:,,/images/Dataset.png"));
                    }
                    else
                    {
                        imgp.Source = new BitmapImage(new Uri("pack://application:,,/images/Dataflow.png"));
                    }
                    imgp.Width = 14;
                    imgp.Height = 14;
                    Label lbl = new Label();
                    lbl.Content = o.DataObject;
                    sp.Children.Add(imgp);
                    sp.Children.Add(lbl);
                    ti.Header = sp;
                    ti.Tag = o;
                    item.Items.Add(ti);
                }
                i = 0;
            }
        }

        private void tvWorkspaces_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            {
                if (tvWorkspaces.SelectedItem != null)
                {
                    var o = ((TreeViewItem)tvWorkspaces.SelectedItem).Tag;

                    if (o == null || o is Group)
                    {
                        expandFolder();
                    }
                    else
                    {
                        CDataObject d = (CDataObject)o;
                        addToCollection(d);
                    }
                }

            }
        }
        private void addToCollection(CDataObject d)
        {
            foreach (var ob in DataObjects.ToList()) 
            {
                if (d.DataObjectId == ob.DataObjectId)
                {
                    DataObjects.Remove(ob);
                    DataObjects.Add(d);
                    DataObjects.OrderBy(o => o.DataObject);
                    return;
                }
            }
            DataObjects.Add(d);
        }

        private void bRemoveAll_Click(object sender, RoutedEventArgs e)
        {
            DataObjects.Clear();
            IsDirty = true;
        }

        private void bCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            IsDirty = false;
            this.Close();
        }

        private void bApply_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            IsDirty = true;
            this.Close();
        }
    }
}
