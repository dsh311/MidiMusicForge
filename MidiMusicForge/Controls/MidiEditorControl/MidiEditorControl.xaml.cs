/*
 * Copyright (C) 2025 David S. Shelley <davidsmithshelley@gmail.com>
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License 
 * along with this program. If not, see <https://www.gnu.org/licenses/>.
 */

using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using Melanchall.DryWetMidi.Multimedia;
using Melanchall.DryWetMidi.MusicTheory;
using MidiMusicForge.Controls.MidiEditorControl.MidiEditorInterfaces;
using MidiMusicForge.Controls.MidiEditorControl.MidiEditorState;
using MidiMusicForge.Controls.MidiEditorControl.MidiEditorTools;
using MidiMusicForge.Controls.MidiEditorControl.MidiEditorUtils;
using MidiMusicForge.Interfaces;
using NAudio.CoreAudioApi;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;


namespace MidiMusicForge
{
    /// <summary>
    /// Interaction logic for Toolbar.xaml
    /// </summary>
    public partial class MidiEditorControl : UserControl, IMidiEditControl, INotifyPropertyChanged
    {
        public event EventHandler OpenFileClicked;
        public event EventHandler SaveFileClicked;

        public event Action<bool> GridToggleClicked;
        public event Action<bool> CheckerboardToggleClicked;
        public event Action<double> ZoomSliderChanged;

        public IMidiEditControl? SourceTileControl { get; set; }
        public IMidiEditControl? DestinationTileControl { get; set; }

        public string TilesetPath { get; set; }
        public Rectangle? SelectionRect { get; set; }

        public WriteableBitmap? SelectionAsBitmap { get; set; }

        public int SelectedTilesetRowStart { get; set; }
        public int SelectedTilesetColumnStart { get; set; }
        public int SelectedTilesetRowEnd { get; set; }
        public int SelectedTilesetColumnEnd { get; set; }

        public int SelectedTilesetRowMouseDownOffset { get; set; }
        public int SelectedTilesetColumnMouseDownOffset { get; set; }

        //Tools
        public IPaintTool? TheTool { get; set; }
        public IPaintTool? pencilTool { get; set; }
        public IPaintTool? selectTool { get; set; }
        public Point? MouseDownLastLoc { get; set; }
        public Point MouseOverLocation { get; set; }
        public int MouseOverGridRow { get; set; }
        public int MouseOverGridCol { get; set; }


        public int ImageWidth { get; set; }
        public int ImageHeight { get; set; }
        public int GridDimention { get; set; }

        public EditorCell[,] TileMapArray { get; set; }

        public Image? InputImage { get; set; }
        public Image? InputImagePreview { get; set; }
        public Image? OutputImage { get; set; }

        public bool IsDragging { get; set; }
        public bool IsDraggingSelection { get; set; }

        public bool IsUserDraggingSlider = false;

        private SolidColorBrush selectedBrush;
        public UndoRedoManager undoRedoManager;
        private DateTime _lastUndoTime = DateTime.MinValue;
        private TimeSpan _undoCooldown = TimeSpan.FromMilliseconds(100);

        private bool _tileSetImageInitialized = false;
        private bool _tileSetImagePreviewInitialized = false;
        private bool _overlayTilesetGridInitialized = false;
        private bool _checkerboardBackgroundInitialized = false;

        public bool MouseIsDownOnPlayLine { get; set; }
        private Stopwatch timer = new Stopwatch();

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));


        private double _currentTimeInSeconds;
        private double _songDurationInSeconds;
        public double CurrentTimeInSeconds
        {
            get => _currentTimeInSeconds;
            set
            {
                if (_currentTimeInSeconds != value)
                {
                    _currentTimeInSeconds = value;
                    OnPropertyChanged(nameof(CurrentTimeInSeconds));
                }
            }
        }

        public double SongDurationInSeconds
        {
            get => _songDurationInSeconds;
            set
            {
                if (_songDurationInSeconds != value)
                {
                    _songDurationInSeconds = value;
                    OnPropertyChanged(nameof(SongDurationInSeconds));
                }
            }
        }

        MidiFile MidiFileLoaded;
        TempoMap MidiFileTempoMapLoaded;

        private OutputDevice outputDevice;
        private Playback playback;

        private bool isPlaying = false;
        private bool isPaused = false;

        string lastFileDir = "";

        DispatcherTimer playbackTimer;
        Stopwatch playbackStopwatch;

        public enum ToolMode
        {
            Select,
            Pencil,
            None
        }

        ToolMode selectedTool;

        public void ClearFilePath()
        {
            TilesetPath = "";
            tilesetFileNameTxtBox.Visibility = Visibility.Collapsed;
            tilesetFileNameTxtBox.Text = "";
            tilesetFileNameTxtBox.ScrollToHorizontalOffset(double.MaxValue);
        }
        public void RefreshLayers()
        {
            // REDRAW the grid and checkerboard of the source
            GraphicsUtils.DrawGridOnCanvas(overlayTilesetGrid,
                TileSetImage.Source.Width,
                TileSetImage.Source.Height,
                GridDimention,
                System.Windows.Media.Brushes.Gray,
                0.5);

            // Redraw Checkerboard
            CheckerboardBackground.Height = TileSetImage.Source.Height;
            CheckerboardBackground.Width = TileSetImage.Source.Width;
            GraphicsUtils.DrawCheckerboard(CheckerboardBackground, GridDimention);

            // Ensure image dimensions are updated
            int pixelWidth = (int)TileSetImage.Source.Width;
            int pixelHeight = (int)TileSetImage.Source.Height;
            imgDimensions.Text = pixelWidth + " x " + pixelHeight;
        }
        public void deselectToolButtons()
        {
            //Clear all button selections
            // Add back tool bar
            /*
            selectBtn.BorderBrush = Brushes.Transparent;
            selectBtn.BorderThickness = new Thickness(0);

            pencilBtn.BorderBrush = Brushes.Transparent;
            pencilBtn.BorderThickness = new Thickness(0);
            */
        }

        public MidiEditorControl()
        {
            InitializeComponent();

            selectedTool = ToolMode.None;
            timer.Start();

            this.DataContext = this;

            MainGrid.Focus();

            IsDragging = false;
            IsDraggingSelection = false;

            SelectedTilesetRowStart = -1;
            SelectedTilesetColumnStart = -1;
            SelectedTilesetRowEnd = -1;
            SelectedTilesetColumnEnd = -1;
            // Keep track of click inside of a selection
            SelectedTilesetRowMouseDownOffset = 0;
            SelectedTilesetColumnMouseDownOffset = 0;

            TilesetPath = "";
            SelectionRect = null;
            SelectionAsBitmap = null;
            GridDimention = 4;

            MouseDownLastLoc = null;

            pencilTool = new PencilTool();
            selectTool = new SelectTool();
            if (selectTool is SelectTool concreteSelectTool)
            {
                concreteSelectTool.useThisGridDimension = GridDimention;
            }

            selectedBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 0, 0));

            selectedTool = ToolMode.None;
            TheTool = selectTool;

            TileSetImage.Loaded += (s, e) =>
            {
                if (!_tileSetImageInitialized)
                {
                    //Ensure we are always working with a WritableBitmap
                    //TileSetImage is not a WriteableBitmap, but instead a BitmapFrameDecode
                    //which is what WPF creates when you load an image from a file
                    GraphicsUtils.transparentImage(TileSetImage);

                    //TileSetImage.Source.Width and .Height give you device-independent units (DIPs), not actual pixels.
                    var bmpSource = TileSetImage.Source as BitmapSource;
                    if (bmpSource != null)
                    {
                        ImageWidth = bmpSource.PixelWidth;
                        ImageHeight = bmpSource.PixelHeight;

                        imgDimensions.Text = ImageWidth + " x " + ImageHeight;
                    }

                    // Create empty array that represends the Tile map board on the right side of the UI
                    int neededRows = 512 / GridDimention;
                    int neededCols = 512 / GridDimention;
                    TileMapArray = CellUtils.CreateEmptyTileMapArray(neededRows, neededCols);

                    // Save the current state of the TileSetImage
                    if (undoRedoManager == null)
                    {
                        undoRedoManager = new UndoRedoManager(); // Create the undo redo manager

                        //Ensure the current state is saved since the UndoRedoManager requires knowing the current state
                        WriteableBitmap currentImage = new WriteableBitmap((BitmapSource)TileSetImage.Source);
                        EditorState currentState = new EditorState(currentImage, TileMapArray);
                        undoRedoManager.SaveState(currentState);
                    }
                }
                _tileSetImageInitialized = true;
            };

            TileSetImagePreview.Loaded += (s, e) =>
            {
                if (!_tileSetImagePreviewInitialized)
                {
                    //Ensure we are always working with a WritableBitmap
                    //TileSetImagePreview is not a WriteableBitmap, but instead a BitmapFrameDecode
                    //which is what WPF creates when you load an image from a file
                    GraphicsUtils.transparentImage(TileSetImagePreview);
                }
                _tileSetImagePreviewInitialized = true;
            };

            overlayTilesetGrid.Loaded += (s, e) =>
            {
                if (!_overlayTilesetGridInitialized)
                {
                    GraphicsUtils.DrawGridOnCanvas(overlayTilesetGrid, 512, 512, GridDimention, Brushes.Gray, 0.5);
                }
                _overlayTilesetGridInitialized = true;
            };

            CheckerboardBackground.Loaded += (s, e) =>
            {
                if (!_checkerboardBackgroundInitialized)
                {
                    GraphicsUtils.DrawCheckerboard(CheckerboardBackground, GridDimention);
                }
                _checkerboardBackgroundInitialized = true;
            };

        }

        private void About_Click(object sender, RoutedEventArgs e)
        {
            string theMsg = "MidiMusicForge" + Environment.NewLine + Environment.NewLine;
            theMsg += "By David S. Shelley - (2025)" + Environment.NewLine + Environment.NewLine;
            MessageBox.Show(theMsg);
        }
        private void CheckBox_Checked_GridOverlay(object sender, RoutedEventArgs e)
        {
            if (overlayTilesetGrid != null)
            {
                overlayTilesetGrid.Visibility = Visibility.Visible;
            }
        }

        private void CheckBox_UnChecked_GridOverlay(object sender, RoutedEventArgs e)
        {
            if (overlayTilesetGrid != null)
            {
                overlayTilesetGrid.Visibility = Visibility.Hidden;
            }
        }

        private void CheckBox_Checked_CheckerboardUnderlay(object sender, RoutedEventArgs e)
        {
            if (CheckerboardBackground != null)
            {
                CheckerboardBackground.Visibility = Visibility.Visible;
            }
        }

        private void CheckBox_UnChecked_CheckerboardUnderlay(object sender, RoutedEventArgs e)
        {
            if (CheckerboardBackground != null)
            {
                CheckerboardBackground.Visibility = Visibility.Hidden;
            }
        }

        private void MyGrid_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
            {
                double delta = e.Delta > 0 ? 0.1 : -0.1;
                double currentValue = ZoomSlider.Value;
                double newValue = Math.Clamp(currentValue + delta, ZoomSlider.Minimum, ZoomSlider.Maximum);

                ZoomSlider.Value = newValue; // This triggers ZoomSlider_ValueChanged
                e.Handled = true;
            }
        }

        private void removeSelectionFromSelectTool(SelectTool selectedTool)
        {
            // Remove the selection
            if (selectedTool is SelectTool concreteSelectTool)
            {
                concreteSelectTool.SelectionRect = null;
                concreteSelectTool.LastValidSelectionRect = null;
                concreteSelectTool.SelectionAsBitmap = null;
                concreteSelectTool.LastValidSelectionAsBitmap = null;
                concreteSelectTool.MouseIsDown = false;
                concreteSelectTool.IsDraggingSelection = false;
                concreteSelectTool.useThisGridDimension = GridDimention;
            }
        }

        private void ZoomSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {

            double newZoom = e.NewValue;

            if (UnifiedScaleTransform != null)
            {
                // Display the current percentage
                // Convert to percentage string
                string zoomText = $"{Math.Round(newZoom * 100)}%";
                // Clear selection to avoid it auto-selecting the nearest ComboBoxItem
                ZoomComboBox.SelectedItem = null;
                ZoomComboBox.Text = zoomText;

                double currentScale = UnifiedScaleTransform.ScaleX;

                UnifiedScaleTransform.ScaleX = newZoom;
                UnifiedScaleTransform.ScaleY = newZoom;

                if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
                {
                    // Mouse position relative to ScrollViewer
                    Point mousePosInScrollViewer = Mouse.GetPosition(scrollViewerForImage);
                    double logicalX = (scrollViewerForImage.HorizontalOffset + mousePosInScrollViewer.X) / currentScale;
                    double logicalY = (scrollViewerForImage.VerticalOffset + mousePosInScrollViewer.Y) / currentScale;

                    void OnLayoutUpdated(object? sender, EventArgs e)
                    {
                        // Unhook after first call
                        scrollViewerForImage.LayoutUpdated -= OnLayoutUpdated;

                        double newOffsetX = logicalX * newZoom - mousePosInScrollViewer.X;
                        double newOffsetY = logicalY * newZoom - mousePosInScrollViewer.Y;

                        scrollViewerForImage.ScrollToHorizontalOffset(newOffsetX);
                        scrollViewerForImage.ScrollToVerticalOffset(newOffsetY);
                    }

                    scrollViewerForImage.LayoutUpdated += OnLayoutUpdated;
                    e.Handled = true;
                }
            }

        }
        private void Toolboar_Grid_MouseEnter(object sender, MouseEventArgs e)
        {
            Mouse.OverrideCursor = Cursors.Arrow;
        }

        private void Toolboar_Grid_MouseLeave(object sender, MouseEventArgs e)
        {
            Mouse.OverrideCursor = null;
        }

        private void ZoomOut_Click(object sender, RoutedEventArgs e)
        {
            ZoomSlider.Value *= 0.75;
        }

        private void ZoomIn_Click(object sender, RoutedEventArgs e)
        {
            ZoomSlider.Value *= 1.25;
        }

        private void Image_MouseMove_Tileset(object sender, MouseEventArgs e)
        {
            Point position = e.GetPosition((IInputElement)sender);
            ToolResult aToolResult;


            if (MouseIsDownOnPlayLine)
            {
                const double TimeScale = 100.0; // 100 ms per horizontal pixel

                // Move to the new time
                var targetTime = TimeSpan.FromMilliseconds(position.X * TimeScale);

                PlayLine.X1 = position.X;
                PlayLine.Y1 = 0;

                PlayLine.X2 = position.X;
                PlayLine.Y2 = overlayTilesetCursor.ActualHeight;

                if (isPlaying)
                {
                    CurrentTimeInSeconds = targetTime.TotalSeconds;
                    playback.MoveToTime(new MetricTimeSpan(targetTime));
                }
            }

            // If user trying to grab the PlayLine
            int grabLineAllowance = 4;
            int playLineGrabMaxX = (int)PlayLine.X1 + grabLineAllowance;
            int playLineGrabMinX = (int)PlayLine.X1 - grabLineAllowance;
            int whereTheyMovedX = (int)position.X;

            if ((whereTheyMovedX <= playLineGrabMaxX) &&
                (whereTheyMovedX >= playLineGrabMinX))
            {
                // Pause the song
                Mouse.OverrideCursor = Cursors.ScrollWE;
                return;
            }
            else
            {
                Mouse.OverrideCursor = Cursors.Cross;
            }


            if (selectedTool != ToolMode.None)
            {

                aToolResult = TheTool?.OnMouseMove(TileSetImage, TileSetImagePreview, overlayTilesetSelection, TileMapArray, position, GridDimention, selectedBrush);

                // Save Undo state
                if (aToolResult?.Success == true && aToolResult.ShouldSaveForUndo)
                {
                    // Ensure UI has rendered before capturing image state
                    TileSetImage.Dispatcher.Invoke(() => { }, DispatcherPriority.Render);

                    if (TileSetImage.Source is BitmapSource source)
                    {
                        var currentImage = new WriteableBitmap(source);
                        var currentState = new EditorState(currentImage, TileMapArray);
                        undoRedoManager.SaveState(currentState);
                    }
                }


                if (aToolResult?.SelectionRect != null)
                {
                    selectionRectTxtBox.Text =
                        aToolResult?.SelectionRect.Value.Width.ToString() + "px ," +
                        aToolResult?.SelectionRect.Value.Height.ToString() + "px";
                }
                else
                {
                    selectionRectTxtBox.Text = "";
                }
            }

            // Coordinates to display
            Point dipPosition = e.GetPosition((IInputElement)sender);
            var dpi = VisualTreeHelper.GetDpi((Visual)sender);
            Point pixelPosition = new Point(dipPosition.X * dpi.DpiScaleX, dipPosition.Y * dpi.DpiScaleY);
            mouseLoc.Text = pixelPosition.X.ToString("0") + ", " + pixelPosition.Y.ToString("0");

            (int theRow, int theColumn) = GraphicsUtils.GetGridXYFromPosition(sender, position, GridDimention);
            gridLoc.Text = "R: " + theRow.ToString() + " C: " + theColumn.ToString();
        }

        private void Image_MouseDown_Tileset(object sender, MouseButtonEventArgs e)
        {
            IsDragging = true;
            Point position = e.GetPosition((IInputElement)sender);
            ToolResult aToolResult;

            // If user trying to grab the PlayLine
            int grabLineAllowance = 4;
            int playLineGrabMaxX = grabLineAllowance + (int)PlayLine.X1;
            int playLineGrabMinX = grabLineAllowance - (int)PlayLine.X1;
            int whereTheyClickedX = (int)position.X;

            if ((whereTheyClickedX <= playLineGrabMaxX) &&
                (whereTheyClickedX >= playLineGrabMinX))
            {
                MouseIsDownOnPlayLine = true;
                // Don't do anything else
                return;
            }


            if (selectedTool != ToolMode.None)
            {
                aToolResult = TheTool?.OnMouseDown(TileSetImage, TileSetImagePreview, overlayTilesetSelection, TileMapArray, position, GridDimention, selectedBrush);

                // Save Undo state
                if (aToolResult?.Success == true && aToolResult.ShouldSaveForUndo)
                {
                    // Ensure UI has rendered before capturing image state
                    TileSetImage.Dispatcher.Invoke(() => { }, DispatcherPriority.Render);

                    if (TileSetImage.Source is BitmapSource source)
                    {
                        var currentImage = new WriteableBitmap(source);
                        var currentState = new EditorState(currentImage, TileMapArray);
                        undoRedoManager.SaveState(currentState);
                    }
                }

                if (aToolResult?.SelectionRect != null)
                {
                    selectionRectTxtBox.Text =
                        aToolResult?.SelectionRect.Value.Width.ToString() + "px ," +
                        aToolResult?.SelectionRect.Value.Height.ToString() + "px";
                }
                else
                {
                    selectionRectTxtBox.Text = "";
                }
            }
            mouseLoc.Text = position.X.ToString("0") + ", " + position.Y.ToString("0");
            (int theRow, int theColumn) = GraphicsUtils.GetGridXYFromPosition(sender, position, GridDimention);
            gridLoc.Text = "R: " + theRow.ToString() + " C: " + theColumn.ToString();
        }

        private void Image_MouseUp_Tileset(object sender, MouseButtonEventArgs e)
        {
            IsDragging = false;
            MouseDownLastLoc = null;
            MouseIsDownOnPlayLine = false;
            Point position = e.GetPosition((IInputElement)sender);

            if (selectedTool != ToolMode.None)
            {
                ToolResult aToolResult = TheTool?.OnMouseUp(TileSetImage, TileSetImagePreview, overlayTilesetSelection, TileMapArray, position, GridDimention, selectedBrush);

                // Save Undo state
                if (aToolResult?.Success == true && aToolResult.ShouldSaveForUndo)
                {
                    // Ensure UI has rendered before capturing image state
                    TileSetImage.Dispatcher.Invoke(() => { }, DispatcherPriority.Render);

                    if (TileSetImage.Source is BitmapSource source)
                    {
                        var currentImage = new WriteableBitmap(source);
                        var currentState = new EditorState(currentImage, TileMapArray);
                        undoRedoManager.SaveState(currentState);
                    }
                }

                if (aToolResult?.SelectionRect != null)
                {
                    selectionRectTxtBox.Text =
                        aToolResult?.SelectionRect.Value.Width.ToString() + "px ," +
                        aToolResult?.SelectionRect.Value.Height.ToString() + "px";
                }
                else
                {
                    selectionRectTxtBox.Text = "";
                }
            }


            mouseLoc.Text = position.X.ToString("0") + ", " + position.Y.ToString("0");
            (int theRow, int theColumn) = GraphicsUtils.GetGridXYFromPosition(sender, position, GridDimention);
            gridLoc.Text = "R: " + theRow.ToString() + " C: " + theColumn.ToString();
        }

        private void TileSet_ScrollViewer_MouseMove(object sender, MouseEventArgs e)
        {
        }

        private void ScrollViewerForImage_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {

        }

        private void Image_MouseEnter_Tileset(object sender, MouseEventArgs e)
        {
            Mouse.OverrideCursor = Cursors.Cross;
        }

        private void Image_MouseLeave_Tileset(object sender, MouseEventArgs e)
        {
            Mouse.OverrideCursor = null;
            MouseIsDownOnPlayLine = false;
            Point position = e.GetPosition((IInputElement)sender);
            ToolResult aToolResult;

            aToolResult = TheTool?.OnMouseLeave(TileSetImage, TileSetImagePreview, overlayTilesetSelection, TileMapArray, position, GridDimention, selectedBrush);
        }

        private void ZoomComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ZoomComboBox.SelectedItem is ComboBoxItem selectedItem)
            {
                string? content = selectedItem.Content as string;

                if (content != null && content.EndsWith("%"))
                {
                    string numberPart = content.TrimEnd('%');

                    if (double.TryParse(numberPart, out double percent))
                    {
                        double newValue = percent / 100.0;
                        ZoomSlider.Value = newValue;
                    }
                }
            }
        }

        private void ZoomComboBox_LostFocus(object sender, RoutedEventArgs e)
        {

        }

        public void SetGridDimension(int gridDim)
        {
            // Clear any selection since the grid is about to change
            savePreviewLayerToImageWhenSelectTool();
            if (overlayTilesetSelection != null)
            {
                overlayTilesetSelection.Children.Clear();
            }

            // Save the new grid dimension to the select tool
            GridDimention = gridDim;

            if (selectTool is SelectTool concreteSelectTool)
            {
                removeSelectionFromSelectTool(concreteSelectTool);
                concreteSelectTool.useThisGridDimension = GridDimention;
            }
            // Redraw the new grid and background
            GraphicsUtils.DrawGridOnCanvas(overlayTilesetGrid,
                ImageWidth,
                ImageHeight,
                GridDimention,
                Brushes.Gray,
                0.5);

            GraphicsUtils.DrawCheckerboard(CheckerboardBackground, GridDimention);

        }

        private void MainGrid_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                // Force choosing the select tool
                if (TheTool is SelectTool concreteSelectTool)
                {
                    concreteSelectTool.EscapeSelection(TileSetImage, TileSetImagePreview, overlayTilesetSelection);
                }
            }

            // Ctrl + C for Copy selection
            if (e.Key == Key.C && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            {
                if (TheTool is SelectTool concreteSelectTool)
                {
                }
            }
            // Ctrl + V for Past selection
            if (e.Key == Key.V && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            {
                if (TheTool is SelectTool concreteSelectTool)
                {
                }
            }

            // Ctrl + A for Select All
            if (e.Key == Key.A && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            {
                savePreviewLayerToImageWhenSelectTool();
                deselectToolButtons();
                overlayTilesetSelection.Children.Clear();
                selectedTool = ToolMode.Select;
                TheTool = selectTool;
                // Add back tool bar
                /*
                selectBtn.BorderBrush = new SolidColorBrush(Color.FromRgb(76, 194, 255));
                selectBtn.BorderThickness = new Thickness(1);
                */
                if (TheTool is SelectTool concreteSelectTool)
                {
                    concreteSelectTool.SelectAll(TileSetImage, TileSetImagePreview, overlayTilesetSelection, TileMapArray);
                }
            }

            // Ctrl + Z for Undo
            if (e.Key == Key.Z && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            {
                var now = DateTime.Now;
                if (now - _lastUndoTime >= _undoCooldown)
                {
                    _lastUndoTime = now;

                    undoRedoManager.Undo();
                    if (undoRedoManager.currentState != null && undoRedoManager.currentState.Image != null)
                    {
                        WriteableBitmap restoreThisImage = undoRedoManager.currentState.Image;
                        if (restoreThisImage != null)
                        {
                            // Remove the selection
                            if (selectTool is SelectTool concreteSelectTool)
                            {
                                overlayTilesetSelection.Children.Clear();
                                removeSelectionFromSelectTool(concreteSelectTool);
                            }
                            TileSetImage.Source = new WriteableBitmap(restoreThisImage);
                            int pixelWidth = restoreThisImage.PixelWidth;
                            int pixelHeight = restoreThisImage.PixelHeight;
                            imgDimensions.Text = pixelWidth + " x " + pixelHeight;
                        }
                    }
                }

                e.Handled = true; //Prevents further propagation of this key event
                return;
            }

            // Ctrl + Y for Re-Undo
            if (e.Key == Key.Y && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            {
                undoRedoManager.Redo();
                if (undoRedoManager.currentState?.Image != null)
                {
                    WriteableBitmap restoreThisImage = undoRedoManager.currentState.Image;
                    if (restoreThisImage != null)
                    {
                        // Remove the selection
                        if (selectTool is SelectTool concreteSelectTool)
                        {
                            overlayTilesetSelection.Children.Clear();
                            removeSelectionFromSelectTool(concreteSelectTool);
                        }
                        TileSetImage.Source = new WriteableBitmap(restoreThisImage);
                        int pixelWidth = restoreThisImage.PixelWidth;
                        int pixelHeight = restoreThisImage.PixelHeight;
                        imgDimensions.Text = pixelWidth + " x " + pixelHeight;
                    }
                }

                e.Handled = true; // Optional: Prevents further propagation of this key event
                return;
            }

            if (e.Key == Key.Delete)
            {

                if (TheTool is SelectTool concreteSelectTool)
                {
                    concreteSelectTool.DeleteSelection(TileSetImage, TileSetImagePreview, overlayTilesetSelection);
                }
            }
        }
        private void savePreviewLayerToImageWhenSelectTool()
        {
            if (TheTool is SelectTool concreteSelectTool)
            {
                concreteSelectTool.saveValidSelectionBitmapToImageLayer(TileSetImage, TileSetImagePreview);
            }
        }
        private void Pencil_Click(object sender, RoutedEventArgs e)
        {
            savePreviewLayerToImageWhenSelectTool();
            // Clear selection canvas
            SelectionRect = null;
            overlayTilesetSelection.Children.Clear();
            deselectToolButtons();
            selectedTool = ToolMode.Pencil;
            // Add back tool bar
            /*
            pencilBtn.BorderBrush = new SolidColorBrush(Color.FromRgb(76, 194, 255));
            pencilBtn.BorderThickness = new Thickness(1);
            */
            TheTool = pencilTool;
        }

        private void SelectGrid_Click(object sender, RoutedEventArgs e)
        {
            savePreviewLayerToImageWhenSelectTool();
            deselectToolButtons();
            overlayTilesetSelection.Children.Clear();
            selectedTool = ToolMode.Select;
            TheTool = selectTool;

            // Remove the selection
            if (selectTool is SelectTool concreteSelectTool)
            {
                removeSelectionFromSelectTool(concreteSelectTool);
                concreteSelectTool.shouldUseGrid = true;
                concreteSelectTool.shouldUseEllipse = false;
            }
            // Add back tool bar
            /*
            selectBtn.BorderBrush = new SolidColorBrush(Color.FromRgb(76, 194, 255));
            selectBtn.BorderThickness = new Thickness(1);
            */
        }

        private void resizeImage(int newWidth, int newHeight)
        {
            if ((newWidth != ImageWidth) || (newHeight != ImageHeight))
            {
                var src = TileSetImage.Source as BitmapSource;

                // Save the preview before resizing
                savePreviewLayerToImageWhenSelectTool();
                overlayTilesetSelection.Children.Clear();

                // Remove the selection
                if (selectTool is SelectTool concreteSelectTool)
                {
                    removeSelectionFromSelectTool(concreteSelectTool);
                }

                // Resize the image and save
                WriteableBitmap? shrunkImage = GraphicsUtils.resizeImageSource(TileSetImage, newWidth, newHeight);
                if (shrunkImage != null)
                {
                    TileSetImage.Source = shrunkImage;


                    ImageWidth = newWidth;
                    ImageHeight = newHeight;
                    RefreshLayers();

                    int pixelWidth = shrunkImage.PixelWidth;
                    int pixelHeight = shrunkImage.PixelHeight;
                    imgDimensions.Text = pixelWidth + " x " + pixelHeight;

                    int neededRows = pixelHeight / GridDimention;
                    int neededCols = pixelWidth / GridDimention;

                    // ---------------------------------
                    EditorCell[,] tempTileMapArray = CellUtils.CreateEmptyTileMapArray(neededRows, neededCols);

                    // Copy the old array to the new one
                    int numRowsInOldArray = TileMapArray.GetLength(0);
                    int numColsInOldArray = TileMapArray.GetLength(1);
                    // Move from top row to bottom row
                    for (int curRowCounter = 0; curRowCounter < neededRows; curRowCounter++)
                    {
                        // Move left to right through the row columns
                        for (int curColCounter = 0; curColCounter < neededCols; curColCounter++)
                        {
                            bool newLocationExistsInOldArray = (curRowCounter <= (numRowsInOldArray - 1)) && (curColCounter <= (numColsInOldArray - 1));
                            if (newLocationExistsInOldArray)
                            {
                                int savedTileId = tempTileMapArray[curRowCounter, curColCounter].TileId;
                                EditorCell curCell = TileMapArray[curRowCounter, curColCounter];
                                // The IsEmpty of the TileMapArray cell should be correct, so no need to set it
                                // Set the TileId since it is different than the one in TileMapArray
                                curCell.TileId = savedTileId;
                                tempTileMapArray[curRowCounter, curColCounter] = curCell;
                            }
                        }
                    }
                    TileMapArray = tempTileMapArray;
                }

            }
        }

        private string getPlayTimeString()
        {
            var currentMetricTime = playback.GetCurrentTime<MetricTimeSpan>();
            var totalMetricTime = playback.GetDuration<MetricTimeSpan>();
            TimeSpan currentTime = TimeSpan.FromMilliseconds(currentMetricTime.TotalMicroseconds / 1000);
            TimeSpan totalTime = TimeSpan.FromMilliseconds(totalMetricTime.TotalMicroseconds / 1000);
            string timeDisplay = $"{currentTime:mm\\:ss} / {totalTime:mm\\:ss}";
            return timeDisplay;
        }

        private void PlaybackTimer_Tick(object sender, EventArgs e)
        {
            if (playback == null || MidiFileLoaded == null)
            {
                return;
            }
            // PlayLine is line that is a child of the canvas
            PlayLine.Visibility = Visibility.Visible;

            var metricTime = playback.GetCurrentTime<MetricTimeSpan>();
            double totalMilliseconds =
                metricTime.Minutes * 60_000 +
                metricTime.Seconds * 1000 +
                metricTime.Milliseconds;

            double x = totalMilliseconds / 100.0; // if 1px = 100ms

            PlayLine.X1 = x;
            PlayLine.Y1 = 0;

            PlayLine.X2 = x;
            PlayLine.Y2 = overlayTilesetCursor.ActualHeight;

            // Auto scrol base on PlayLine position
            double scale = UnifiedScaleTransform.ScaleX; // Assuming uniform scaling
            scrollViewerForImage.ScrollToHorizontalOffset((x * scale) - scrollViewerForImage.ViewportWidth / 2);

            // Update progress bar
            CurrentTimeInSeconds = metricTime.TotalSeconds;

            // Update play time
            songTimeTextBlock.Text = getPlayTimeString();

            // --- Equalizer Visualization Logic ---
            double currentTimeMs = playback.GetCurrentTime<MetricTimeSpan>().TotalMilliseconds;
            double windowEndMs = currentTimeMs + playbackTimer.Interval.TotalMilliseconds;
            midiEqControl.renderEqualizer(currentTimeMs, windowEndMs);
        }


        private void pauseSong()
        {
            if (isPlaying && !isPaused)
            {
                playback.Stop();    // Pause

                playbackTimer?.Stop();
                playbackStopwatch?.Stop();
                // Ensure no notes continue to play
                outputDevice.TurnAllNotesOff();


                isPaused = true;
                isPlaying = false;
                savetilesetImg.Source = new BitmapImage(new Uri("pack://application:,,,/Controls/MidiEditorControl/MidiEditorImages/play.png"));
            }
        }

        private void startSong()
        {
            if (isPaused)
            {
                playback.Start();
                playbackTimer?.Start();
                playbackStopwatch?.Start();

                isPaused = false;
                isPlaying = true;
                savetilesetImg.Source = new BitmapImage(new Uri("pack://application:,,,/Controls/MidiEditorControl/MidiEditorImages/pause.png"));
            }
        }


        private async void PlayFile_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (MidiFileLoaded == null)
                {
                    MessageBox.Show("Error, No MIDI file loaded");
                    return;
                }

                // If playing and not paused, pause playback
                if (isPlaying && !isPaused)
                {
                    pauseSong();
                    return;
                }

                // If paused, resume playback
                if (isPaused)
                {
                    startSong();
                    return;
                }

                // If not playing and not paused, start playback fresh
                // This device takes MIDI messages and renders them into sound using General MIDI sounds.
                // You cannot capture the audio being played from this synth directly in your application via standard libraries like NAudio.
                // Therefore, you cannot easily create visualizations(like equalizers or spectrograms) based on the actual audio output—because you're not generating or intercepting the sound.
                // The Microsoft GS Wavetable Synth uses its own built-in set of instrument sounds, which function very similarly to a General MIDI (GM) SoundFont, though it's not a user-accessible .sf2 file.
                outputDevice = OutputDevice.GetByName("Microsoft GS Wavetable Synth");
                playback = MidiFileLoaded.GetPlayback(outputDevice);

                playback.Finished += (s, e2) =>
                {
                    isPlaying = false;
                    isPaused = false;

                    playbackTimer?.Stop();
                    playbackStopwatch?.Stop();

                    Dispatcher.Invoke(() =>
                    {
                        savetilesetImg.Source = new BitmapImage(new Uri("pack://application:,,,/Controls/MidiEditorControl/MidiEditorImages/play.png"));
                    });

                    playback.Dispose();
                    outputDevice.Dispose();
                    playback = null;
                    outputDevice = null;
                    midiEqControl.ZeroEqualizerBars();
                };

                isPlaying = true;
                isPaused = false;
                savetilesetImg.Source = new BitmapImage(new Uri("pack://application:,,,/Controls/MidiEditorControl/MidiEditorImages/pause.png"));

                playbackStopwatch = Stopwatch.StartNew();
                playbackTimer = new DispatcherTimer();
                playbackTimer.Interval = TimeSpan.FromMilliseconds(30); // update ~33 FPS
                playbackTimer.Tick += PlaybackTimer_Tick;
                playbackTimer.Start();

                await Task.Run(() =>
                {
                    playback.Start();
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error during playback: {ex}");
            }
        }

        private Melanchall.DryWetMidi.Interaction.MetricTimeSpan getFinalNoteTime(ICollection<Melanchall.DryWetMidi.Interaction.Note> notes, TempoMap tempoMap)
        {
            var maxEndTime = new MetricTimeSpan(); // initialized to 0

            foreach (var note in notes)
            {
                var start = note.TimeAs<MetricTimeSpan>(tempoMap);
                var duration = note.LengthAs<MetricTimeSpan>(tempoMap);
                var endTime = start + duration;

                if (endTime.CompareTo(maxEndTime) > 0)
                    maxEndTime = endTime;
            }

            return maxEndTime;
        }

        // This ischromatic a rainbow, where each semitone gets a distinct color
        private Color GetColorForNote(int noteNumber)
        {
            int pitchClass = noteNumber % 12; // C=0, C#=1, D=2, ..., B=11

            switch (pitchClass)
            {
                case 0: return Colors.Red;      // C
                case 1: return Colors.OrangeRed; // C#
                case 2: return Colors.Orange;   // D
                case 3: return Colors.Yellow;   // D#
                case 4: return Colors.LimeGreen; // E
                case 5: return Colors.Green;    // F
                case 6: return Colors.LightSeaGreen; // F#
                case 7: return Colors.Blue;     // G
                case 8: return Colors.DarkViolet; // G#
                case 9: return Colors.Indigo;   // A
                case 10: return Colors.Magenta;  // A#
                case 11: return Colors.DeepPink; // B
                default: return Colors.Gray; // Fallback
            }
        }

        private async void OpenMidiFile_Click(object sender, RoutedEventArgs e)
        {
            // The OpenFileDialog is a UI operation and must run on the main thread
            Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog();

            openFileDialog.InitialDirectory = String.IsNullOrEmpty(lastFileDir) ? Directory.GetCurrentDirectory() : lastFileDir;
            openFileDialog.Title = "Select a Media File";
            openFileDialog.Filter = "MIDI Files (*.mid;*.midi;*.smf;*.rmi)|*.mid;*.midi;*.smf;*.rmi|Standard MIDI File (*.mid)|*.mid|MIDI File (*.midi)|*.midi|SMF (Standard MIDI File) (*.smf)|*.smf|RIFF MIDI (*.rmi)|*.rmi|All Files (*.*)|*.*";

            bool? result = openFileDialog.ShowDialog();

            if (result == true)
            {
                string filePath = openFileDialog.FileName;

                if (System.IO.File.Exists(filePath))
                {
                    pauseSong();
                    midiEqControl.ZeroEqualizerBars();

                    // Step 1: Update UI to show the loading overlay
                    LoadingTextBlock.Text = $"Loading: {System.IO.Path.GetFileName(filePath)}";
                    LoadingOverlay.Visibility = Visibility.Visible;
                    scrollViewerForImage.Visibility = Visibility.Hidden;

                    // Wait for the UI to truly finish rendering before starting the heavy work
                    await GraphicsUtils.WaitForRender();

                    try
                    {
                        // Step 2: Offload all the heavy lifting to a background thread
                        await Task.Run(() =>
                        {
                            // Clear previous resources
                            playback?.Dispose();
                            outputDevice?.Dispose();
                            playback = null;
                            outputDevice = null;
                            isPlaying = false;
                            isPaused = false;

                            // All UI-related property changes from the background thread need Dispatcher.Invoke
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                savetilesetImg.Source = new BitmapImage(new Uri("pack://application:,,,/Controls/MidiEditorControl/MidiEditorImages/play.png"));
                            });

                            lastFileDir = System.IO.Path.GetDirectoryName(filePath);

                            // Load the file and process the data
                            MidiFileLoaded = MidiFile.Read(filePath);
                            MidiFileTempoMapLoaded = MidiFileLoaded.GetTempoMap();
                            var notes = MidiFileLoaded.GetNotes();
                            var finalNoteTime = getFinalNoteTime(notes, MidiFileTempoMapLoaded);

                            // Update properties that are bound to the UI
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                SongDurationInSeconds = finalNoteTime.TotalSeconds;
                                CurrentTimeInSeconds = 0.0;
                            });

                            // Resize and clear the image
                            const double TimeScale = 100.0;
                            int newWidthInPixels = (int)Math.Ceiling(finalNoteTime.TotalMilliseconds / TimeScale);
                            int newHeightInPixels = 127 * GridDimention;

                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                resizeImage(newWidthInPixels, newHeightInPixels);
                                GraphicsUtils.transparentImage(TileSetImage);
                            });

                            // Draw notes onto the image in the background
                            foreach (var note in notes)
                            {
                                var startTime = TimeConverter.ConvertTo<MetricTimeSpan>(note.Time, MidiFileTempoMapLoaded);
                                var duration = Melanchall.DryWetMidi.Interaction.LengthConverter.ConvertTo<MetricTimeSpan>(note.Length, note.Time, MidiFileTempoMapLoaded);
                                double x = Math.Round(startTime.TotalMilliseconds / TimeScale);
                                double width = Math.Round(duration.TotalMilliseconds / TimeScale);
                                double y = (127 - note.NoteNumber) * GridDimention;
                                double adjustedToGridY = y - (GridDimention / 2);

                                Point firstPoint = new Point(x, adjustedToGridY);
                                Point lastPoint = new Point(x + width, adjustedToGridY);
                                Color brushColor = GetColorForNote(note.NoteNumber);

                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    GraphicsUtils.DrawSquareLineOnImage(TileSetImage, firstPoint, lastPoint, GridDimention, brushColor);
                                });
                            }

                            // Allow equalizer to read from the midi file
                            midiEqControl.PreprocessMidiFileForEqualizer(MidiFileLoaded, MidiFileTempoMapLoaded);


                            // If not playing and not paused, start playback fresh
                            // This device takes MIDI messages and renders them into sound using General MIDI sounds.
                            // You cannot capture the audio being played from this synth directly in your application via standard libraries like NAudio.
                            // Therefore, you cannot easily create visualizations(like equalizers or spectrograms) based on the actual audio output—because you're not generating or intercepting the sound.
                            // The Microsoft GS Wavetable Synth uses its own built-in set of instrument sounds, which function very similarly to a General MIDI (GM) SoundFont, though it's not a user-accessible .sf2 file.
                            outputDevice = OutputDevice.GetByName("Microsoft GS Wavetable Synth");
                            playback = MidiFileLoaded.GetPlayback(outputDevice);


                            // Update play time
                            Dispatcher.Invoke(() =>
                            {
                                songTimeTextBlock.Text = getPlayTimeString();
                            });
                        });

                        // Step 3: Once the background task is complete, run final UI updates on the main thread
                        TilesetPath = filePath;
                        tilesetFileNameTxtBox.Visibility = Visibility.Visible;
                        tilesetFileNameTxtBox.Text = System.IO.Path.GetFileName(filePath);
                        tilesetFileNameTxtBox.ScrollToHorizontalOffset(double.MaxValue);
                        PlayLine.X1 = 0;
                        PlayLine.Y1 = 0;
                        PlayLine.X2 = 0;
                        PlayLine.Y2 = overlayTilesetCursor.ActualHeight;
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error loading tilemap: {ex}");
                    }
                    finally
                    {
                        // Step 4: Hide the overlay in the 'finally' block to ensure it's always closed
                        LoadingOverlay.Visibility = Visibility.Collapsed;
                        scrollViewerForImage.Visibility = Visibility.Visible;
                    }

                }
            }
        }

        private void AdjustPlaybackTime(double secondsToAdjust)
        {
            if (playback == null)
            {
                return;
            }

            // Get current time as a MetricTimeSpan
            var currentMetricTime = playback.GetCurrentTime<MetricTimeSpan>();

            // Create a MetricTimeSpan for the adjustment amount
            // Note: MetricTimeSpan constructor only takes positive values for hours, minutes, seconds.
            // The sign of secondsToAdjust determines if we add or subtract.
            var adjustmentAmount = new MetricTimeSpan(0, 0, Math.Abs((int)secondsToAdjust));

            MetricTimeSpan newMetricTime;

            var durationMetricTime = playback.GetDuration<MetricTimeSpan>();

            if (secondsToAdjust > 0) // Fast Forward
            {
                if (currentMetricTime + adjustmentAmount > durationMetricTime)
                {
                    newMetricTime = durationMetricTime;
                }
                else
                {
                    newMetricTime = currentMetricTime + adjustmentAmount;
                }
            }
            else // Rewind (secondsToAdjust < 0)
            {
                if (currentMetricTime > adjustmentAmount)
                {
                    newMetricTime = currentMetricTime - adjustmentAmount;
                }
                else
                {
                    newMetricTime = new MetricTimeSpan(0, 0, 0);
                }
            }

            // Move to the new time
            CurrentTimeInSeconds = newMetricTime.TotalSeconds;
            playback.MoveToTime(newMetricTime);
        }

        private void Rewind_Click(object sender, RoutedEventArgs e)
        {
            AdjustPlaybackTime(-5); // Fast forward by 5 seconds
        }

        private void FastForward_Click(object sender, RoutedEventArgs e)
        {
            AdjustPlaybackTime(5); // Fast forward by 5 seconds
        }
        private void SongProgressSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (SongProgressSlider.IsMouseCaptureWithin)
            {
                IsUserDraggingSlider = true;
                double newValue = e.NewValue;

                var desiredSecondsInTimeSpan = new MetricTimeSpan(0, 0, Math.Abs((int)newValue));
                if (isPlaying)
                {
                    // Limit the rate of calls since it can hang
                    if (timer.ElapsedMilliseconds >= 100)
                    {
                        timer.Restart();
                        playback.MoveToTime(desiredSecondsInTimeSpan);
                    }

                }
            }
        }

        private void SetAppVolume(float volume)
        {
            var enumerator = new MMDeviceEnumerator();
            var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            var sessionManager = device.AudioSessionManager;
            var sessions = sessionManager.Sessions;

            int pid = Process.GetCurrentProcess().Id;

            for (int i = 0; i < sessions.Count; i++)
            {
                var session = sessions[i];
                if ((int)session.GetProcessID == pid)
                {
                    session.SimpleAudioVolume.Volume = volume;
                    break;
                }
            }
        }

        private void SetSystemVolume(float volume)
        {
            if (volume < 0)
            {
                volume = 0;
            }
            var enumerator = new MMDeviceEnumerator();
            var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            device.AudioEndpointVolume.MasterVolumeLevelScalar = volume;
        }


        private void HandleVolumeChange(double newValue, Action<float> setVolumeFunc, Image targetImage)
        {
            // Scale velocity based on desired volume (0.0 to 1.0)
            float volume = (float)((newValue - 1) / 99.0);
            if (volume < 0) { volume = 0; }

            setVolumeFunc(volume);

            string imagePath;

            if (newValue == 0)
            {
                imagePath = "volumemute.png";
            }
            else if (newValue < 20)
            {
                imagePath = "volume0.png";
            }
            else if (newValue < 40)
            {
                imagePath = "volume1.png";
            }
            else if (newValue < 60)
            {
                imagePath = "volume2.png";
            }
            else // 60–100
            {
                imagePath = "volume3.png";
            }

            targetImage.Source = new BitmapImage(
                new Uri($"pack://application:,,,/Controls/MidiEditorControl/MidiEditorImages/{imagePath}"));
        }

        private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            HandleVolumeChange(e.NewValue, SetAppVolume, volumeImg);

        }

        private void VolumeSystemSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            HandleVolumeChange(e.NewValue, SetSystemVolume, volumeSystemImg);
        }

    }
}
