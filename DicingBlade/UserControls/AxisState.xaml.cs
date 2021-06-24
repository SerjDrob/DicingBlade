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
using PropertyChanged;
using System.Windows.Media;

namespace DicingBlade.UserControls
{
    /// <summary>
    /// Interaction logic for AxisState.xaml
    /// </summary>
    [AddINotifyPropertyChangedInterface]
    public partial class AxisState : UserControl
    {
        public AxisState()
        {
            InitializeComponent();
            TheAxis.DataContext= this;
        }


        public double Coordinate
        {
            get { return (double)GetValue(CoordinateProperty); }
            set { SetValue(CoordinateProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Coordinate.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty CoordinateProperty =
            DependencyProperty.Register("Coordinate", typeof(double), typeof(AxisState), new PropertyMetadata(default(double)));


        public string CoordinateName
        {
            get { return (string)GetValue(CoordinateNameProperty); }
            set { SetValue(CoordinateNameProperty, value); }
        }

        // Using a DependencyProperty as the backing store for CoordinateName.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty CoordinateNameProperty =
            DependencyProperty.Register("CoordinateName", typeof(string), typeof(AxisState), new PropertyMetadata(String.Empty));



        public bool LmtNeg
        {
            get { return (bool)GetValue(LmtNegProperty); }
            set { SetValue(LmtNegProperty, value); }
        }

        // Using a DependencyProperty as the backing store for LmtNeg.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty LmtNegProperty =
            DependencyProperty.Register("LmtNeg", typeof(bool), typeof(AxisState), new PropertyMetadata(false));


        public bool LmtPos
        {
            get { return (bool)GetValue(LmtPosProperty); }
            set { SetValue(LmtPosProperty, value); }
        }

        // Using a DependencyProperty as the backing store for LmtPos.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty LmtPosProperty =
            DependencyProperty.Register("LmtPos", typeof(bool), typeof(AxisState), new PropertyMetadata(false));



        public bool MotionDone
        {
            get { return (bool)GetValue(MotionDoneProperty); }
            set { SetValue(MotionDoneProperty, value); }
        }

        // Using a DependencyProperty as the backing store for MotionDone.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty MotionDoneProperty =
            DependencyProperty.Register("MotionDone", typeof(bool), typeof(AxisState), new PropertyMetadata(true));



        public Brush NegColor
        {
            get { return (Brush)GetValue(NegColorProperty); }
            set { SetValue(NegColorProperty, value); }
        }

        // Using a DependencyProperty as the backing store for NegColor.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty NegColorProperty =
            DependencyProperty.Register("NegColor", typeof(Brush), typeof(AxisState), new PropertyMetadata(Brushes.Red));


        public Brush PosColor
        {
            get { return (Brush)GetValue(PosColorProperty); }
            set { SetValue(PosColorProperty, value); }
        }

        // Using a DependencyProperty as the backing store for PosColor.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty PosColorProperty =
            DependencyProperty.Register("PosColor", typeof(Brush), typeof(AxisState), new PropertyMetadata(Brushes.Green));



        public Brush TextBackground
        {
            get { return (Brush)GetValue(TextBackgroundProperty); }
            set { SetValue(TextBackgroundProperty, value); }
        }

        // Using a DependencyProperty as the backing store for TextBackGround.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty TextBackgroundProperty =
            DependencyProperty.Register("TextBackground", typeof(Brush), typeof(AxisState), new PropertyMetadata(Brushes.AliceBlue));






    }
}
