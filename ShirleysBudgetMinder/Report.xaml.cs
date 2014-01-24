using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace ShirleysBudgetMinder
{
    /// <summary>
    /// Interaction logic for Report.xaml
    /// </summary>
    public partial class Report : Window
    {
        public Report()
        {
            InitializeComponent();
        }

        private void btnReportOK_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void btnPrintReport_Click(object sender, RoutedEventArgs e)
        {
            PrintDialog dlg = new PrintDialog();
            btnPrintReport.Visibility = Visibility.Hidden;
            //label1.Visibility = Visibility.Hidden;
            this.Background = Brushes.White;
            //  cnv.Background = Brushes.White;
            //scroll.Visibility = Visibility.Visible;
            //panel.Visibility = Visibility.Hidden;
            //dgorder.Background = Brushes.White;
            this.Width = 700;
            //this.Margin = new Thickness(100.0);

            scrollReport.ScrollToTop();

            if (dlg.ShowDialog() == true)
            {
                dlg.PrintVisual(scrollReport.Content as Visual, "Print Button");
            }
        }
    }
}
