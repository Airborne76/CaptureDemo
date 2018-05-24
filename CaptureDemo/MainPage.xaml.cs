using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Effects;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Graphics;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;
using Windows.Media.Editing;
using Windows.Media.Transcoding;
using Windows.System;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x804 上介绍了“空白页”项模板

namespace CaptureDemo
{
    /// <summary>
    /// 可用于自身或导航至 Frame 内部的空白页。
    /// </summary>
    public sealed partial class MainPage : Page
    {
        public MainPage()
        {
            this.InitializeComponent();
        }
        private GraphicsCaptureItem _item;
        private SizeInt32 _lastSize;
        private Direct3D11CaptureFramePool _framePool;
        private CanvasDevice _canvasDevice;
        private GraphicsCaptureSession _session;
        private CompositionDrawingSurface _surface;
        private CanvasBitmap canvasBitmap;
        private CanvasSwapChain _swapChain;
        private MediaComposition composition;
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            bool isSupport = GraphicsCaptureSession.IsSupported();
            if (isSupport)
            {
                caputurebutton.Visibility = Visibility.Visible;
                
            }
            //MessageDialog messageDialog = new MessageDialog(isSupport.ToString());
            //await messageDialog.ShowAsync();
        }

        private async void Button_Click_1(object sender, RoutedEventArgs e)
        {
            _canvasDevice = new CanvasDevice();
            await StartCaptureAsync();
            
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            StopCapture();
            StopButton.Visibility = Visibility.Collapsed;
            SaveMediaFile();
        }
        public async Task StartCaptureAsync()
        {
            composition = new MediaComposition();            
            // The GraphicsCapturePicker follows the same pattern the 
            // file pickers do. 
            var picker = new GraphicsCapturePicker();
            GraphicsCaptureItem item = await picker.PickSingleItemAsync();
            // The item may be null if the user dismissed the 
            // control without making a selection or hit Cancel. 
            if (item != null)
            {
                StopButton.Visibility = Visibility.Visible;
                // We'll define this method later in the document.
                StartCaptureInternal(item);
            }
        }
        public void StartCaptureInternal(GraphicsCaptureItem item)
        {
            StopCapture();
            _item = item;
            _lastSize = _item.Size;
            _swapChain = new CanvasSwapChain(_canvasDevice, _item.Size.Width, _item.Size.Height, 96);
            
            swapChain.SwapChain = _swapChain;

            _framePool = Direct3D11CaptureFramePool.Create(
                _canvasDevice, // D3D device 
                DirectXPixelFormat.B8G8R8A8UIntNormalized, // Pixel format 
                60, // Number of frames 
                _item.Size); // Size of the buffers   
            _session = _framePool.CreateCaptureSession(_item);
            _framePool.FrameArrived += (s, a) =>
            {
                using (var frame = _framePool.TryGetNextFrame())
                {
                    ProcessFrame(frame);
                }
            };
            _item.Closed += (s, a) =>
            {
                StopCapture();
            };
            _session.StartCapture();
        }
        public void StopCapture()
        {
            _session?.Dispose();
            _framePool?.Dispose();
            _item = null;
            _session = null;
            _framePool = null;
            
        }
        public async void SaveMediaFile()
        {
            var picker = new Windows.Storage.Pickers.FileSavePicker();
            picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.VideosLibrary;
            picker.FileTypeChoices.Add("MP4 files", new List<string>() { ".mp4" });
            picker.SuggestedFileName = "RenderedComposition.mp4";
            Windows.Storage.StorageFile file = await picker.PickSaveFileAsync();
            if (file != null)
            {
                // Call RenderToFileAsync
                var saveOperation = composition.RenderToFileAsync(file, MediaTrimmingPreference.Precise);

                saveOperation.Progress = new AsyncOperationProgressHandler<TranscodeFailureReason, double>(async (info, progress) =>
                {
                    await this.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, new DispatchedHandler(() =>
                    {
                        Debug.WriteLine(string.Format("Saving file... Progress: {0:F0}%", progress));
                    }));
                });
                saveOperation.Completed = new AsyncOperationWithProgressCompletedHandler<TranscodeFailureReason, double>(async (info, status) =>
                {
                    await this.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, new DispatchedHandler(() =>
                    {
                        try
                        {
                            var results = info.GetResults();
                            if (results != TranscodeFailureReason.None || status != AsyncStatus.Completed)
                            {
                                Debug.WriteLine("Saving was unsuccessful");
                            }
                            else
                            {
                                Debug.WriteLine("Trimmed clip saved to file");
                            }
                        }
                        finally
                        {
                            // Update UI whether the operation succeeded or not
                        }

                    }));
                });
            }
            else
            {
                Debug.WriteLine("User cancelled the file selection");
            }


        }
        private TimeSpan lastFrameTime=TimeSpan.Zero;
        private void ProcessFrame(Direct3D11CaptureFrame frame)
        {
            // Resize and device-lost leverage the same function on the
            // Direct3D11CaptureFramePool. Refactoring it this way avoids 
            // throwing in the catch block below (device creation could always 
            // fail) along with ensuring that resize completes successfully and 
            // isn’t vulnerable to device-lost.   
            bool needsReset = false;
            bool recreateDevice = false;

            if ((frame.ContentSize.Width != _lastSize.Width) ||
                (frame.ContentSize.Height != _lastSize.Height))
            {
                needsReset = true;
                _lastSize = frame.ContentSize;
                _swapChain.ResizeBuffers(_lastSize.Width, _lastSize.Height);
            }
            Direct3D11CaptureFrame direct=frame;
            try
            {
                // Take the D3D11 surface and draw it into a  
                // Composition surface.
                    if (direct.SystemRelativeTime - lastFrameTime < TimeSpan.FromSeconds(1))
                    {
                        //Fuck Microsoft🤬
                        MediaClip mediaClip = MediaClip.CreateFromSurface(direct.Surface, direct.SystemRelativeTime - lastFrameTime);
                        composition.Clips.Add(mediaClip);

                    }
                    lastFrameTime = direct.SystemRelativeTime;

                    // Convert our D3D11 surface into a Win2D object.
                    canvasBitmap = CanvasBitmap.CreateFromDirect3D11Surface(
                    _canvasDevice,
                    direct.Surface);

                    using (var drawingSession = _swapChain.CreateDrawingSession(Colors.Transparent))
                    {

                        //drawingSession.DrawCircle(400, 300, 100, Colors.Red, 20);
                        ScaleEffect effect = new ScaleEffect()
                        {
                            Source = canvasBitmap,
                            Scale = new Vector2((float)swapChain.ActualWidth / _item.Size.Width)
                        };

                        drawingSession.DrawImage(effect);

                    }
                
                _swapChain.Present();

                //canvasControl.Invalidate();
                // Helper that handles the drawing for us, not shown.              

            }
            // This is the device-lost convention for Win2D.
            catch (Exception e) when (_canvasDevice.IsDeviceLost(e.HResult))
            {
                // We lost our graphics device. Recreate it and reset 
                // our Direct3D11CaptureFramePool.  
                needsReset = true;
                recreateDevice = true;
            }
            if (needsReset)
            {
                ResetFramePool(direct.ContentSize, recreateDevice);
            }
        }
        
        private void CanvasControl_Draw(Microsoft.Graphics.Canvas.UI.Xaml.CanvasControl sender, Microsoft.Graphics.Canvas.UI.Xaml.CanvasDrawEventArgs args)
        {

            CanvasDrawingSession ds = args.DrawingSession;
            ds.Clear(Colors.White);
            if (canvasBitmap != null)
            {
                //ds.DrawRectangle(10, 10, 100, 100, Colors.Red);
                ds.DrawImage(canvasBitmap);
            }
        }
        private void ResetFramePool(SizeInt32 size, bool recreateDevice)
        {
            do
            {
                try
                {
                    if (recreateDevice)
                    {
                        _canvasDevice = new CanvasDevice();
                    }

                    _framePool.Recreate(
                        _canvasDevice,
                        DirectXPixelFormat.B8G8R8A8UIntNormalized,
                        2,
                        size);
                }
                // This is the device-lost convention for Win2D.
                catch (Exception e) when (_canvasDevice.IsDeviceLost(e.HResult))
                {
                    _canvasDevice = null;
                    recreateDevice = true;
                }
            } while (_canvasDevice == null);
        }

    }
}
