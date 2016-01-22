﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Shapes;

namespace MyUWPToolkit
{
    [TemplatePart(Name = ImageCanvas, Type = typeof(Canvas))]
    [TemplatePart(Name = SourceImage, Type = typeof(Image))]
    [TemplatePart(Name = SelectRegion, Type = typeof(Path))]
    [TemplatePart(Name = TopLeftThumb, Type = typeof(Ellipse))]
    [TemplatePart(Name = TopRightThumb, Type = typeof(Ellipse))]
    [TemplatePart(Name = BottomLeftThumb, Type = typeof(Ellipse))]
    [TemplatePart(Name = BottomRightThumb, Type = typeof(Ellipse))]
    public class CropImageControl : Control
    {
        #region Fields
        private const string ImageCanvas = "imageCanvas";
        private const string SourceImage = "sourceImage";
        private const string SelectRegion = "selectRegion";
        private const string TopLeftThumb = "topLeftThumb";
        private const string TopRightThumb = "topRightThumb";
        private const string BottomLeftThumb = "bottomLeftThumb";
        private const string BottomRightThumb = "bottomRightThumb";

        private Canvas imageCanvas;
        private Image sourceImage;
        private Path selectRegion;
        private Ellipse topLeftThumb;
        private Ellipse topRightThumb;
        private Ellipse bottomLeftThumb;
        private Ellipse bottomRightThumb;

        private bool _isTemplateLoaded = false;

        private uint sourceImagePixelHeight;
        private uint sourceImagePixelWidth;
        /// <summary>
        /// The previous points of all the pointers.
        /// </summary>
        Dictionary<uint, Point?> pointerPositionHistory = new Dictionary<uint, Point?>();

        #endregion

        #region DependencyProperty

        public StorageFile SourceImageFile
        {
            get { return (StorageFile)GetValue(SourceImageFileProperty); }
            set { SetValue(SourceImageFileProperty, value); }
        }

        // Using a DependencyProperty as the backing store for SourceImageFile.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty SourceImageFileProperty =
            DependencyProperty.Register("SourceImageFile", typeof(StorageFile), typeof(CropImageControl), new PropertyMetadata(null, new PropertyChangedCallback(OnSourceImageFileChanged)));

        private static void OnSourceImageFileChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = d as CropImageControl;
            if (control != null)
            {
                control.OnSourceImageFileChanged();
            }
        }



        #endregion

        #region Property
        private CropSelection1 _cropSelection;

        public CropSelection1 CropSelection
        {
            get { return _cropSelection; }
            private set { _cropSelection = value; }
        }

        private AspectRatio _cropAspectRatio = AspectRatio.Square;

        public AspectRatio CropAspectRatio
        {
            get { return _cropAspectRatio; }
            set { _cropAspectRatio = value; }
        }

        private CropSelectionSize _defaultCropSelectionSize = CropSelectionSize.Half;

        public CropSelectionSize DefaultCropSelectionSize
        {
            get { return _defaultCropSelectionSize; }
            set { _defaultCropSelectionSize = value; }
        }


        public event CropImageSourceChangedEventHandler CropImageSourceChanged;
        #endregion


        public CropImageControl()
        {
            this.DefaultStyleKey = typeof(CropImageControl);
        }

        protected override void OnApplyTemplate()
        {
            imageCanvas = GetTemplateChild(ImageCanvas) as Canvas;
            sourceImage = GetTemplateChild(SourceImage) as Image;
            selectRegion = GetTemplateChild(SelectRegion) as Path;

            topLeftThumb = GetTemplateChild(TopLeftThumb) as Ellipse;
            topRightThumb = GetTemplateChild(TopRightThumb) as Ellipse;
            bottomLeftThumb = GetTemplateChild(BottomLeftThumb) as Ellipse;
            bottomRightThumb = GetTemplateChild(BottomRightThumb) as Ellipse;
            Initialize();

            AttachEvents();

            base.OnApplyTemplate();
            _isTemplateLoaded = true;
            OnSourceImageFileChanged();
        }



        #region Method

        private async void OnSourceImageFileChanged()
        {
            if (_isTemplateLoaded && SourceImageFile != null)
            {
                // Ensure the stream is disposed once the image is loaded
                using (IRandomAccessStream fileStream = await SourceImageFile.OpenAsync(Windows.Storage.FileAccessMode.Read))
                {
                    BitmapDecoder decoder = await BitmapDecoder.CreateAsync(fileStream);

                    this.sourceImagePixelHeight = decoder.PixelHeight;
                    this.sourceImagePixelWidth = decoder.PixelWidth;
                }

                if (this.sourceImagePixelHeight < 2 * 20 ||
                    this.sourceImagePixelWidth < 2 * 20)
                {

                }
                else
                {
                    double sourceImageScale = 1;

                    if (this.sourceImagePixelHeight < this.ActualHeight &&
                        this.sourceImagePixelWidth < this.ActualWidth)
                    {
                        this.sourceImage.Stretch = Windows.UI.Xaml.Media.Stretch.None;
                    }
                    else
                    {
                        sourceImageScale = Math.Min(this.ActualWidth / this.sourceImagePixelWidth,
                             this.ActualHeight / this.sourceImagePixelHeight);
                        this.sourceImage.Stretch = Windows.UI.Xaml.Media.Stretch.Uniform;
                    }

                    this.sourceImage.Source = await CropBitmapHelper.GetCroppedBitmapAsync(
                        this.SourceImageFile,
                        new Point(0, 0),
                        new Size(this.sourceImagePixelWidth, this.sourceImagePixelHeight),
                        sourceImageScale);
                }
            }
        }


        private void Initialize()
        {
            selectRegion.ManipulationMode = ManipulationModes.Scale |
            ManipulationModes.TranslateX | ManipulationModes.TranslateY;


            //Thumb width and height is 20.
            CropSelection = new CropSelection1 { MinSelectRegionSize = 2 * 20, CropAspectRatio = CropAspectRatio };
            imageCanvas.DataContext = CropSelection;
        }

        private void AttachEvents()
        {
            sourceImage.SizeChanged += SourceImage_SizeChanged;

            // Handle the pointer events of the corners. 
            AddThumbEvents(topLeftThumb);
            AddThumbEvents(topRightThumb);
            AddThumbEvents(bottomLeftThumb);
            AddThumbEvents(bottomRightThumb);

            // Handle the manipulation events of the selectRegion
            selectRegion.ManipulationDelta += selectRegion_ManipulationDelta;
            selectRegion.ManipulationCompleted += selectRegion_ManipulationCompleted;
        }

        private void selectRegion_ManipulationCompleted(object sender, ManipulationCompletedRoutedEventArgs e)
        {

        }

        private void selectRegion_ManipulationDelta(object sender, ManipulationDeltaRoutedEventArgs e)
        {
            var x = e.Delta.Translation.X;
            var y = e.Delta.Translation.Y;
            this.CropSelection.UpdateSelectedRect(e.Delta.Scale, x, y);
            e.Handled = true;
        }

        private void AddThumbEvents(Ellipse thumb)
        {
            thumb.PointerPressed += Thumb_PointerPressed;
            thumb.PointerMoved += Thumb_PointerMoved;
            thumb.PointerReleased += Thumb_PointerReleased;
        }

        private void Thumb_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            uint ptrId = e.GetCurrentPoint(this).PointerId;
            if (this.pointerPositionHistory.ContainsKey(ptrId))
            {
                this.pointerPositionHistory.Remove(ptrId);
            }

            (sender as UIElement).ReleasePointerCapture(e.Pointer);

            //event
            //GetCropImageSource();
            e.Handled = true;

        }


        private void Thumb_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            Windows.UI.Input.PointerPoint pt = e.GetCurrentPoint(this);
            uint ptrId = pt.PointerId;

            if (pointerPositionHistory.ContainsKey(ptrId) && pointerPositionHistory[ptrId].HasValue)
            {
                Point currentPosition = pt.Position;
                Point previousPosition = pointerPositionHistory[ptrId].Value;

                double xUpdate = currentPosition.X - previousPosition.X;
                double yUpdate = currentPosition.Y - previousPosition.Y;
                xUpdate = (int)xUpdate;
                yUpdate = (int)yUpdate;
                if (CropAspectRatio == AspectRatio.Square)
                {

                    if (sender == topLeftThumb || sender == bottomRightThumb)
                    {
                        if (Math.Abs(xUpdate) >= Math.Abs(yUpdate))
                        {
                            yUpdate = xUpdate;
                        }
                        else
                        {
                            xUpdate = yUpdate;
                        }
                    }
                    else
                    {
                        if (Math.Abs(xUpdate) >= Math.Abs(yUpdate))
                        {
                            yUpdate = -xUpdate;
                        }
                        else
                        {
                            xUpdate = -yUpdate ;
                        }
                    }
                    currentPosition = new Point() { X = previousPosition.X + xUpdate, Y = previousPosition.Y + yUpdate };
                    Debug.WriteLine((sender as Ellipse).Name + "----------" + xUpdate + ",,,," + yUpdate);
                }
                //todo
                //this.CropSelection.UpdateThumb((sender as Ellipse).Name as string, xUpdate, yUpdate);

                pointerPositionHistory[ptrId] = currentPosition;
            }

            e.Handled = true;
        }

        private void Thumb_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            (sender as UIElement).CapturePointer(e.Pointer);

            Windows.UI.Input.PointerPoint pt = e.GetCurrentPoint(this);

            // Record the start point of the pointer.
            pointerPositionHistory[pt.PointerId] = pt.Position;

            e.Handled = true;
        }

        private void SourceImage_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (e.NewSize.IsEmpty || double.IsNaN(e.NewSize.Height) || e.NewSize.Height <= 0)
            {
                this.imageCanvas.Visibility = Visibility.Collapsed;
                CropSelection.OuterRect = Rect.Empty;
                //todo
                // CropSelection.ResetThumb(0, 0, 0, 0);
                CropSelection.SelectedRect = new Rect(0, 0, 0, 0);
            }
            else
            {
                this.imageCanvas.Visibility = Visibility.Visible;

                this.imageCanvas.Height = e.NewSize.Height;
                this.imageCanvas.Width = e.NewSize.Width;
                CropSelection.OuterRect = new Rect(0, 0, e.NewSize.Width, e.NewSize.Height);

                if (e.PreviousSize.IsEmpty || double.IsNaN(e.PreviousSize.Height) || e.PreviousSize.Height <= 0)
                {
                    var rect = new Rect();
                    if (CropAspectRatio == AspectRatio.Custom)
                    {
                        rect.Width = e.NewSize.Width / (int)DefaultCropSelectionSize;
                        rect.Height = e.NewSize.Height / (int)DefaultCropSelectionSize;
                    }
                    else
                    {
                        var min = Math.Min(e.NewSize.Width, e.NewSize.Height);
                        rect.Width = rect.Height = min / (int)DefaultCropSelectionSize;
                    }

                    rect.X = (e.NewSize.Width - rect.Width) / (int)DefaultCropSelectionSize;
                    rect.Y = (e.NewSize.Height - rect.Height) / (int)DefaultCropSelectionSize;
                    //todo
                    //CropSelection.ResetThumb(rect.X, rect.Y, rect.X + rect.Width, rect.Y + rect.Height);
                    CropSelection.SelectedRect = rect;
                }
                else
                {
                    double scale = e.NewSize.Height / e.PreviousSize.Height;
                    //todo
                   // CropSelection.ResizeSelectedRect(scale);
                    // GetCropImageSource();
                }

            }

        }


        public ImageSource GetCropImageSource()
        {


            return null;
        }

        #endregion
    }
}