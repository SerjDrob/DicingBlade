using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace DicingBlade.UserControls
{
    /// <summary>
    /// Interaction logic for Indicator.xaml
    /// </summary>
    public partial class Indicator : UserControl
    {
        public Indicator()
        {           
            InitializeComponent();
            TheLevel.DataContext = this;
        }


        public double MinLevel
        {
            get { return (double)GetValue(MinLevelProperty); }
            set { SetValue(MinLevelProperty, value); }
        }

        // Using a DependencyProperty as the backing store for MinLevel.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty MinLevelProperty =
            DependencyProperty.Register("MinLevel", typeof(double), typeof(Indicator), new PropertyMetadata(default(double)));


        public double MaxLevel
        {
            get { return (double)GetValue(MaxLevelProperty); }
            set { SetValue(MaxLevelProperty, value); }
        }

        // Using a DependencyProperty as the backing store for MaxLevel.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty MaxLevelProperty =
            DependencyProperty.Register("MaxLevel", typeof(double), typeof(Indicator), new PropertyMetadata((double)100));


        public double NoLessLevel
        {
            get { return (double)GetValue(NoLessLevelProperty); }
            set { SetValue(NoLessLevelProperty, value); }
        }

        // Using a DependencyProperty as the backing store for NoLessLevel.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty NoLessLevelProperty =
            DependencyProperty.Register("NoLessLevel", typeof(double), typeof(Indicator), new PropertyMetadata((double)50));



        public double CurrentLevel
        {
            get { return (double)GetValue(CurrentLevelProperty); }
            set { SetValue(CurrentLevelProperty, value); }
        }

        // Using a DependencyProperty as the backing store for CurrentLevel.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty CurrentLevelProperty =
            DependencyProperty.Register("CurrentLevel", typeof(double), typeof(Indicator), new PropertyMetadata(default(double)));



        public string Units
        {
            get { return (string)GetValue(UnitsProperty); }
            set { SetValue(UnitsProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Units.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty UnitsProperty =
            DependencyProperty.Register("Units", typeof(string), typeof(Indicator), new PropertyMetadata(String.Empty));




    }
}
